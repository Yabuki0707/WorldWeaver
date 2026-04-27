using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;

namespace WorldWeaver.PixelShapeSystem.PointsShape
{
    /// <summary>
    /// 动态点形状抽象基类。
    /// <para>该类型负责基于可变点列表提供统一的读取接口，并声明具体点形状必须实现的追加接口。</para>
    /// </summary>
    public abstract class DynamicPointsShapeBase : PointsShape
    {
        // ================================================================================
        //                                  核心缓存
        // ================================================================================

        /// <summary>
        /// 当前点形状的核心存储。
        /// </summary>
        protected readonly List<Vector2I> points = new();

        /// <summary>
        /// 当前点形状的最小 X 坐标。
        /// <para>空形状时保持为 <see cref="int.MaxValue"/>。</para>
        /// </summary>
        protected int minX = int.MaxValue;

        /// <summary>
        /// 当前点形状的最大 X 坐标。
        /// <para>空形状时保持为 <see cref="int.MinValue"/>。</para>
        /// </summary>
        protected int maxX = int.MinValue;

        /// <summary>
        /// 当前点形状的最小 Y 坐标。
        /// <para>空形状时保持为 <see cref="int.MaxValue"/>。</para>
        /// </summary>
        protected int minY = int.MaxValue;

        /// <summary>
        /// 当前点形状的最大 Y 坐标。
        /// <para>空形状时保持为 <see cref="int.MinValue"/>。</para>
        /// </summary>
        protected int maxY = int.MinValue;


        // ================================================================================
        //                                  PixelShape基础属性
        // ================================================================================

        /// <summary>
        /// 当前点形状的坐标边界范围。
        /// </summary>
        public override Rect2I CoordinateBounds
        {
            get
            {
                if (points.Count == 0)
                {
                    return PointsShape.EmptyCoordinateBounds;
                }

                return PointsShape.CreateCoordinateBounds(points.Count, minX, maxX, minY, maxY);
            }
        }

        /// <summary>
        /// 当前点形状中的点数量。
        /// </summary>
        public override int PointCount => points.Count;


        // ================================================================================
        //                                  点追加方法
        // ================================================================================

        /// <summary>
        /// 追加一个点。
        /// <para>该方法面向单点追加，不适合大规模批量追加；批量输入应使用 <c>AddPoints</c> 系列方法。</para>
        /// </summary>
        /// <param name="x">点的全局 X 坐标。</param>
        /// <param name="y">点的全局 Y 坐标。</param>
        public void AddPoint(int x, int y)
        {
            AddPoint(new Vector2I(x, y));
        }

        /// <summary>
        /// 追加一个点。
        /// <para>该方法面向单点追加，不适合大规模批量追加；批量输入应使用 <c>AddPoints</c> 系列方法。</para>
        /// </summary>
        /// <param name="point">要追加的全局坐标点。</param>
        public abstract void AddPoint(Vector2I point);

        /// <summary>
        /// 追加一组点。
        /// </summary>
        /// <param name="inputPoints">要追加的点序列。</param>
        public abstract void AddPoints(IEnumerable<Vector2I> inputPoints);

        /// <summary>
        /// 追加一个数组中的所有点。
        /// </summary>
        /// <param name="inputPoints">要追加的点数组。</param>
        public abstract void AddPoints(Vector2I[] inputPoints);

        /// <summary>
        /// 追加一个列表中的所有点。
        /// </summary>
        /// <param name="inputPoints">要追加的点列表。</param>
        public abstract void AddPoints(List<Vector2I> inputPoints);


        // ================================================================================
        //                                  迭代器
        // ================================================================================

        /// <summary>
        /// 按写入顺序迭代输出所有全局坐标。
        /// </summary>
        public override IEnumerable<Vector2I> GetGlobalCoordinateIterator()
        {
            // 当前输出的点索引。
            for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
            {
                yield return points[pointIndex];
            }
        }


        // ================================================================================
        //                                  列表与数组输出
        // ================================================================================

        /// <summary>
        /// 获取当前点形状的全局坐标列表副本。
        /// </summary>
        public override List<Vector2I> GetGlobalCoordinateList()
        {
            return new List<Vector2I>(points);
        }

        /// <summary>
        /// 获取当前点形状的全局坐标数组副本。
        /// </summary>
        public override Vector2I[] GetGlobalCoordinateArray()
        {
            return points.Count == 0 ? Array.Empty<Vector2I>() : points.ToArray();
        }

        /// <summary>
        /// 获取当前点形状的全局坐标只读切片。
        /// <para>该方法直接基于内部 <see cref="List{T}"/> 生成切片，不会额外复制数据。</para>
        /// </summary>
        public override ReadOnlySpan<Vector2I> GetGlobalCoordinateSpan()
        {
            return CollectionsMarshal.AsSpan(points);
        }


    }
}
