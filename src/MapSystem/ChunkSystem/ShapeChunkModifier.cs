using System.Collections.Generic;
using Godot;
using WorldWeaver.MapSystem.TileSystem;
using WorldWeaver.PixelShapeSystem;

namespace WorldWeaver.MapSystem.ChunkSystem
{
    /// <summary>
    /// Shape 修改器（静态）。
    /// <para>职责：对区块切片接口给出的 ChunkData 访问能力执行逐点映射与原子操作。</para>
    /// <para>关键约束：</para>
    /// <para>1. 每个点先转“父级全局区块坐标 + 局部坐标”；</para>
    /// <para>2. 父级区块坐标映射到二维数组索引；</para>
    /// <para>3. 索引处为 null 则跳过；否则将局部坐标转 tileIndex 并调用 ChunkData 原子方法。</para>
    /// </summary>
    internal static class ShapeChunkModifier
    {
        /// <summary>
        /// 按 shape 读取命中点值（只读操作）。
        /// </summary>
        internal static TileValueShape GetTiles(PixelShape shape, IShapeChunkSlice chunkSlice, MapElementSize chunkSize)
        {
            List<Vector2I> globalPositions = [];
            List<int> tileRunIds = [];

            // 逐点处理：全局坐标 -> (父级区块坐标 + 局部坐标 -> tileIndex) -> 原子读取。
            foreach (Vector2I globalPosition in shape.GetGlobalCoordinateIterator())
            {
                if (!TryResolveChunkDataAndTileIndex(
                        globalPosition,
                        chunkSlice,
                        chunkSize,
                        out ChunkData chunkData,
                        out int tileIndex))
                {
                    continue;
                }

                globalPositions.Add(globalPosition);
                tileRunIds.Add(chunkData.GetTileSingleUnchecked(tileIndex));
            }

            return TileValueShape.CreateValued(globalPositions, tileRunIds);
        }

        /// <summary>
        /// 按统一值执行设置。
        /// </summary>
        internal static TileValueShape SetTiles(TileRegion tileRegion, IShapeChunkSlice chunkSlice, MapElementSize chunkSize)
        {
            List<Vector2I> globalPositions = [];
            List<int> tileRunIds = [];

            // 逐点映射并原子写入：能映射到已加载 chunk.Data 才执行 Set。
            foreach (Vector2I globalPosition in tileRegion.Shape.GetGlobalCoordinateIterator())
            {
                if (!TryResolveChunkDataAndTileIndex(
                        globalPosition,
                        chunkSlice,
                        chunkSize,
                        out ChunkData chunkData,
                        out int tileIndex))
                {
                    continue;
                }

                int finalTileRunId = chunkData.SetTileSingleUnchecked(tileIndex, tileRegion.TileRunId);
                globalPositions.Add(globalPosition);
                tileRunIds.Add(finalTileRunId);
            }

            return TileValueShape.CreateValued(globalPositions, tileRunIds);
        }

        /// <summary>
        /// 按逐点值执行设置。
        /// </summary>
        internal static TileValueShape SetTiles(TileValueShape tileValueShape, IShapeChunkSlice chunkSlice, MapElementSize chunkSize)
        {
            List<Vector2I> globalPositions = [];
            List<int> tileRunIds = [];

            // 逐点映射并原子写入：使用每个点自带的 tileRunId。
            foreach ((Vector2I globalPosition, int tileRunId) in tileValueShape.GetGlobalValueIterator())
            {
                if (!TryResolveChunkDataAndTileIndex(
                        globalPosition,
                        chunkSlice,
                        chunkSize,
                        out ChunkData chunkData,
                        out int tileIndex))
                {
                    continue;
                }

                int finalTileRunId = chunkData.SetTileSingleUnchecked(tileIndex, tileRunId);
                globalPositions.Add(globalPosition);
                tileRunIds.Add(finalTileRunId);
            }

            return TileValueShape.CreateValued(globalPositions, tileRunIds);
        }

        /// <summary>
        /// 按 shape 逻辑移除。
        /// </summary>
        internal static TileValueShape RemoveTiles(PixelShape shape, IShapeChunkSlice chunkSlice, MapElementSize chunkSize)
        {
            List<Vector2I> globalPositions = [];

            // 逐点映射并原子移除：Remove 不携带 values。
            foreach (Vector2I globalPosition in shape.GetGlobalCoordinateIterator())
            {
                if (!TryResolveChunkDataAndTileIndex(
                        globalPosition,
                        chunkSlice,
                        chunkSize,
                        out ChunkData chunkData,
                        out int tileIndex))
                {
                    continue;
                }

                chunkData.RemoveTileSingleUnchecked(tileIndex);
                globalPositions.Add(globalPosition);
            }

            return TileValueShape.CreateCoordinateOnly(globalPositions);
        }

        /// <summary>
        /// 按 shape 恢复。
        /// </summary>
        internal static TileValueShape RestoreTiles(PixelShape shape, IShapeChunkSlice chunkSlice, MapElementSize chunkSize)
        {
            List<Vector2I> globalPositions = [];
            List<int> tileRunIds = [];

            // 逐点映射并原子恢复：Restore 会返回最终值，因此要收集 values。
            foreach (Vector2I globalPosition in shape.GetGlobalCoordinateIterator())
            {
                if (!TryResolveChunkDataAndTileIndex(
                        globalPosition,
                        chunkSlice,
                        chunkSize,
                        out ChunkData chunkData,
                        out int tileIndex))
                {
                    continue;
                }

                int finalTileRunId = chunkData.RestoreTileSingleUnchecked(tileIndex);
                globalPositions.Add(globalPosition);
                tileRunIds.Add(finalTileRunId);
            }

            return TileValueShape.CreateValued(globalPositions, tileRunIds);
        }

        /// <summary>
        /// 按 shape 彻底删除。
        /// </summary>
        internal static TileValueShape DeleteTiles(PixelShape shape, IShapeChunkSlice chunkSlice, MapElementSize chunkSize)
        {
            List<Vector2I> globalPositions = [];

            // 逐点映射并原子删除：Delete 不携带 values。
            foreach (Vector2I globalPosition in shape.GetGlobalCoordinateIterator())
            {
                if (!TryResolveChunkDataAndTileIndex(
                        globalPosition,
                        chunkSlice,
                        chunkSize,
                        out ChunkData chunkData,
                        out int tileIndex))
                {
                    continue;
                }

                chunkData.DeleteTileSingleUnchecked(tileIndex);
                globalPositions.Add(globalPosition);
            }

            return TileValueShape.CreateCoordinateOnly(globalPositions);
        }


        // ================================================================================
        //                                  内部工具
        // ================================================================================

        /// <summary>
        /// 将单个全局点映射到“二维切片 range 中的 ChunkData + Tile 一维索引”。
        /// <para>这是整个修改流程最关键的一步：</para>
        /// <para>1. 全局点 -> (局部坐标 + 父级区块坐标)；</para>
        /// <para>2. 父级区块坐标 -> 二维数组索引；</para>
        /// <para>3. 索引处为 null 则跳过；</para>
        /// <para>4. 局部坐标 -> tileIndex。</para>
        /// </summary>
        private static bool TryResolveChunkDataAndTileIndex(
            Vector2I globalPosition,
            IShapeChunkSlice chunkSlice,
            MapElementSize chunkSize,
            out ChunkData chunkData,
            out int tileIndex)
        {
            // 步骤 1：把全局坐标拆为“父级区块坐标 + 区块内局部坐标”。
            Vector2I localTilePosition = GlobalTilePositionConverter.ToLocalTilePosition(
                globalPosition,
                chunkSize,
                out ChunkPosition parentChunkPosition);

            // 步骤 2：通过接口按父级区块坐标获取 ChunkData（无数据则返回 null）。
            chunkData = chunkSlice.GetChunkData(parentChunkPosition);
            if (chunkData == null)
            {
                tileIndex = -1;
                return false;
            }

            // 步骤 3：局部坐标转 tileIndex，供 ChunkData 原子方法使用。
            tileIndex = LocalTilePositionConverter.ToTileIndex(localTilePosition, chunkSize);
            return true;
        }

    }
}
