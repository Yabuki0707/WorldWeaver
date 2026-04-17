using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// ChunkRegion 空闲分区锁表。
    /// <para>该静态类负责按 region 文件路径维护锁对象，并封装相关加锁执行逻辑。</para>
    /// </summary>
    public static class ChunkRegionFreePartitionLockTable
    {
        /// <summary>
        /// 区域锁项。
        /// <para>引用计数通过独立的引用类型承载，以便在并发环境下使用 Interlocked 做原子增减。</para>
        /// </summary>
        private readonly struct RegionLockEntry(LockReferenceCounter referenceCounter, object lockObject)
        {
            /// <summary>
            /// 锁项的引用计数容器。
            /// </summary>
            public LockReferenceCounter ReferenceCounter { get; } = referenceCounter;

            /// <summary>
            /// 真正用于互斥的锁对象。
            /// </summary>
            public object LockObject { get; } = lockObject;
        }

        /// <summary>
        /// 锁项引用计数容器。
        /// </summary>
        private sealed class LockReferenceCounter
        {
            /// <summary>
            /// 当前引用次数。
            /// </summary>
            public int Value;
        }

        /// <summary>
        /// 按 region 文件路径维护的空闲分区操作锁表。
        /// </summary>
        private static readonly ConcurrentDictionary<string, RegionLockEntry> _REGION_FREE_PARTITION_LOCKS =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 串行化锁表增删与引用回收流程的门闩。
        /// </summary>
        private static readonly object _LOCK_TABLE_GATE = new();

        /// <summary>
        /// 在指定 region 文件的空闲分区锁内执行操作。
        /// </summary>
        public static void ExecuteLocked(string regionFilePath, Action action)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(regionFilePath);
            ArgumentNullException.ThrowIfNull(action);

            string normalizedRegionFilePath = Path.GetFullPath(regionFilePath);
            RegionLockEntry lockEntry = AcquireLockEntry(normalizedRegionFilePath);
            try
            {
                lock (lockEntry.LockObject)
                {
                    action();
                }
            }
            finally
            {
                ReleaseLockEntry(normalizedRegionFilePath, lockEntry);
            }
        }

        /// <summary>
        /// 在指定 region 文件的空闲分区锁内执行操作并返回结果。
        /// </summary>
        public static T ExecuteLocked<T>(string regionFilePath, Func<T> action)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(regionFilePath);
            ArgumentNullException.ThrowIfNull(action);

            string normalizedRegionFilePath = Path.GetFullPath(regionFilePath);
            RegionLockEntry lockEntry = AcquireLockEntry(normalizedRegionFilePath);
            try
            {
                lock (lockEntry.LockObject)
                {
                    return action();
                }
            }
            finally
            {
                ReleaseLockEntry(normalizedRegionFilePath, lockEntry);
            }
        }

        /// <summary>
        /// 获取指定 region 文件对应的锁项，并增加引用次数。
        /// </summary>
        private static RegionLockEntry AcquireLockEntry(string normalizedRegionFilePath)
        {
            lock (_LOCK_TABLE_GATE)
            {
                RegionLockEntry lockEntry = _REGION_FREE_PARTITION_LOCKS.GetOrAdd(
                    normalizedRegionFilePath,
                    _ => new RegionLockEntry(new LockReferenceCounter(), new object()));
                Interlocked.Increment(ref lockEntry.ReferenceCounter.Value);
                return lockEntry;
            }
        }

        /// <summary>
        /// 减少锁项引用次数，并在归零后从锁表中移除。
        /// </summary>
        private static void ReleaseLockEntry(string normalizedRegionFilePath, RegionLockEntry lockEntry)
        {
            if (Interlocked.Decrement(ref lockEntry.ReferenceCounter.Value) != 0)
            {
                return;
            }

            lock (_LOCK_TABLE_GATE)
            {
                if (lockEntry.ReferenceCounter.Value != 0)
                {
                    return;
                }

                if (_REGION_FREE_PARTITION_LOCKS.TryGetValue(normalizedRegionFilePath, out RegionLockEntry currentLockEntry) &&
                    ReferenceEquals(currentLockEntry.LockObject, lockEntry.LockObject))
                {
                    _REGION_FREE_PARTITION_LOCKS.TryRemove(normalizedRegionFilePath, out _);
                }
            }
        }
    }
}
