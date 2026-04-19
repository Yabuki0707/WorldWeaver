namespace WorldWeaver.MapSystem.ChunkSystem.Persistence.Region
{
    /// <summary>
    /// ChunkRegion 创建锁表。
    /// <para>该静态类对外暴露 creator 锁语义，内部通过路径锁映射维护实际锁项。</para>
    /// </summary>
    public static class ChunkRegionCreatorLockTable
    {
        private const string OWNER_NAME = "ChunkRegionCreatorLockTable";

        /// <summary>
        /// creator 锁对应的路径锁映射。
        /// </summary>
        private static readonly ChunkRegionPathLockMap _LOCK_MAP = new();

        /// <summary>
        /// 进入指定 region 文件路径对应的 creator 锁。
        /// </summary>
        public static void EnterRegionLock(string regionFilePath)
        {
            _LOCK_MAP.Enter(regionFilePath, OWNER_NAME);
        }

        /// <summary>
        /// 退出指定 region 文件路径对应的 creator 锁。
        /// </summary>
        public static void ExitRegionLock(string regionFilePath)
        {
            _LOCK_MAP.Exit(regionFilePath, OWNER_NAME);
        }
    }
}
