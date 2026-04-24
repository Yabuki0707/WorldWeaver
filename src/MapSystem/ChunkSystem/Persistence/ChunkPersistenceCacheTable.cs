using System.Collections.Generic;
using WorldWeaver.MapSystem.ChunkSystem.Data;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// 持久化缓存项快照。
    /// <para>快照用于让缓存器在锁外做调度判断，避免直接暴露缓存表内部字典。</para>
    /// </summary>
    /// <param name="chunkKey">区块坐标 long key。</param>
    /// <param name="storage">缓存的区块储存对象；可以为 null，表示读取确认磁盘中没有旧数据。</param>
    /// <param name="storedTick">该缓存项写入缓存表时的缓存器 tick。</param>
    internal readonly struct ChunkPersistenceCacheSnapshot(
        long chunkKey,
        ChunkDataStorage storage,
        ulong storedTick)
    {
        /// <summary>
        /// 区块坐标 long key。
        /// </summary>
        public long ChunkKey { get; } = chunkKey;

        /// <summary>
        /// 缓存的区块储存对象。
        /// <para>该值为 null 时，表示一次读取已经确认磁盘中没有该 chunk 数据。</para>
        /// </summary>
        public ChunkDataStorage Storage { get; } = storage;

        /// <summary>
        /// 写入缓存表时的 tick。
        /// </summary>
        public ulong StoredTick { get; } = storedTick;

        /// <summary>
        /// 判断该缓存快照在指定 tick 下是否过期。
        /// </summary>
        /// <param name="currentTick">当前缓存器 tick。</param>
        /// <param name="expirationTicks">缓存项允许停留在缓存表中的最大 tick 数。</param>
        /// <returns>缓存快照是否已经达到过期期限。</returns>
        public bool IsExpired(ulong currentTick, ulong expirationTicks)
        {
            // currentTick 小于 StoredTick 代表 tick 溢出或外部传参异常，此时不能按过期处理。
            return currentTick >= StoredTick && currentTick - StoredTick >= expirationTicks;
        }
    }

    /// <summary>
    /// 区块持久化缓存表。
    /// <para>该表完全存在于内存中，并以 chunk key 缓存最新 IO 结果或保存结果。</para>
    /// </summary>
    public sealed class ChunkPersistenceCacheTable
    {
        /// <summary>
        /// 缓存表内部项。
        /// <para>该类型只保存在字典内部，不向外暴露，外部读取统一转换为 <see cref="ChunkPersistenceCacheSnapshot"/>。</para>
        /// </summary>
        /// <param name="storage">缓存的区块储存对象；可以为 null，表示读取确认磁盘中没有旧数据。</param>
        /// <param name="storedTick">缓存写入时的缓存器 tick。</param>
        private sealed class CacheEntry(ChunkDataStorage storage, ulong storedTick)
        {
            /// <summary>
            /// 缓存的区块储存对象。
            /// </summary>
            public readonly ChunkDataStorage Storage = storage;

            /// <summary>
            /// 缓存项写入缓存表时的 tick。
            /// </summary>
            public readonly ulong StoredTick = storedTick;
        }

        /// <summary>
        /// 缓存表主字典。
        /// <para>键为区块坐标 long key，值为该 chunk 当前最新的持久化缓存项。</para>
        /// </summary>
        private readonly Dictionary<long, CacheEntry> _entries = new();

        /// <summary>
        /// 缓存表总锁。
        /// <para>该表当前采用普通 Dictionary，因此所有读写都必须经由该锁保护。</para>
        /// </summary>
        private readonly object _lockObject = new();

        /// <summary>
        /// 尝试夺取缓存项。
        /// <para>返回 true 表示缓存表中确实存在该 chunk，并会立即从缓存表中删除该项。</para>
        /// <para><paramref name="storage"/> 可以为 null，表示磁盘中无旧数据这一读取结果被成功夺取。</para>
        /// </summary>
        /// <param name="chunkKey">需要夺取缓存项的区块坐标 long key。</param>
        /// <param name="storage">夺取出的区块储存对象；可以为 null。</param>
        /// <returns>缓存表中是否存在并成功移除了该缓存项。</returns>
        public bool TryTakeOutStorage(long chunkKey, out ChunkDataStorage storage)
        {
            lock (_lockObject)
            {
                // 夺取语义要求“读到并删除”必须在同一个锁内完成，避免另一个线程看到已被消费的缓存。
                if (!_entries.TryGetValue(chunkKey, out CacheEntry entry))
                {
                    storage = null;
                    return false;
                }

                // 先取值再删除，确保调用方拿到的对象就是本次被移除的缓存项。
                storage = entry.Storage;
                _entries.Remove(chunkKey);
                return true;
            }
        }

        /// <summary>
        /// 查询缓存表中是否存在指定 chunk。
        /// </summary>
        /// <param name="chunkKey">需要查询的区块坐标 long key。</param>
        /// <returns>缓存表中是否存在该 chunk 的缓存项。</returns>
        public bool Contains(long chunkKey)
        {
            lock (_lockObject)
            {
                // 该查询常用于分发前二次确认，必须读取锁内的实时字典状态。
                return _entries.ContainsKey(chunkKey);
            }
        }

        /// <summary>
        /// 写入或覆盖指定 chunk 的缓存项。
        /// </summary>
        /// <param name="chunkKey">需要写入缓存的区块坐标 long key。</param>
        /// <param name="storage">需要缓存的区块储存对象；可以为 null。</param>
        /// <param name="storedTick">该缓存项写入时的缓存器 tick。</param>
        public void SetStorage(long chunkKey, ChunkDataStorage storage, ulong storedTick)
        {
            lock (_lockObject)
            {
                // 覆盖写入代表缓存表永远保留最新结果，旧读取结果或旧保存结果直接失效。
                _entries[chunkKey] = new CacheEntry(storage, storedTick);
            }
        }

        /// <summary>
        /// 尝试获取指定 chunk 的缓存快照。
        /// </summary>
        /// <param name="chunkKey">需要读取快照的区块坐标 long key。</param>
        /// <param name="snapshot">读取出的不可变快照。</param>
        /// <returns>缓存表中是否存在该 chunk 的缓存项。</returns>
        internal bool TryGetSnapshot(long chunkKey, out ChunkPersistenceCacheSnapshot snapshot)
        {
            lock (_lockObject)
            {
                // 快照只复制引用和值，不暴露内部 CacheEntry，防止锁外修改表状态。
                if (!_entries.TryGetValue(chunkKey, out CacheEntry entry))
                {
                    snapshot = default;
                    return false;
                }

                snapshot = new ChunkPersistenceCacheSnapshot(chunkKey, entry.Storage, entry.StoredTick);
                return true;
            }
        }

        /// <summary>
        /// 获取当前全部已经过期的缓存快照。
        /// </summary>
        /// <param name="currentTick">当前缓存器 tick。</param>
        /// <param name="expirationTicks">缓存项过期期限 tick。</param>
        /// <returns>扫描时已经过期的缓存快照列表。</returns>
        internal List<ChunkPersistenceCacheSnapshot> GetExpiredSnapshots(ulong currentTick, ulong expirationTicks)
        {
            List<ChunkPersistenceCacheSnapshot> snapshots = [];
            lock (_lockObject)
            {
                // 只在锁内枚举字典；返回给缓存器的是快照列表，后续判断不再持有表锁。
                foreach (KeyValuePair<long, CacheEntry> pair in _entries)
                {
                    ChunkPersistenceCacheSnapshot snapshot = new(pair.Key, pair.Value.Storage, pair.Value.StoredTick);
                    if (snapshot.IsExpired(currentTick, expirationTicks))
                    {
                        snapshots.Add(snapshot);
                    }
                }
            }

            return snapshots;
        }

        /// <summary>
        /// 若指定缓存项仍然过期，则将其移除。
        /// </summary>
        /// <param name="chunkKey">需要移除的区块坐标 long key。</param>
        /// <param name="currentTick">当前缓存器 tick。</param>
        /// <param name="expirationTicks">缓存项过期期限 tick。</param>
        /// <returns>是否确实移除了一个仍处于过期状态的缓存项。</returns>
        public bool RemoveIfExpired(long chunkKey, ulong currentTick, ulong expirationTicks)
        {
            lock (_lockObject)
            {
                // 重新在锁内读取当前项，避免根据过期扫描时的旧快照删除新写入的缓存。
                if (!_entries.TryGetValue(chunkKey, out CacheEntry entry))
                {
                    return false;
                }

                ChunkPersistenceCacheSnapshot snapshot = new(chunkKey, entry.Storage, entry.StoredTick);
                if (!snapshot.IsExpired(currentTick, expirationTicks))
                {
                    return false;
                }

                _entries.Remove(chunkKey);
                return true;
            }
        }

        /// <summary>
        /// 若指定缓存项仍然过期，且写入 tick 与指定值一致，则将其移除。
        /// </summary>
        /// <param name="chunkKey">需要移除的区块坐标 long key。</param>
        /// <param name="expectedStoredTick">Store 任务创建时记录的缓存写入 tick。</param>
        /// <param name="currentTick">当前缓存器 tick。</param>
        /// <param name="expirationTicks">缓存项过期期限 tick。</param>
        /// <returns>是否确实移除了匹配的过期缓存项。</returns>
        internal bool RemoveIfExpiredAndStoredTickMatches(
            long chunkKey,
            ulong expectedStoredTick,
            ulong currentTick,
            ulong expirationTicks)
        {
            lock (_lockObject)
            {
                // Store 完成回收必须重新校验缓存项存在性，因为该缓存可能已被 TryTakeOut 消费。
                if (!_entries.TryGetValue(chunkKey, out CacheEntry entry))
                {
                    return false;
                }

                // tick 不一致说明缓存项已被新的 TrySave 覆盖，旧 Store 不允许删除新缓存。
                if (entry.StoredTick != expectedStoredTick)
                {
                    return false;
                }

                ChunkPersistenceCacheSnapshot snapshot = new(chunkKey, entry.Storage, entry.StoredTick);
                if (!snapshot.IsExpired(currentTick, expirationTicks))
                {
                    return false;
                }

                _entries.Remove(chunkKey);
                return true;
            }
        }

        /// <summary>
        /// 移除指定 chunk 的缓存项。
        /// </summary>
        /// <param name="chunkKey">需要移除缓存的区块坐标 long key。</param>
        /// <returns>是否确实移除了一个缓存项。</returns>
        public bool Remove(long chunkKey)
        {
            lock (_lockObject)
            {
                // 普通移除只服务于明确的外部清理场景，不进行过期或 tick 校验。
                return _entries.Remove(chunkKey);
            }
        }
    }
}
