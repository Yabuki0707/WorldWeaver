using Godot;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// ChunkRegion 区块坐标处理器。
    /// <para>该静态类负责将全局语义的 ChunkPosition 拆分为 region 坐标与 region 内局部区块坐标。</para>
    /// </summary>
    public static class ChunkRegionPositionProcessor
    {
        /// <summary>
        /// 校验指定区块在所属 region 内部的局部坐标是否合法。
        /// </summary>
        public static bool ValidateLocalChunkPosition(Vector2I localChunkPosition)
        {
            if (localChunkPosition.X < 0 || localChunkPosition.X >= ChunkRegionFileLayout.REGION_CHUNK_AXIS ||
                localChunkPosition.Y < 0 || localChunkPosition.Y >= ChunkRegionFileLayout.REGION_CHUNK_AXIS)
            {
                GD.PushError(
                    $"[ChunkRegionPositionProcessor] ValidateLocalChunkPosition: localChunkPosition=({localChunkPosition.X}, {localChunkPosition.Y}) 超出合法范围。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 拆分指定区块坐标，获取所属 region 坐标与 region 内局部区块坐标。
        /// </summary>
        public static void GetRegionAndLocalChunkPosition(
            ChunkPosition chunkPosition,
            out Vector2I regionPosition,
            out Vector2I localChunkPosition)
        {
            regionPosition = new Vector2I(chunkPosition.X >> 5, chunkPosition.Y >> 5);
            localChunkPosition = new Vector2I(
                chunkPosition.X & (ChunkRegionFileLayout.REGION_CHUNK_AXIS - 1),
                chunkPosition.Y & (ChunkRegionFileLayout.REGION_CHUNK_AXIS - 1));
        }

        /// <summary>
        /// 获取指定区块所属的 region 坐标。
        /// </summary>
        public static Vector2I GetRegionPosition(ChunkPosition chunkPosition)
        {
            GetRegionAndLocalChunkPosition(chunkPosition, out Vector2I regionPosition, out _);
            return regionPosition;
        }

        /// <summary>
        /// 获取指定区块在所属 region 内部的局部坐标。
        /// </summary>
        public static Vector2I GetLocalChunkPosition(ChunkPosition chunkPosition)
        {
            GetRegionAndLocalChunkPosition(chunkPosition, out _, out Vector2I localChunkPosition);
            return localChunkPosition;
        }
    }
}
