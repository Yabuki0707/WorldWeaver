using System;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.PixelShapeSystem.PointsShape
{
    /// <summary>
    /// 静态点形状抽象基类。
    /// <para>该类型负责基于不变点数组提供统一的读取接口。</para>
    /// </summary>
    public abstract class StaticPointsShapeBase : PointsShape
    {
        // ================================================================================
        //                                  核心缓存
        // ================================================================================

        /// <summary>
        /// 构造完成后的点数组。
        /// <para>该数组不会在构造完成后继续变化。</para>
        /// </summary>
        protected Vector2I[] points = Array.Empty<Vector2I>();

        /// <summary>
        /// 构造完成后的坐标边界。
        /// </summary>
        protected Rect2I coordinateBounds = PointsShape.EmptyCoordinateBounds;


        // ================================================================================
        //                                  PixelShape基础属性
        // ================================================================================

        /// <summary>
        /// 当前点形状的坐标边界范围。
        /// </summary>
        public override Rect2I CoordinateBounds => coordinateBounds;

        /// <summary>
        /// 当前点形状中的点数量。
        /// </summary>
        public override int PointCount => points.Length;


        // ================================================================================
        //                                  迭代器
        // ================================================================================

        /// <summary>
        /// 按内部稳定顺序迭代输出所有全局坐标。
        /// </summary>
        public override IEnumerable<Vector2I> GetGlobalCoordinateIterator()
        {
            // 当前输出的点索引。
            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
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
            if (points.Length == 0)
            {
                return Array.Empty<Vector2I>();
            }

            Vector2I[] globalCoordinates = new Vector2I[points.Length];
            Array.Copy(points, globalCoordinates, points.Length);
            return globalCoordinates;
        }

        /// <summary>
        /// 获取当前点形状的全局坐标只读切片。
        /// <para>该方法直接暴露内部数组视图，不会额外复制数组。</para>
        /// </summary>
        public override ReadOnlySpan<Vector2I> GetGlobalCoordinateSpan()
        {
            return points;
        }


    }
}
