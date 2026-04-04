using Godot;
using WorldWeaver.MapSystem.ChunkSystem;
using WorldWeaver.MapSystem.PositionConverter;

namespace WorldWeaver.MapSystem.TileSystem
{
    /// <summary>
    /// 局部 Tile 坐标转换器。
    /// <para>用于处理局部 Tile 坐标的合法性校验、归一化、索引换算以及向全局坐标的还原。</para>
    /// </summary>
    public static class LocalTilePositionConverter
    {
        // ================================================================================
        //                                  合法性判断
        // ================================================================================

        /// <summary>
        /// 判断局部 Tile 坐标在指定 Chunk 尺寸下是否有效。
        /// </summary>
        public static bool IsValid(Vector2I localTilePosition, MapElementSize chunkSize)
        {
            return chunkSize.IsValidLocalPosition(localTilePosition.X, localTilePosition.Y);
        }

        /// <summary>
        /// 将局部 Tile 坐标规范化到指定 Chunk 尺寸的合法范围内。
        /// </summary>
        public static Vector2I ToValidLocalTilePosition(Vector2I localTilePosition, MapElementSize chunkSize)
        {
            return chunkSize.ToValidLocalPosition(localTilePosition.X, localTilePosition.Y);
        }

        // ================================================================================
        //                                  坐标换算
        // ================================================================================

        /// <summary>
        /// 根据局部 Tile 坐标与 Chunk 坐标还原全局 Tile 坐标。
        /// </summary>
        public static Vector2I ToGlobalTilePosition(
            Vector2I localTilePosition,
            MapElementSize chunkSize,
            ChunkPosition chunkPosition)
        {
            return LocalPositionSizeExpConverter.ToGlobalPosition(
                localTilePosition.X,
                localTilePosition.Y,
                chunkPosition.X,
                chunkPosition.Y,
                chunkSize
            );
        }

        // ================================================================================
        //                                  索引换算
        // ================================================================================

        /// <summary>
        /// 将局部 Tile 坐标转换为 Chunk 内一维索引。
        /// </summary>
        public static int ToTileIndex(Vector2I localTilePosition, MapElementSize chunkSize)
        {
            return LocalPositionSizeExpConverter.ToTileIndex(localTilePosition.X, localTilePosition.Y, chunkSize);
        }
    }
}
