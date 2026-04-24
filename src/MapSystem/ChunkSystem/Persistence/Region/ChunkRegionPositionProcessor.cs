using Godot;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence.Region
{
    /// <summary>
    /// ChunkRegion 区块坐标处理器。
    /// <para>该静态类负责将全局语义的 ChunkPosition 拆分为 region 坐标与 region 内局部区块坐标。</para>
    /// </summary>
    public static class ChunkRegionPositionProcessor
    {
        /// <summary>
        /// 坐标 key 中单轴坐标占用的 bit 数。
        /// <para>ChunkPosition.ToKey 使用高 32 位保存 X，低 32 位保存 Y。</para>
        /// </summary>
        private const int POSITION_KEY_AXIS_BITS = 32;

        /// <summary>
        /// region 轴长对应的右移位数。
        /// <para>当前 region 轴长为 32，即 2^5，因此全局 chunk 坐标右移 5 位即可得到 region 坐标。</para>
        /// </summary>
        private const int REGION_CHUNK_AXIS_BIT_SHIFT = 5;

        /// <summary>
        /// region 内局部 chunk 坐标掩码。
        /// <para>当前 region 轴长为 32，因此低 5 位就是 region 内局部坐标。</para>
        /// </summary>
        private const int LOCAL_CHUNK_POSITION_MASK = ChunkRegionFileLayout.REGION_CHUNK_AXIS - 1;

        /// <summary>
        /// 校验指定区块在所属 region 内部的局部坐标是否合法。
        /// </summary>
        /// <param name="localChunkPosition">需要校验的 region 内局部 chunk 坐标。</param>
        /// <returns>该局部坐标是否落在当前 region 文件布局允许的范围内。</returns>
        public static bool ValidateLocalChunkPosition(Vector2I localChunkPosition)
        {
            if (localChunkPosition.X < 0 || localChunkPosition.X >= ChunkRegionFileLayout.REGION_CHUNK_AXIS ||
                localChunkPosition.Y < 0 || localChunkPosition.Y >= ChunkRegionFileLayout.REGION_CHUNK_AXIS)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 拆分指定区块坐标，获取所属 region 坐标与 region 内局部区块坐标。
        /// </summary>
        /// <param name="chunkPosition">需要拆分的全局 chunk 坐标。</param>
        /// <param name="regionPosition">chunk 所属的 region 坐标。</param>
        /// <param name="localChunkPosition">chunk 在所属 region 内部的局部坐标。</param>
        public static void GetRegionAndLocalChunkPosition(
            ChunkPosition chunkPosition,
            out Vector2I regionPosition,
            out Vector2I localChunkPosition)
        {
            regionPosition = GetRegionPosition(chunkPosition);
            localChunkPosition = new Vector2I(
                chunkPosition.X & LOCAL_CHUNK_POSITION_MASK,
                chunkPosition.Y & LOCAL_CHUNK_POSITION_MASK);
        }

        /// <summary>
        /// 获取指定区块所属的 region 坐标。
        /// </summary>
        /// <param name="chunkPosition">需要计算所属 region 的全局 chunk 坐标。</param>
        /// <returns>chunk 所属的 region 坐标。</returns>
        public static Vector2I GetRegionPosition(ChunkPosition chunkPosition)
        {
            return new Vector2I(
                chunkPosition.X >> REGION_CHUNK_AXIS_BIT_SHIFT,
                chunkPosition.Y >> REGION_CHUNK_AXIS_BIT_SHIFT);
        }

        /// <summary>
        /// 根据 chunk long key 获取所属 region 坐标。
        /// <para>该方法直接从 key 的高低 32 位中提取 chunk 坐标并右移，不需要先构造 ChunkPosition。</para>
        /// </summary>
        /// <param name="chunkKey">由 ChunkPosition.ToKey 生成的 chunk long key。</param>
        /// <returns>chunk 所属的 region 坐标。</returns>
        public static Vector2I GetRegionPosition(long chunkKey)
        {
            GetRegionCoordinates(chunkKey, out int regionX, out int regionY);
            return new Vector2I(regionX, regionY);
        }

        /// <summary>
        /// 根据 chunk long key 获取所属 region 的 long key。
        /// <para>该方法直接用位运算提取 region 坐标并重新打包，避免 ChunkPosition 与 Vector2I 的中间转换。</para>
        /// </summary>
        /// <param name="chunkKey">由 ChunkPosition.ToKey 生成的 chunk long key。</param>
        /// <returns>chunk 所属 region 的 long key。</returns>
        public static long GetRegionKey(long chunkKey)
        {
            GetRegionCoordinates(chunkKey, out int regionX, out int regionY);
            return ToRegionKey(regionX, regionY);
        }

        /// <summary>
        /// 根据 chunk long key 同时获取所属 region 坐标与 region long key。
        /// <para>调用方同时需要坐标和 key 时使用该方法，避免重复拆解 chunk key。</para>
        /// </summary>
        /// <param name="chunkKey">由 ChunkPosition.ToKey 生成的 chunk long key。</param>
        /// <param name="regionPosition">chunk 所属的 region 坐标。</param>
        /// <param name="regionKey">chunk 所属 region 的 long key。</param>
        public static void GetRegionPositionAndKey(
            long chunkKey,
            out Vector2I regionPosition,
            out long regionKey)
        {
            GetRegionCoordinates(chunkKey, out int regionX, out int regionY);
            regionPosition = new Vector2I(regionX, regionY);
            regionKey = ToRegionKey(regionX, regionY);
        }

        /// <summary>
        /// 将 region 坐标转换为稳定 long key。
        /// </summary>
        /// <param name="regionPosition">需要转换的 region 坐标。</param>
        /// <returns>由 X 和 Y 拼接出的 region long key。</returns>
        public static long ToRegionKey(Vector2I regionPosition)
        {
            return ToRegionKey(regionPosition.X, regionPosition.Y);
        }

        /// <summary>
        /// 获取指定区块在所属 region 内部的局部坐标。
        /// </summary>
        /// <param name="chunkPosition">需要计算局部坐标的全局 chunk 坐标。</param>
        /// <returns>chunk 在所属 region 内部的局部坐标。</returns>
        public static Vector2I GetLocalChunkPosition(ChunkPosition chunkPosition)
        {
            GetRegionAndLocalChunkPosition(chunkPosition, out _, out Vector2I localChunkPosition);
            return localChunkPosition;
        }

        /// <summary>
        /// 从 chunk long key 中提取所属 region 坐标。
        /// </summary>
        /// <param name="chunkKey">由 ChunkPosition.ToKey 生成的 chunk long key。</param>
        /// <param name="regionX">提取出的 region X 坐标。</param>
        /// <param name="regionY">提取出的 region Y 坐标。</param>
        private static void GetRegionCoordinates(long chunkKey, out int regionX, out int regionY)
        {
            // chunk key 的高 32 位是 X；整体右移 32 + 5 位即可得到 region X。
            regionX = (int)(chunkKey >> (POSITION_KEY_AXIS_BITS + REGION_CHUNK_AXIS_BIT_SHIFT));

            // chunk key 的低 32 位是 Y；先截成 int 保留符号，再右移 5 位得到 region Y。
            regionY = ((int)chunkKey) >> REGION_CHUNK_AXIS_BIT_SHIFT;
        }

        /// <summary>
        /// 将 region 坐标分量打包为 long key。
        /// </summary>
        /// <param name="regionX">region X 坐标。</param>
        /// <param name="regionY">region Y 坐标。</param>
        /// <returns>由 X 和 Y 拼接出的 region long key。</returns>
        private static long ToRegionKey(int regionX, int regionY)
        {
            return (long)regionX << POSITION_KEY_AXIS_BITS | (uint)regionY;
        }
    }
}
