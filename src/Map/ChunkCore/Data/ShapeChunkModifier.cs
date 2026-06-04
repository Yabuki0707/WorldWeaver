using Godot;
using WorldWeaver.Map.TileCore;
using WorldWeaver.PixelShapeSystem;
using WorldWeaver.PixelShapeSystem.ValueShape;

namespace WorldWeaver.Map.ChunkCore.Data
{
    /// <summary>
    /// Shape 修改器（静态）。
    /// <para>职责：对外部提供的区块读取策略执行逐点映射与原子操作。</para>
    /// <para>注意：本类只负责“执行修改/读取”，不负责判断应该使用哪种区块读取策略。</para>
    /// <para>策略判断与创建统一由 <see cref="ChunkDataOperator"/> 负责。</para>
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
        internal static TileValueShape GetTiles(PixelShape shape, IShapeChunkReadStrategy readStrategy, MapElementSize chunkSize)
        {
            TileValueShape resultShape = CreateResultTileValueShape(shape);

            // 使用坐标与值索引迭代器原地写回，未命中项默认保持 0。
            int valueIndex = -1;
            foreach (Vector2I globalPosition in resultShape.ValueShape.Shape.GetGlobalCoordinateIterator())
            {
                valueIndex++;
                if (!TryResolveChunkDataAndTileIndex(
                        globalPosition,
                        readStrategy,
                        chunkSize,
                        out ChunkData chunkData,
                        out int tileIndex))
                {
                    continue;
                }

                resultShape[valueIndex] = chunkData.GetTileSingleUnchecked(tileIndex);
            }

            return resultShape;
        }

        /// <summary>
        /// 按值 shape 执行设置。
        /// <para>统一值、数组值、列表值均通过 <see cref="TileValueShape"/> 索引器统一读取。</para>
        /// </summary>
        internal static TileValueShape SetTiles(TileValueShape tileValueShape, IShapeChunkReadStrategy readStrategy, MapElementSize chunkSize)
        {
            TileValueShape resultShape = CreateResultTileValueShape(tileValueShape.ValueShape.Shape);

            int valueIndex = -1;
            foreach (Vector2I globalPosition in resultShape.ValueShape.Shape.GetGlobalCoordinateIterator())
            {
                valueIndex++;
                if (!TryResolveChunkDataAndTileIndex(
                        globalPosition,
                        readStrategy,
                        chunkSize,
                        out ChunkData chunkData,
                        out int tileIndex))
                {
                    continue;
                }

                resultShape[valueIndex] = chunkData.SetTileSingleUnchecked(tileIndex, tileValueShape[valueIndex]);
            }

            return resultShape;
        }

        /// <summary>
        /// 按 shape 逻辑移除。
        /// </summary>
        internal static TileValueShape RemoveTiles(PixelShape shape, IShapeChunkReadStrategy readStrategy, MapElementSize chunkSize)
        {
            TileValueShape resultShape = CreateResultTileValueShape(shape);

            // 逐点映射并原子移除：未命中项保留 0，命中项记录移除后的最终值。
            int valueIndex = -1;
            foreach (Vector2I globalPosition in resultShape.ValueShape.Shape.GetGlobalCoordinateIterator())
            {
                valueIndex++;
                if (!TryResolveChunkDataAndTileIndex(
                        globalPosition,
                        readStrategy,
                        chunkSize,
                        out ChunkData chunkData,
                        out int tileIndex))
                {
                    continue;
                }

                resultShape[valueIndex] = chunkData.RemoveTileSingleUnchecked(tileIndex);
            }

            return resultShape;
        }

        /// <summary>
        /// 按 shape 恢复。
        /// </summary>
        internal static TileValueShape RestoreTiles(PixelShape shape, IShapeChunkReadStrategy readStrategy, MapElementSize chunkSize)
        {
            TileValueShape resultShape = CreateResultTileValueShape(shape);

            // 逐点映射并原子恢复：未命中项保留 0，命中项记录恢复后的最终值。
            int valueIndex = -1;
            foreach (Vector2I globalPosition in resultShape.ValueShape.Shape.GetGlobalCoordinateIterator())
            {
                valueIndex++;
                if (!TryResolveChunkDataAndTileIndex(
                        globalPosition,
                        readStrategy,
                        chunkSize,
                        out ChunkData chunkData,
                        out int tileIndex))
                {
                    continue;
                }

                resultShape[valueIndex] = chunkData.RestoreTileSingleUnchecked(tileIndex);
            }

            return resultShape;
        }

        /// <summary>
        /// 按 shape 彻底删除。
        /// </summary>
        internal static TileValueShape DeleteTiles(PixelShape shape, IShapeChunkReadStrategy readStrategy, MapElementSize chunkSize)
        {
            TileValueShape resultShape = CreateResultTileValueShape(shape);

            // 逐点映射并原子删除：未命中项保留 0，命中项记录删除后的最终值。
            int valueIndex = -1;
            foreach (Vector2I globalPosition in resultShape.ValueShape.Shape.GetGlobalCoordinateIterator())
            {
                valueIndex++;
                if (!TryResolveChunkDataAndTileIndex(
                        globalPosition,
                        readStrategy,
                        chunkSize,
                        out ChunkData chunkData,
                        out int tileIndex))
                {
                    continue;
                }

                resultShape[valueIndex] = chunkData.DeleteTileSingleUnchecked(tileIndex);
            }

            return resultShape;
        }


        // ================================================================================
        //                                  内部工具
        // ================================================================================

        /// <summary>
        /// 根据输入 shape 创建同点序的结果 TileValueShape。
        /// <para>结果数组默认以 0 填充，表示未命中或禁用渲染。</para>
        /// </summary>
        private static TileValueShape CreateResultTileValueShape(PixelShape shape)
        {
            return new TileValueShape(new PixelValueArrayShape<PixelShape, int>(shape, new int[shape.PointCount]));
        }

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
            IShapeChunkReadStrategy readStrategy,
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
            chunkData = readStrategy.GetChunkData(parentChunkPosition);
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
