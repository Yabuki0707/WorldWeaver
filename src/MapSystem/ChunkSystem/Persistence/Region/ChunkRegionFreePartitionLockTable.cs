using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Godot;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence.Region
{
    /// <summary>
    /// ChunkRegion 空闲分区锁表。
    /// <para>该静态类负责按 region 文件路径维护锁对象，并提供显式加锁与解锁能力。</para>
    /// <para>调用方只需要提供 region 文件路径，不应接触锁表内部状态。</para>
    /// </summary>
    public static class ChunkRegionFreePartitionLockTable
    {
        /// <summary>
        /// 区域锁项。
        /// <para>记录实际互斥对象与当前被多少次 Enter 持有。</para>
        /// </summary>
        private sealed class RegionLockEntry
        {
            /// <summary>
            /// 真正用于互斥的锁对象。
            /// </summary>
            public object LockObject { get; } = new();

            /// <summary>
            /// 当前锁项被 Enter 持有的次数。
            /// </summary>
            public int ReferenceCount { get; set; }
        }

        /// <summary>
        /// 按 region 文件路径维护的空闲分区操作锁表。
        /// </summary>
        private static readonly Dictionary<string, RegionLockEntry> _REGION_FREE_PARTITION_LOCKS =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 锁表总锁。
        /// </summary>
        private static readonly object _LOCK_TABLE_LOCK = new();

        /// <summary>
        /// 进入指定 region 文件对应的空闲分区锁。
        /// </summary>
        public static void EnterRegionLock(string regionFilePath)
        {
            string normalizedRegionFilePath = NormalizeRegionFilePath(regionFilePath, nameof(EnterRegionLock));
            RegionLockEntry lockEntry = AcquireLockEntry(normalizedRegionFilePath);
            try
            {
                Monitor.Enter(lockEntry.LockObject);
            }
            catch
            {
                ReleaseLockEntry(normalizedRegionFilePath, lockEntry);
                throw;
            }
        }

        /// <summary>
        /// 退出指定 region 文件对应的空闲分区锁。
        /// </summary>
        public static void ExitRegionLock(string regionFilePath)
        {
            string normalizedRegionFilePath = NormalizeRegionFilePath(regionFilePath, nameof(ExitRegionLock));
            RegionLockEntry lockEntry = GetLockEntryOrThrow(normalizedRegionFilePath);

            bool monitorExited = false;
            try
            {
                Monitor.Exit(lockEntry.LockObject);
                monitorExited = true;
            }
            finally
            {
                if (monitorExited)
                {
                    ReleaseLockEntry(normalizedRegionFilePath, lockEntry);
                }
            }
        }

        /// <summary>
        /// 将 region 文件路径标准化为锁表键。
        /// </summary>
        private static string NormalizeRegionFilePath(string regionFilePath, string callerName)
        {
            if (string.IsNullOrWhiteSpace(regionFilePath))
            {
                throw new ArgumentException(
                    $"[ChunkRegionFreePartitionLockTable] {callerName}: regionFilePath 不能为空。",
                    nameof(regionFilePath));
            }

            return Path.GetFullPath(regionFilePath);
        }

        /// <summary>
        /// 获取指定 region 文件对应的锁项，并增加引用次数。
        /// </summary>
        private static RegionLockEntry AcquireLockEntry(string normalizedRegionFilePath)
        {
            lock (_LOCK_TABLE_LOCK)
            {
                // 锁表增项和引用计数增加必须放在同一总锁内，避免刚创建就被并发线程提前回收。
                if (!_REGION_FREE_PARTITION_LOCKS.TryGetValue(normalizedRegionFilePath, out RegionLockEntry lockEntry))
                {
                    lockEntry = new RegionLockEntry();
                    _REGION_FREE_PARTITION_LOCKS.Add(normalizedRegionFilePath, lockEntry);
                }

                lockEntry.ReferenceCount++;
                return lockEntry;
            }
        }

        /// <summary>
        /// 获取指定 region 文件对应的锁项；若不存在则抛出同步异常。
        /// </summary>
        private static RegionLockEntry GetLockEntryOrThrow(string normalizedRegionFilePath)
        {
            lock (_LOCK_TABLE_LOCK)
            {
                if (_REGION_FREE_PARTITION_LOCKS.TryGetValue(normalizedRegionFilePath, out RegionLockEntry lockEntry))
                {
                    return lockEntry;
                }
            }

            throw new SynchronizationLockException(
                $"[ChunkRegionFreePartitionLockTable] ExitRegionLock: region 文件 {normalizedRegionFilePath} 当前没有可释放的锁。");
        }

        /// <summary>
        /// 减少锁项引用次数，并在归零后从锁表中移除。
        /// </summary>
        private static void ReleaseLockEntry(string normalizedRegionFilePath, RegionLockEntry lockEntry)
        {
            lock (_LOCK_TABLE_LOCK)
            {
                if (!_REGION_FREE_PARTITION_LOCKS.TryGetValue(normalizedRegionFilePath, out RegionLockEntry currentLockEntry) ||
                    !ReferenceEquals(currentLockEntry, lockEntry))
                {
                    throw new SynchronizationLockException(
                        $"[ChunkRegionFreePartitionLockTable] ReleaseLockEntry: region 文件 {normalizedRegionFilePath} 的锁项状态异常。");
                }

                if (lockEntry.ReferenceCount <= 0)
                {
                    throw new SynchronizationLockException(
                        $"[ChunkRegionFreePartitionLockTable] ReleaseLockEntry: region 文件 {normalizedRegionFilePath} 的引用计数非法。");
                }

                lockEntry.ReferenceCount--;
                if (lockEntry.ReferenceCount == 0)
                {
                    _REGION_FREE_PARTITION_LOCKS.Remove(normalizedRegionFilePath);
                }
            }
        }
    }
}
