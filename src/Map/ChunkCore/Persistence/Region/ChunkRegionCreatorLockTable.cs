using System;

namespace WorldWeaver.Map.ChunkCore.Persistence.Region
{
    /// <summary>
    /// Region 创建锁表。对指定路径的 region 文件提供互斥锁，防止并发创建同一文件。
    /// </summary>
    public static class ChunkRegionCreatorLockTable
    {
        /// <summary>
        /// 锁映射实例。
        /// </summary>
        private static readonly ChunkRegionPathLockMap _lockMap = new();

        /// <summary>
        /// 锁定指定 region 文件，返回释放句柄。
        /// </summary>
        public static IDisposable Lock(string regionFilePath)
        {
            return _lockMap.Lock(regionFilePath);
        }
    }
}
