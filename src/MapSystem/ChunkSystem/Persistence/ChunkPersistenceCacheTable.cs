using System;
using System.Collections.Generic;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// 区块持久化缓存表。
    /// <para>该表完全存在于内存中，并以 chunk key 缓存最新 IO 结果或保存结果。</para>
    /// </summary>
    public sealed class ChunkPersistenceCacheTable
    {
        /// <summary>
        /// 缓存表内部项。
        /// <para>该类型只保存在字典内部，不向外暴露，外部通过 out 参数读取必要字段。</para>
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
        /// Read 区块组待办任务分发优先度。
        /// <para>正数表示每分发该数量的 Read 区块组后分发一个 Store 区块组；负数表示反向优先 Store。</para>
        /// <para>设置为 0 时会自动修正为 1，并输出警告。</para>
        /// </summary>
        private int _readTaskGroupDispatchPriority = 5;

        // ================================================================================
        //                                  调度优先度
        // ================================================================================

        /// <summary>
        /// Read 区块组待办任务分发优先度。
        /// <para>正数表示 Read 为优势操作类型，绝对值代表连续分发多少个 Read 任务组后插入一个 Store 任务组。</para>
        /// <para>负数表示 Store 为优势操作类型，绝对值代表连续分发多少个 Store 任务组后插入一个 Read 任务组。</para>
        /// </summary>
        public int ReadTaskGroupDispatchPriority
        {
            get => _readTaskGroupDispatchPriority;
            set
            {
                // 0 没有明确优先语义，按用户约定自动修正为 1，避免调度循环无法确定优势方。
                if (value == 0)
                {
                    GD.PushWarning("[ChunkPersistenceCacheTable] ReadTaskGroupDispatchPriority 不能为 0，已自动调整为 1。");
                    _readTaskGroupDispatchPriority = 1;
                    return;
                }

                _readTaskGroupDispatchPriority = value;
            }
        }

        /// <summary>
        /// 根据 Read 区块组分发优先度拆出优势操作桶与劣势操作桶。
        /// </summary>
        /// <param name="operationBuckets">按操作类型索引保存的一次性分桶结果。</param>
        /// <param name="priorityOperationType">优势操作类型。</param>
        /// <param name="priorityBuckets">优势操作类型对应的 region 桶列表。</param>
        /// <param name="secondaryOperationType">劣势操作类型。</param>
        /// <param name="secondaryBuckets">劣势操作类型对应的 region 桶列表。</param>
        /// <param name="priorityTaskGroupQuota">优势操作类型连续分发任务组数量。</param>
        internal void GetPrioritizedOperationBuckets(
            List<(Vector2I RegionPosition, long[] ChunkKeys)>[] operationBuckets,
            out PersistenceOperationType priorityOperationType,
            out List<(Vector2I RegionPosition, long[] ChunkKeys)> priorityBuckets,
            out PersistenceOperationType secondaryOperationType,
            out List<(Vector2I RegionPosition, long[] ChunkKeys)> secondaryBuckets,
            out int priorityTaskGroupQuota)
        {
            // 先读取当前优先度配置；该值只影响本轮分发，不直接修改待办表或缓存表内容。
            // 后续调度会按这个值拆分出“优势方连续分发多少组、劣势方插入一组”的节奏。
            // 读取属性时再次兜底，防止字段被未来代码绕过 setter 写成 0。
            int priority = ReadTaskGroupDispatchPriority;
            if (priority == 0)
            {
                GD.PushWarning("[ChunkPersistenceCacheTable] ReadTaskGroupDispatchPriority 为 0，已按 1 处理。");
                priority = 1;
            }

            // 正数表示 Read 为优势方；负数表示 Store 为优势方。
            // 这里把方向先确定下来，缓存器后续只处理 priority/secondary 两个抽象角色。
            if (priority > 0)
            {
                priorityOperationType = PersistenceOperationType.Read;
                secondaryOperationType = PersistenceOperationType.Store;
            }
            else
            {
                priorityOperationType = PersistenceOperationType.Store;
                secondaryOperationType = PersistenceOperationType.Read;
            }

            // int.MinValue 取绝对值会溢出，所以单独按 int.MaxValue 处理。
            // 这种值等价于几乎只分发优势方，但仍保留劣势方尝试机会的循环结构。
            priorityTaskGroupQuota = priority == int.MinValue ? int.MaxValue : Math.Abs(priority);
            // 分桶数组第一层索引与 PersistenceOperationType 枚举值一致。
            // GetOperationBucketList 会处理 null、越界等异常输入，让调用方拿到可安全遍历的列表。
            priorityBuckets = GetOperationBucketList(operationBuckets, priorityOperationType);
            secondaryBuckets = GetOperationBucketList(operationBuckets, secondaryOperationType);
        }

        /// <summary>
        /// 从分桶数组中读取指定操作类型对应的 region 桶列表。
        /// </summary>
        /// <param name="operationBuckets">按操作类型索引保存的一次性分桶结果。</param>
        /// <param name="operationType">需要读取的操作类型。</param>
        /// <returns>指定操作类型对应的 region 桶列表。</returns>
        private static List<(Vector2I RegionPosition, long[] ChunkKeys)> GetOperationBucketList(
            List<(Vector2I RegionPosition, long[] ChunkKeys)>[] operationBuckets,
            PersistenceOperationType operationType)
        {
            int operationIndex = (int)operationType;
            if (operationBuckets == null ||
                operationIndex < 0 ||
                operationIndex >= operationBuckets.Length ||
                operationBuckets[operationIndex] == null)
            {
                // 当前操作没有桶时返回空列表，避免调用方为 null 分支写重复保护。
                return [];
            }

            // 返回的是一次性分桶中的原列表引用，调度阶段只通过游标顺序推进。
            // 这里不复制列表，避免多一次无意义的桶遍历和数组分配。
            return operationBuckets[operationIndex];
        }

        // ================================================================================
        //                                  基础操作
        // ================================================================================

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
        /// 尝试获取指定 chunk 的缓存储存对象与写入 tick。
        /// </summary>
        /// <param name="chunkKey">需要读取缓存信息的区块坐标 long key。</param>
        /// <param name="storage">缓存的区块储存对象；可以为 null。</param>
        /// <param name="storedTick">缓存项写入缓存表时的 tick。</param>
        /// <returns>缓存表中是否存在该 chunk 的缓存项。</returns>
        internal bool TryGetStorageInfo(long chunkKey, out ChunkDataStorage storage, out ulong storedTick)
        {
            lock (_lockObject)
            {
                // 只复制调用方需要的引用和值，不暴露内部 CacheEntry。
                if (!_entries.TryGetValue(chunkKey, out CacheEntry entry))
                {
                    storage = null;
                    storedTick = 0;
                    return false;
                }

                storage = entry.Storage;
                storedTick = entry.StoredTick;
                return true;
            }
        }

        /// <summary>
        /// 获取当前全部已经过期的缓存项。
        /// </summary>
        /// <param name="currentTick">当前缓存器 tick。</param>
        /// <param name="expirationTicks">缓存项过期期限 tick。</param>
        /// <returns>扫描时已经过期的缓存项列表。</returns>
        internal List<(long ChunkKey, ChunkDataStorage Storage)> GetExpiredEntries(ulong currentTick, ulong expirationTicks)
        {
            List<(long ChunkKey, ChunkDataStorage Storage)> entries = [];
            lock (_lockObject)
            {
                // 只在锁内枚举字典；返回给缓存器的是普通值元组，后续判断不再持有表锁。
                foreach (KeyValuePair<long, CacheEntry> pair in _entries)
                {
                    if (IsExpired(pair.Value.StoredTick, currentTick, expirationTicks))
                    {
                        entries.Add((pair.Key, pair.Value.Storage));
                    }
                }
            }

            return entries;
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
                // 重新在锁内读取当前项，避免根据过期扫描时的旧结果删除新写入的缓存。
                if (!_entries.TryGetValue(chunkKey, out CacheEntry entry))
                {
                    return false;
                }

                if (!IsExpired(entry.StoredTick, currentTick, expirationTicks))
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

                if (!IsExpired(entry.StoredTick, currentTick, expirationTicks))
                {
                    return false;
                }

                _entries.Remove(chunkKey);
                return true;
            }
        }

        /// <summary>
        /// 判断缓存写入 tick 在当前 tick 下是否已经过期。
        /// </summary>
        /// <param name="storedTick">缓存项写入 tick。</param>
        /// <param name="currentTick">当前缓存器 tick。</param>
        /// <param name="expirationTicks">缓存项允许停留在缓存表中的最大 tick 数。</param>
        /// <returns>缓存项是否已经达到过期期限。</returns>
        internal static bool IsExpired(ulong storedTick, ulong currentTick, ulong expirationTicks)
        {
            // currentTick 小于 storedTick 代表 tick 溢出或外部传参异常，此时不能按过期处理。
            return currentTick >= storedTick && currentTick - storedTick >= expirationTicks;
        }

    }
}
