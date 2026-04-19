using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence.Region
{
    /// <summary>
    /// 基于 region 文件路径维护的锁映射。
    /// <para>该类型负责按标准化路径管理锁项，并提供显式 Enter / Exit 能力。</para>
    /// </summary>
    public sealed class ChunkRegionPathLockMap
    {
        /// <summary>
        /// 锁项。
        /// <para>记录实际互斥对象与当前被 Enter 持有的次数。</para>
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
        /// 按 region 文件路径维护的锁表。
        /// </summary>
        private readonly Dictionary<string, RegionLockEntry> _lockEntries =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 锁表总锁。
        /// </summary>
        private readonly object _lockTableLock = new();

        /// <summary>
        /// 进入指定 region 文件路径对应的锁。
        /// </summary>
        public void Enter(string regionFilePath, string ownerName)
        {
            string normalizedRegionFilePath = NormalizeRegionFilePath(regionFilePath, ownerName, nameof(Enter));
            RegionLockEntry lockEntry = AcquireLockEntry(normalizedRegionFilePath);
            try
            {
                Monitor.Enter(lockEntry.LockObject);
            }
            catch
            {
                ReleaseLockEntry(normalizedRegionFilePath, lockEntry, ownerName, nameof(Enter));
                throw;
            }
        }

        /// <summary>
        /// 退出指定 region 文件路径对应的锁。
        /// </summary>
        public void Exit(string regionFilePath, string ownerName)
        {
            string normalizedRegionFilePath = NormalizeRegionFilePath(regionFilePath, ownerName, nameof(Exit));
            RegionLockEntry lockEntry = GetLockEntryOrThrow(normalizedRegionFilePath, ownerName, nameof(Exit));

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
                    ReleaseLockEntry(normalizedRegionFilePath, lockEntry, ownerName, nameof(Exit));
                }
            }
        }

        /// <summary>
        /// 将 region 文件路径标准化为锁表键。
        /// </summary>
        private static string NormalizeRegionFilePath(string regionFilePath, string ownerName, string callerName)
        {
            if (string.IsNullOrWhiteSpace(regionFilePath))
            {
                throw new ArgumentException(
                    $"[{ownerName}] {callerName}: regionFilePath 不能为空。",
                    nameof(regionFilePath));
            }

            return Path.GetFullPath(regionFilePath);
        }

        /// <summary>
        /// 获取指定 region 文件对应的锁项，并增加引用次数。
        /// </summary>
        private RegionLockEntry AcquireLockEntry(string normalizedRegionFilePath)
        {
            lock (_lockTableLock)
            {
                if (!_lockEntries.TryGetValue(normalizedRegionFilePath, out RegionLockEntry lockEntry))
                {
                    lockEntry = new RegionLockEntry();
                    _lockEntries.Add(normalizedRegionFilePath, lockEntry);
                }

                lockEntry.ReferenceCount++;
                return lockEntry;
            }
        }

        /// <summary>
        /// 获取指定 region 文件对应的锁项；若不存在则抛出同步异常。
        /// </summary>
        private RegionLockEntry GetLockEntryOrThrow(string normalizedRegionFilePath, string ownerName, string callerName)
        {
            lock (_lockTableLock)
            {
                if (_lockEntries.TryGetValue(normalizedRegionFilePath, out RegionLockEntry lockEntry))
                {
                    return lockEntry;
                }
            }

            throw new SynchronizationLockException(
                $"[{ownerName}] {callerName}: region 文件 {normalizedRegionFilePath} 当前没有可释放的锁。");
        }

        /// <summary>
        /// 减少锁项引用次数，并在归零后从锁表中移除。
        /// </summary>
        private void ReleaseLockEntry(
            string normalizedRegionFilePath,
            RegionLockEntry lockEntry,
            string ownerName,
            string callerName)
        {
            lock (_lockTableLock)
            {
                if (!_lockEntries.TryGetValue(normalizedRegionFilePath, out RegionLockEntry currentLockEntry) ||
                    !ReferenceEquals(currentLockEntry, lockEntry))
                {
                    throw new SynchronizationLockException(
                        $"[{ownerName}] {callerName}: region 文件 {normalizedRegionFilePath} 的锁项状态异常。");
                }

                if (lockEntry.ReferenceCount <= 0)
                {
                    throw new SynchronizationLockException(
                        $"[{ownerName}] {callerName}: region 文件 {normalizedRegionFilePath} 的引用计数非法。");
                }

                lockEntry.ReferenceCount--;
                if (lockEntry.ReferenceCount == 0)
                {
                    _lockEntries.Remove(normalizedRegionFilePath);
                }
            }
        }
    }
}
