using System;
using System.Collections.Generic;
using Godot;
using WorldWeaver.PixelShapeSystem;
using WorldWeaver.PixelShapeSystem.PointsShape;
using WorldWeaver.PixelShapeSystem.ValueShape;

namespace WorldWeaver.MapSystem.TileSystem
{
    /// <summary>
    /// Tile 值形状。
    /// <para>继承自 <see cref="PixelValuesArrayShape{T}"/>，表达“全局坐标形状 + 按点序对齐的 TileRunId 数组”。</para>
    /// <para>当该对象用于承载删除或逻辑移除结果时，<see cref="TileRunIds"/> 允许为 <see langword="null"/>，表示只返回变化点，不返回值数组。</para>
    /// </summary>
    public sealed class TileValuesArrayShape : PixelValuesArrayShape<int>
    {
        /// <summary>
        /// 空的“带值”TileValueShape（空坐标 + 空值数组）。
        /// </summary>
        public static readonly TileValuesArrayShape EMPTY_VALUED = new(new PointListShape(), Array.Empty<int>());

        /// <summary>
        /// 空的“仅坐标”TileValueShape（空坐标 + null 值数组）。
        /// </summary>
        public static readonly TileValuesArrayShape EMPTY_COORDINATE_ONLY = new(new PointListShape());

        /// <summary>
        /// 与点序严格对齐的 TileRunId 数组。
        /// <para>为 <see langword="null"/> 时表示该对象只描述点，不描述值。</para>
        /// </summary>
        public int[] TileRunIds => Values;

        /// <summary>
        /// 当前对象是否携带 TileRunId 数组。
        /// </summary>
        public bool HasTileRunIds => HasValues();

        /// <summary>
        /// 使用全局坐标列表与对应值列表构造一个带值的 TileValueShape。
        /// </summary>
        public static TileValuesArrayShape CreateValued(List<Vector2I> globalPositions, List<int> tileRunIds)
        {
            if (globalPositions == null)
            {
                throw new ArgumentNullException(nameof(globalPositions));
            }

            if (tileRunIds == null)
            {
                throw new ArgumentNullException(nameof(tileRunIds));
            }

            if (globalPositions.Count == 0)
            {
                return EMPTY_VALUED;
            }

            return new TileValuesArrayShape(new PointListShape(globalPositions), tileRunIds.ToArray());
        }

        /// <summary>
        /// 使用全局坐标列表构造一个仅坐标的 TileValueShape。
        /// </summary>
        public static TileValuesArrayShape CreateCoordinateOnly(List<Vector2I> globalPositions)
        {
            if (globalPositions == null)
            {
                throw new ArgumentNullException(nameof(globalPositions));
            }

            if (globalPositions.Count == 0)
            {
                return EMPTY_COORDINATE_ONLY;
            }

            return new TileValuesArrayShape(new PointListShape(globalPositions));
        }

        /// <summary>
        /// 创建一个 Tile 值形状。
        /// </summary>
        public TileValuesArrayShape(PixelShape shape, int[] tileRunIds = null) : base(shape, tileRunIds)
        {
        }

        /// <summary>
        /// 使用现有的像素值形状创建 Tile 值形状。
        /// </summary>
        public TileValuesArrayShape(IPixelValuesShape<int> source) : base(
            source?.Shape ?? throw new ArgumentNullException(nameof(source)),
            CreateTileRunIdArray(source))
        {
        }

        /// <summary>
        /// 获取全局坐标与 TileRunId 的配对迭代器。
        /// <para>若当前对象未携带 TileRunId 数组，将输出错误并终止迭代。</para>
        /// </summary>
        public new IEnumerable<(Vector2I GlobalPosition, int TileRunId)> GetGlobalValueIterator()
        {
            return base.GetGlobalValueIterator();
        }

        /// <summary>
        /// 获取全局坐标与 TileRunId 索引的配对迭代器。
        /// </summary>
        public new IEnumerable<(Vector2I GlobalPosition, int ValueIndex)> GetGlobalValueIndexIterator()
        {
            return base.GetGlobalValueIndexIterator();
        }

        /// <summary>
        /// 将任意像素值形状中的值复制为 TileRunId 数组。
        /// </summary>
        private static int[] CreateTileRunIdArray(IPixelValuesShape<int> source)
        {
            if (!source.HasValues())
            {
                return null;
            }

            if (!source.IsAligned())
            {
                throw new ArgumentException("source 的点数量与值数量不一致。", nameof(source));
            }

            int[] tileRunIds = new int[source.ValueCount];
            int valueIndex = 0;
            foreach ((Vector2I _, int tileRunId) in source.GetGlobalValueIterator())
            {
                tileRunIds[valueIndex] = tileRunId;
                valueIndex++;
            }

            return tileRunIds;
        }
    }
}
