using System;

namespace WorldWeaver.MapCore.ChunkCore.Persistence.Region.InfoOperator
{
    /// <summary>
    /// 空闲分区锁表。对指定路径的 region 文件提供空闲分区链互斥锁。
    /// </summary>
    public static class ChunkRegionFreePartitionLockTable
    {
        /// <summary>
        /// 锁映射实例。
        /// </summary>
        private static readonly ChunkRegionPathLockMap _LOCK_MAP = new();

        /// <summary>
        /// 锁定指定 region 文件，返回释放句柄。
        /// </summary>
        public static IDisposable Lock(string regionFilePath)
        {
            return _LOCK_MAP.Lock(regionFilePath);
        }
    }
}
