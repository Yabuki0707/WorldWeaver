using System;
using Godot;
using WorldWeaver.MapSystem.TileSystem;
using WorldWeaver.PixelShapeSystem;

namespace WorldWeaver.MapSystem.ChunkSystem
{
    /// <summary>
    /// 区块数据操作入口包装类。
    /// <para>该类本身不承载切片或修改细节，仅负责参数校验与流程编排。</para>
    /// <para>切片策略由 <see cref="IShapeChunkSlice"/> 抽象：</para>
    /// <para>1. <see cref="ShapeChunkRange"/>：构造阶段预缓存区块范围；</para>
    /// <para>2. <see cref="ShapeChunkSet"/>：按需直接访问 chunk manager。</para>
    /// </summary>
    public sealed class ChunkDataOperator
    {
        /// <summary>
        /// 大 shape 使用 ShapeChunkSet 的面积阈值倍数。
        /// </summary>
        private const int LARGE_SHAPE_THRESHOLD_MULTIPLIER = 1024;

        /// <summary>
        /// 所属 chunk 管理器。
        /// </summary>
        private readonly ChunkManager _owner;

        internal ChunkDataOperator(ChunkManager owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>
        /// 按全局 shape 读取已加载区块内命中的 Tile。
        /// </summary>
        public TileValueShape GetTiles(PixelShape shape)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            if (shape.PointCount == 0)
            {
                return TileValueShape.EMPTY_VALUED;
            }

            IShapeChunkSlice chunkSlice = CreateChunkSlice(shape);
            return ShapeChunkModifier.GetTiles(shape, chunkSlice, _owner.OwnerLayer.ChunkSize);
        }

        /// <summary>
        /// 使用统一 TileRunId 对全局 shape 执行设置。
        /// </summary>
        public TileValueShape SetTiles(TileRegion tileRegion)
        {
            if (tileRegion == null)
            {
                throw new ArgumentNullException(nameof(tileRegion));
            }

            if (tileRegion.Shape.PointCount == 0)
            {
                return TileValueShape.EMPTY_VALUED;
            }

            IShapeChunkSlice chunkSlice = CreateChunkSlice(tileRegion.Shape);
            TileValueShape totalResult = ShapeChunkModifier.SetTiles(tileRegion, chunkSlice, _owner.OwnerLayer.ChunkSize);
            NotifyTilesChanged(totalResult, TileChangeType.Set);
            return totalResult;
        }

        /// <summary>
        /// 使用逐点值 shape 执行设置。
        /// </summary>
        public TileValueShape SetTiles(TileValueShape tileValueShape)
        {
            if (tileValueShape == null)
            {
                throw new ArgumentNullException(nameof(tileValueShape));
            }

            if (!tileValueShape.HasTileRunIds || tileValueShape.IsInvalid)
            {
                GD.PushError("[ChunkSystem/ChunkDataOperator]: SetTiles(TileValueShape) 调用失败，输入对象必须携带与点序一致的 TileRunId 数组。");
                return TileValueShape.EMPTY_VALUED;
            }

            if (tileValueShape.Shape.PointCount == 0)
            {
                return TileValueShape.EMPTY_VALUED;
            }

            IShapeChunkSlice chunkSlice = CreateChunkSlice(tileValueShape.Shape);
            TileValueShape totalResult = ShapeChunkModifier.SetTiles(tileValueShape, chunkSlice, _owner.OwnerLayer.ChunkSize);
            NotifyTilesChanged(totalResult, TileChangeType.Set);
            return totalResult;
        }

        /// <summary>
        /// 按全局 shape 逻辑移除 Tile。
        /// </summary>
        public TileValueShape RemoveTiles(PixelShape shape)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            if (shape.PointCount == 0)
            {
                return TileValueShape.EMPTY_COORDINATE_ONLY;
            }

            IShapeChunkSlice chunkSlice = CreateChunkSlice(shape);
            TileValueShape totalResult = ShapeChunkModifier.RemoveTiles(shape, chunkSlice, _owner.OwnerLayer.ChunkSize);
            NotifyTilesChanged(totalResult, TileChangeType.Remove);
            return totalResult;
        }

        /// <summary>
        /// 按全局 shape 恢复 Tile。
        /// </summary>
        public TileValueShape RestoreTiles(PixelShape shape)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            if (shape.PointCount == 0)
            {
                return TileValueShape.EMPTY_VALUED;
            }

            IShapeChunkSlice chunkSlice = CreateChunkSlice(shape);
            TileValueShape totalResult = ShapeChunkModifier.RestoreTiles(shape, chunkSlice, _owner.OwnerLayer.ChunkSize);
            NotifyTilesChanged(totalResult, TileChangeType.Restore);
            return totalResult;
        }

        /// <summary>
        /// 按全局 shape 彻底删除 Tile。
        /// </summary>
        public TileValueShape DeleteTiles(PixelShape shape)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            if (shape.PointCount == 0)
            {
                return TileValueShape.EMPTY_COORDINATE_ONLY;
            }

            IShapeChunkSlice chunkSlice = CreateChunkSlice(shape);
            TileValueShape totalResult = ShapeChunkModifier.DeleteTiles(shape, chunkSlice, _owner.OwnerLayer.ChunkSize);
            NotifyTilesChanged(totalResult, TileChangeType.Delete);
            return totalResult;
        }

        // ================================================================================
        //                                  内部方法
        // ================================================================================

        /// <summary>
        /// 根据 shape 面积选择切片策略。
        /// <para>当 <c>shape.BoundingBox.GetArea() &gt; ChunkSize.Area * 1024</c> 时使用 <see cref="ShapeChunkSet"/>。</para>
        /// </summary>
        private IShapeChunkSlice CreateChunkSlice(PixelShape shape)
        {
            long shapeArea = shape.BoundingBox.Area;
            long largeShapeThreshold = (long)_owner.OwnerLayer.ChunkSize.Area * LARGE_SHAPE_THRESHOLD_MULTIPLIER;
            return shapeArea > largeShapeThreshold
                ? new ShapeChunkSet(_owner)
                : new ShapeChunkRange(_owner, shape);
        }

        /// <summary>
        /// 将修改器返回的总结果转发到 ChunkManager 事件总线。
        /// <para>空形状不会触发事件。</para>
        /// </summary>
        private void NotifyTilesChanged(TileValueShape tileValueShape, TileChangeType changeType)
        {
            if (tileValueShape == null || tileValueShape.Shape.PointCount == 0)
            {
                return;
            }

            _owner.OnTilesChanged(new TilesChangedEventArgs(tileValueShape, changeType));
        }
    }
}
