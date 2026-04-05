using System;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.PixelShapeSystem.PointsShape
{
    /// <summary>
    /// 静态点列表形状。
    /// <para>该类型在构造阶段接收点序列，并保留输入顺序与重复点，不执行去重校验。</para>
    /// <para>适用于明确需要保留原始点序的场景，而不是“点集合”语义。</para>
    /// </summary>
    public sealed class PointListShape : PixelShape
    {
        /// <summary>
        /// 构造完成后的点数组缓存。
        /// </summary>
        private readonly Vector2I[] _points;

        /// <summary>
        /// 构造完成后的坐标边界范围缓存。
        /// </summary>
        private readonly Rect2I _coordinateBounds;

        /// <summary>
        /// 创建一个空的静态点列表形状。
        /// </summary>
        public PointListShape()
        {
            _points = Array.Empty<Vector2I>();
            _coordinateBounds = new Rect2I(Vector2I.Zero, Vector2I.Zero);
        }

        /// <summary>
        /// 使用一组全局坐标创建静态点列表形状。
        /// <para>构造过程中不会去重，输入顺序与重复点都会被完整保留。</para>
        /// </summary>
        /// <param name="points">输入点序列。</param>
        public PointListShape(IEnumerable<Vector2I> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            (_points, _coordinateBounds) = BuildPoints(points);
        }

        /// <summary>
        /// 使用一组全局坐标创建静态点列表形状。
        /// </summary>
        /// <param name="points">输入点数组。</param>
        public PointListShape(params Vector2I[] points) : this((IEnumerable<Vector2I>)points)
        {
        }

        /// <summary>
        /// 当前点列表的坐标边界范围。
        /// </summary>
        public override Rect2I CoordinateBounds => _coordinateBounds;

        /// <summary>
        /// 当前点列表中的点数量。
        /// <para>该数量包含重复点。</para>
        /// </summary>
        public override int PointCount => _points.Length;

        /// <summary>
        /// 按原始输入顺序迭代输出所有全局坐标。
        /// </summary>
        public override IEnumerable<Vector2I> GetGlobalCoordinateIterator()
        {
            foreach (Vector2I point in _points)
            {
                yield return point;
            }
        }

        /// <summary>
        /// 获取当前点列表的全局坐标列表副本。
        /// </summary>
        public override List<Vector2I> GetGlobalCoordinateList()
        {
            return new List<Vector2I>(_points);
        }

        /// <summary>
        /// 获取当前点列表的全局坐标数组副本。
        /// </summary>
        public override Vector2I[] GetGlobalCoordinateArray()
        {
            if (_points.Length == 0)
            {
                return Array.Empty<Vector2I>();
            }

            Vector2I[] globalCoordinates = new Vector2I[_points.Length];
            Array.Copy(_points, globalCoordinates, _points.Length);
            return globalCoordinates;
        }

        /// <summary>
        /// 获取当前点列表的全局坐标只读切片。
        /// <para>该方法直接暴露内部缓存视图，不会额外复制数组。</para>
        /// </summary>
        public ReadOnlySpan<Vector2I> GetGlobalCoordinateSpan()
        {
            return _points;
        }

        /// <summary>
        /// 将输入点序列整理为点数组，并同步计算坐标边界范围。
        /// </summary>
        /// <param name="points">输入点序列。</param>
        /// <returns>点数组以及对应的坐标边界范围。</returns>
        private static (Vector2I[] Points, Rect2I CoordinateBounds) BuildPoints(IEnumerable<Vector2I> points)
        {
            List<Vector2I> pointList = new();
            bool hasAnyPoint = false;
            int minX = 0;
            int maxX = 0;
            int minY = 0;
            int maxY = 0;

            foreach (Vector2I point in points)
            {
                pointList.Add(point);

                if (!hasAnyPoint)
                {
                    minX = point.X;
                    maxX = point.X;
                    minY = point.Y;
                    maxY = point.Y;
                    hasAnyPoint = true;
                    continue;
                }

                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y);
                maxY = Math.Max(maxY, point.Y);
            }

            if (!hasAnyPoint)
            {
                return (Array.Empty<Vector2I>(), new Rect2I(Vector2I.Zero, Vector2I.Zero));
            }

            return (
                pointList.ToArray(),
                new Rect2I(new Vector2I(minX, minY), new Vector2I(maxX - minX, maxY - minY))
            );
        }
    }
}
