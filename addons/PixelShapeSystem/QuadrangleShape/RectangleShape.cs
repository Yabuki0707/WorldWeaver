using System;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.PixelShapeSystem.QuadrangleShape
{
    /// <summary>
    /// 矩形像素形状。
    /// <para>该形状使用左上角原点、宽度与高度实时推导覆盖的所有全局像素坐标。</para>
    /// <para>所有坐标输出都按从上到下、从左到右的顺序生成。</para>
    /// </summary>
    public class RectangleShape : PixelShape, IGeometricPixelShape
    {
        // ================================================================================
        //                                  核心属性
        // ================================================================================

        /// <summary>
        /// 图形原点。
        /// <para>原点即图形起始的左上角坐标。</para>
        /// </summary>
        public Vector2I Origin { get; set; }

        /// <summary>
        /// 矩形宽度。
        /// <para>宽度表示 X 方向包含多少个离散像素点。</para>
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// 矩形高度。
        /// <para>高度表示 Y 方向包含多少个离散像素点。</para>
        /// </summary>
        public int Height { get; }


        // ================================================================================
        //                                  PixelShape基础属性
        // ================================================================================

        /// <summary>
        /// 图形的坐标边界范围。
        /// <para>边界范围基于当前原点与宽高实时计算，不做缓存。</para>
        /// </summary>
        public override Rect2I CoordinateBounds =>
            new(Origin, new Vector2I(Width - 1, Height - 1));

        /// <summary>
        /// 图形当前覆盖的离散点数量。
        /// </summary>
        public override int PointCount => Width * Height;


        // ================================================================================
        //                                  构造方法
        // ================================================================================

        /// <summary>
        /// 创建矩形像素形状。
        /// </summary>
        public RectangleShape(int width, int height, Vector2I origin)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "width 必须大于 0。");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "height 必须大于 0。");
            }

            Width = width;
            Height = height;
            Origin = origin;
        }


        // ================================================================================
        //                                  迭代器
        // ================================================================================

        /// <summary>
        /// 获取当前矩形覆盖点的全局坐标迭代器。
        /// <para>每次迭代都基于当前原点与当前宽高实时计算坐标。</para>
        /// </summary>
        public override IEnumerable<Vector2I> GetGlobalCoordinateIterator()
        {
            for (int offsetY = 0; offsetY < Height; offsetY++)
            {
                for (int offsetX = 0; offsetX < Width; offsetX++)
                {
                    yield return new Vector2I(Origin.X + offsetX, Origin.Y + offsetY);
                }
            }
        }


        // ================================================================================
        //                                  列表
        // ================================================================================

        /// <summary>
        /// 获取当前矩形覆盖点的全局坐标列表。
        /// <para>该列表基于当前原点与当前宽高实时生成。</para>
        /// </summary>
        public override List<Vector2I> GetGlobalCoordinateList()
        {
            List<Vector2I> globalCoordinates = new(PointCount);

            for (int offsetY = 0; offsetY < Height; offsetY++)
            {
                for (int offsetX = 0; offsetX < Width; offsetX++)
                {
                    globalCoordinates.Add(new Vector2I(Origin.X + offsetX, Origin.Y + offsetY));
                }
            }

            return globalCoordinates;
        }


        // ================================================================================
        //                                  数组
        // ================================================================================

        /// <summary>
        /// 获取当前矩形覆盖点的全局坐标数组。
        /// <para>该数组基于当前原点与当前宽高实时生成。</para>
        /// </summary>
        public override Vector2I[] GetGlobalCoordinateArray()
        {
            Vector2I[] globalCoordinates = new Vector2I[PointCount];
            int pointIndex = 0;

            for (int offsetY = 0; offsetY < Height; offsetY++)
            {
                for (int offsetX = 0; offsetX < Width; offsetX++)
                {
                    globalCoordinates[pointIndex] = new Vector2I(Origin.X + offsetX, Origin.Y + offsetY);
                    pointIndex++;
                }
            }

            return globalCoordinates;
        }
    }
}
