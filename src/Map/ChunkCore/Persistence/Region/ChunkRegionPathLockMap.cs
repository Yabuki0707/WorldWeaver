using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace WorldWeaver.Map.ChunkCore.Persistence.Region
{
    /// <summary>
    /// 基于 region 文件路径维护的可重入锁映射，通过 <see cref="Lock"/> 返回 <see cref="IDisposable"/> 句柄以支持 using。
    /// </summary>
    public sealed class ChunkRegionPathLockMap
    {
        /// <summary>
        /// 锁项。记录互斥对象与当前持有次数。
        /// </summary>
        private sealed class RegionLockEntry
        {
            /// <summary>
            /// 真正的锁对象。
            /// </summary>
            public object LockObject { get; } = new();

            /// <summary>
            /// 当前锁被持有的次数。
            /// </summary>
            public int ReferenceCount;
        }

        /// <summary>
        /// 由 <see cref="Lock"/> 返回的可释放锁句柄。
        /// </summary>
        private sealed class LockHandle : IDisposable
        {
            private readonly ChunkRegionPathLockMap _owner;
            private readonly string _normalizedPath;
            private readonly RegionLockEntry _entry;

            public LockHandle(ChunkRegionPathLockMap owner, string normalizedPath, RegionLockEntry entry)
            {
                _owner = owner;
                _normalizedPath = normalizedPath;
                _entry = entry;
            }

            public void Dispose()
            {
                _owner.Release(_normalizedPath, _entry);
            }
        }

        /// <summary>
        /// 按标准化路径索引的锁项表。
        /// </summary>
        private readonly Dictionary<string, RegionLockEntry> _entries =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 锁表总锁。
        /// </summary>
        private readonly object _tableLock = new();

        /// <summary>
        /// 进入指定 region 文件的锁，返回 using 句柄。
        /// </summary>
        public IDisposable Lock(string regionFilePath)
        {
            string normalizedPath = Path.GetFullPath(regionFilePath);

            RegionLockEntry entry;
            lock (_tableLock)
            {
                if (!_entries.TryGetValue(normalizedPath, out entry))
                {
                    entry = new RegionLockEntry();
                    _entries[normalizedPath] = entry;
                }

                entry.ReferenceCount++;
            }

            try
            {
                Monitor.Enter(entry.LockObject);
            }
            catch
            {
                Release(normalizedPath, entry);
                throw;
            }

            return new LockHandle(this, normalizedPath, entry);
        }

        /// <summary>
        /// 释放一次锁持有，并在引用归零后从锁表中移除该项。
        /// </summary>
        private void Release(string normalizedPath, RegionLockEntry entry)
        {
            bool monitorExited = false;
            try
            {
                Monitor.Exit(entry.LockObject);
                monitorExited = true;
            }
            finally
            {
                if (monitorExited)
                {
                    lock (_tableLock)
                    {
                        entry.ReferenceCount--;
                        if (entry.ReferenceCount == 0)
                        {
                            _entries.Remove(normalizedPath);
                        }
                    }
                }
            }
        }
    }
}
