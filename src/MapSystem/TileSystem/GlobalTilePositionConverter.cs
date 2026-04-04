using Godot;
using WorldWeaver.MapSystem.ChunkSystem;
using WorldWeaver.MapSystem.PositionConverter;

namespace WorldWeaver.MapSystem.TileSystem
{
    /// <summary>
    /// 全局 Tile 坐标转换器。
    /// <para>用于处理全局 Tile 坐标与 Chunk 坐标、局部 Tile 坐标之间的换算与归属判断。</para>
    /// </summary>
    public static class GlobalTilePositionConverter
    {
        // ================================================================================
        //                                  Chunk 换算
        // ================================================================================

        /// <summary>
        /// 将全局 Tile 坐标转换为所属 Chunk 坐标。
        /// </summary>
        public static ChunkPosition ToChunkPosition(Vector2I globalTilePosition, MapElementSize chunkSize)
        {
            Vector2I chunkPosition = GlobalPositionSizeExpConverter.ToGlobalParentPosition(globalTilePosition, chunkSize);
            return new ChunkPosition(chunkPosition);
        }

        // ================================================================================
        //                                  局部坐标换算
        // ================================================================================

        /// <summary>
        /// 将全局 Tile 坐标转换为局部 Tile 坐标，并输出所属 Chunk 坐标。
        /// </summary>
        public static Vector2I ToLocalTilePosition(
            Vector2I globalTilePosition,
            MapElementSize chunkSize,
            out ChunkPosition chunkPosition)
        {
            (Vector2I localPosition, Vector2I parentPosition) =
                GlobalPositionSizeExpConverter.ToLocalAndGlobalParentPosition(globalTilePosition, chunkSize);
            chunkPosition = new ChunkPosition(parentPosition);
            return localPosition;
        }

        /// <summary>
        /// 同时将全局 Tile 坐标转换为局部 Tile 坐标与所属 Chunk 坐标。
        /// </summary>
        public static (Vector2I LocalTilePosition, ChunkPosition ChunkPosition) ToLocalAndChunkTilePosition(
            Vector2I globalTilePosition,
            MapElementSize chunkSize)
        {
            Vector2I localTilePosition = ToLocalTilePosition(globalTilePosition, chunkSize, out ChunkPosition chunkPosition);
            return (localTilePosition, chunkPosition);
        }

        /// <summary>
        /// 将全局 Tile 坐标转换为所属 Chunk 内的局部 Tile 坐标。
        /// </summary>
        public static Vector2I ToLocalTilePosition(Vector2I globalTilePosition, MapElementSize chunkSize)
        {
            return new Vector2I(
                globalTilePosition.X & chunkSize.WidthMask,
                globalTilePosition.Y & chunkSize.HeightMask
            );
        }

        // ================================================================================
        //                                  合法性判断
        // ================================================================================

        /// <summary>
        /// 判断全局 Tile 坐标是否属于指定 Chunk。
        /// </summary>
        public static bool IsInChunk(Vector2I globalTilePosition, ChunkPosition chunkPosition, MapElementSize chunkSize)
        {
            return ToChunkPosition(globalTilePosition, chunkSize) == chunkPosition;
        }

        /// <summary>
        /// 尝试将全局 Tile 坐标转换为指定 Chunk 内的局部 Tile 坐标。
        /// </summary>
        public static bool TryGetLocalTilePositionInChunk(
            Vector2I globalTilePosition,
            ChunkPosition chunkPosition,
            MapElementSize chunkSize,
            out Vector2I localTilePosition)
        {
            if (!IsInChunk(globalTilePosition, chunkPosition, chunkSize))
            {
                localTilePosition = Vector2I.Zero;
                return false;
            }

            localTilePosition = ToLocalTilePosition(globalTilePosition, chunkSize);
            return true;
        }
    }
}
