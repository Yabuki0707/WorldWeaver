using System;
using System.Collections.Generic;
using Godot;
using WorldWeaver.PixelShapeSystem;
using WorldWeaver.PixelShapeSystem.PointsShape;

namespace WorldWeaver.MapSystem.TileSystem
{
    /// <summary>
    /// Tile 值形状。
    /// <para>继承自 <see cref="PixelValueShape{T}"/>，表达“全局坐标形状 + 按点序对齐的 TileRunId 数组”。</para>
    /// <para>当该对象用于承载删除或逻辑移除结果时，<see cref="TileRunIds"/> 允许为 <see langword="null"/>，表示只返回变化点，不返回值数组。</para>
    /// </summary>
    public sealed class TileValueShape : PixelValueShape<int>
    {
        /// <summary>
        /// 空的“带值”TileValueShape（空坐标 + 空值数组）。
        /// </summary>
        public static readonly TileValueShape EMPTY_VALUED = new(new PointListShape(), Array.Empty<int>());

        /// <summary>
        /// 空的“仅坐标”TileValueShape（空坐标 + null 值数组）。
        /// </summary>
        public static readonly TileValueShape EMPTY_COORDINATE_ONLY = new(new PointListShape());

        /// <summary>
        /// 与点序严格对齐的 TileRunId 数组。
        /// <para>为 <see langword="null"/> 时表示该对象只描述点，不描述值。</para>
        /// </summary>
        public int[] TileRunIds => Values;

        /// <summary>
        /// 当前对象是否携带 TileRunId 数组。
        /// </summary>
        public bool HasTileRunIds => HasValues;

        /// <summary>
        /// 创建一个 Tile 值形状。
        /// </summary>
        public TileValueShape(PixelShape shape, int[] tileRunIds = null) : base(shape, tileRunIds)
        {
        }

        /// <summary>
        /// 使用现有的像素值形状创建 Tile 值形状。
        /// </summary>
        public TileValueShape(PixelValueShape<int> source) : base(
            source?.Shape ?? throw new System.ArgumentNullException(nameof(source)),
            source.Values)
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
    }
}
