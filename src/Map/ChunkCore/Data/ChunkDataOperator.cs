using System;
using Godot;
using WorldWeaver.Map.TileCore;
using WorldWeaver.PixelShapeSystem;
using WorldWeaver.PixelShapeSystem.PointsShape;
using WorldWeaver.PixelShapeSystem.ValueShape;

namespace WorldWeaver.Map.ChunkCore.Data
{
    /// <summary>
    /// 区块数据操作入口包装类。
    /// <para>该类不直接执行 Tile 修改细节，而是负责三件事：参数校验、流程编排、以及区块读取策略决策。</para>
    /// <para>也就是说，面对一个 shape 时，应该使用“缓存区块范围读取”还是“实时区块读取”，由本类统一判断并创建策略实例。</para>
    /// </summary>
    public sealed class ChunkDataOperator
    {
        // ================================================================================
        //                                  常量
        // ================================================================================

        /// <summary>
        /// 允许使用缓存区块范围读取器的最大区块范围面积。
        /// </summary>
        private const int MAX_CACHED_CHUNK_RANGE_AREA = 256;

        /// <summary>
        /// 空的带值结果。
        /// </summary>
        private static readonly TileValueShape _emptyValued = new(new PixelValueArrayShape<PointSequenceShape, int>(new PointSequenceShape(), Array.Empty<int>()));

        /// <summary>
        /// 空的仅坐标结果。
        /// </summary>
        private static readonly TileValueShape _emptyCoordinateOnly = new(new PixelValueArrayShape<PointSequenceShape, int>(new PointSequenceShape(), null));

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
                return _emptyValued;
            }

            IShapeChunkReadStrategy readStrategy = CreateChunkReadStrategy(shape);
            return ShapeChunkModifier.GetTiles(shape, readStrategy, _owner.OwnerLayer.ChunkSize);
        }

        /// <summary>
        /// 使用值 shape 执行设置。统一值、数组值、列表值均通过 <see cref="TileValueShape"/> 索引器处理。
        /// </summary>
        public TileValueShape SetTiles(TileValueShape tileValueShape)
        {
            if (tileValueShape == null)
            {
                throw new ArgumentNullException(nameof(tileValueShape));
            }

            if (!tileValueShape.ValueShape.HasValues || !tileValueShape.ValueShape.IsAligned)
            {
                GD.PushError("[ChunkSystem/ChunkDataOperator]: SetTiles 调用失败，输入对象必须携带与点序一致的 TileRunId 值。");
                return _emptyValued;
            }

            if (tileValueShape.ValueShape.Shape.PointCount == 0)
            {
                return _emptyValued;
            }

            IShapeChunkReadStrategy readStrategy = CreateChunkReadStrategy(tileValueShape.ValueShape.Shape);
            TileValueShape totalResult = ShapeChunkModifier.SetTiles(tileValueShape, readStrategy, _owner.OwnerLayer.ChunkSize);
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
                return _emptyCoordinateOnly;
            }

            IShapeChunkReadStrategy readStrategy = CreateChunkReadStrategy(shape);
            TileValueShape totalResult = ShapeChunkModifier.RemoveTiles(shape, readStrategy, _owner.OwnerLayer.ChunkSize);
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
                return _emptyValued;
            }

            IShapeChunkReadStrategy readStrategy = CreateChunkReadStrategy(shape);
            TileValueShape totalResult = ShapeChunkModifier.RestoreTiles(shape, readStrategy, _owner.OwnerLayer.ChunkSize);
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
                return _emptyCoordinateOnly;
            }

            IShapeChunkReadStrategy readStrategy = CreateChunkReadStrategy(shape);
            TileValueShape totalResult = ShapeChunkModifier.DeleteTiles(shape, readStrategy, _owner.OwnerLayer.ChunkSize);
            NotifyTilesChanged(totalResult, TileChangeType.Delete);
            return totalResult;
        }

        // ================================================================================
        //                                  策略决策方法
        // ================================================================================

        /// <summary>
        /// 根据 shape 的更新点数量与区块范围面积选择区块读取策略。
        /// <para>当前规则如下：</para>
        /// <para>1. 先根据 shape 边界计算最小/最大区块坐标；</para>
        /// <para>2. 再计算区块范围面积 <c>chunkRangeArea</c>；</para>
        /// <para>3. 当 <c>updateTileCount * 2 &gt; chunkRangeArea</c> 且区块范围面积不超过缓存阈值时，选缓存读取策略；否则选实时读取策略。</para>
        /// </summary>
        private IShapeChunkReadStrategy CreateChunkReadStrategy(PixelShape shape)
        {
            // 先根据 shape 边界推导区块坐标范围。
            (ChunkPosition minChunkPosition, ChunkPosition maxChunkPosition) = GetChunkRange(shape);

            // 统计本次更新涉及的点数量。
            int updateTileCount = shape.PointCount;

            // 统计区块范围面积，等价于需要覆盖的区块数量。
            int chunkRangeArea = GetChunkRangeArea(minChunkPosition, maxChunkPosition);

            // 当更新点相对密集且区块范围不大时，预缓存区块范围更划算。
            if (chunkRangeArea <= MAX_CACHED_CHUNK_RANGE_AREA && (long)updateTileCount * 2 > chunkRangeArea)
            {
                return new CachedChunkRangeReader(_owner, minChunkPosition, maxChunkPosition);
            }

            return new LiveChunkReader(_owner);
        }


        // ================================================================================
        //                                  区块范围计算方法
        // ================================================================================

        /// <summary>
        /// 根据 shape 边界计算最小/最大区块坐标。
        /// <para>这里返回的是闭区间范围，也就是最小端点和最大端点都属于有效区块范围。</para>
        /// </summary>
        private (ChunkPosition MinChunkPosition, ChunkPosition MaxChunkPosition) GetChunkRange(PixelShape shape)
        {
            // shape 坐标边界用于推导本次访问覆盖到哪些父级区块。
            Rect2I coordinateBounds = shape.CoordinateBounds;

            // 最小边界点映射到最小区块坐标。
            ChunkPosition minChunkPosition =
                GlobalTilePositionConverter.ToChunkPosition(coordinateBounds.Position, _owner.OwnerLayer.ChunkSize);

            // 最大边界点映射到最大区块坐标。
            ChunkPosition maxChunkPosition =
                GlobalTilePositionConverter.ToChunkPosition(
                    coordinateBounds.Position + coordinateBounds.Size,
                    _owner.OwnerLayer.ChunkSize);

            return (minChunkPosition, maxChunkPosition);
        }

        /// <summary>
        /// 根据区块坐标范围计算区块范围面积。
        /// <para>当前区块范围是闭区间语义，因此宽高都必须使用 <c>max - min + 1</c>。</para>
        /// </summary>
        private static int GetChunkRangeArea(ChunkPosition minChunkPosition, ChunkPosition maxChunkPosition)
        {
            // 区块范围宽度包含最小/最大端点（闭区间），因此需要 +1。
            int width = maxChunkPosition.X - minChunkPosition.X + 1;

            // 区块范围高度同样包含最小/最大端点（闭区间），因此需要 +1。
            int height = maxChunkPosition.Y - minChunkPosition.Y + 1;

            return width * height;
        }

        // ================================================================================
        //                                  事件转发方法
        // ================================================================================

        /// <summary>
        /// 将修改器返回的总结果转发到 ChunkManager 事件总线。
        /// <para>空形状不会触发事件。</para>
        /// </summary>
        private void NotifyTilesChanged(TileValueShape tileValueShape, TileChangeType changeType)
        {
            if (tileValueShape == null || tileValueShape.ValueShape.Shape.PointCount == 0)
            {
                return;
            }

            _owner.OnTilesChanged(new TilesChangedEventArgs(tileValueShape, changeType));
        }
    }
}
