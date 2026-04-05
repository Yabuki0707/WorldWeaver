using System;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.PixelShapeSystem.PointsShape
{
    /// <summary>
    /// 动态点列表形状。
    /// <para>该类型支持在构造后继续追加点，并保留输入顺序与重复点，不执行去重校验。</para>
    /// <para>适用于高性能地收集原始点序，而不要求“点集合”语义的场景。</para>
    /// </summary>
    public sealed class MutablePointListShape : PixelShape
    {
        /// <summary>
        /// 点列表的核心数据容器。
        /// <para>该列表按写入顺序保留所有点，包括重复点。</para>
        /// </summary>
        private readonly List<Vector2I> _points;

        /// <summary>
        /// 创建一个空的动态点列表形状。
        /// </summary>
        public MutablePointListShape()
        {
            _points = new List<Vector2I>();
        }

        /// <summary>
        /// 使用一组全局坐标初始化动态点列表形状。
        /// </summary>
        /// <param name="points">输入点序列。</param>
        public MutablePointListShape(IEnumerable<Vector2I> points) : this()
        {
            AddPoints(points);
        }

        /// <summary>
        /// 使用一组全局坐标初始化动态点列表形状。
        /// </summary>
        /// <param name="points">输入点数组。</param>
        public MutablePointListShape(params Vector2I[] points) : this((IEnumerable<Vector2I>)points)
        {
        }

        /// <summary>
        /// 追加一个点。
        /// </summary>
        /// <param name="x">点的全局 X 坐标。</param>
        /// <param name="y">点的全局 Y 坐标。</param>
        public void AddPoint(int x, int y)
        {
            AddPoint(new Vector2I(x, y));
        }

        /// <summary>
        /// 追加一个点。
        /// <para>该类型不会执行去重，重复点会被原样保留。</para>
        /// </summary>
        /// <param name="point">要追加的全局坐标点。</param>
        public void AddPoint(Vector2I point)
        {
            _points.Add(point);
        }

        /// <summary>
        /// 追加一组点。
        /// </summary>
        /// <param name="points">要追加的点序列。</param>
        public void AddPoints(IEnumerable<Vector2I> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            foreach (Vector2I point in points)
            {
                _points.Add(point);
            }
        }

        /// <summary>
        /// 追加一个列表中的所有点。
        /// <para>该重载直接使用索引遍历 <see cref="List{T}"/>。</para>
        /// </summary>
        /// <param name="points">要追加的点列表。</param>
        public void AddPoints(List<Vector2I> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
            {
                _points.Add(points[pointIndex]);
            }
        }

        /// <summary>
        /// 追加一个数组中的所有点。
        /// </summary>
        /// <param name="points">要追加的点数组。</param>
        public void AddPoints(Vector2I[] points)
        {
            AddPoints((IEnumerable<Vector2I>)points);
        }

        /// <summary>
        /// 当前点列表的坐标边界范围。
        /// <para>每次访问都会基于当前列表实时计算，不额外缓存。</para>
        /// </summary>
        public override Rect2I CoordinateBounds
        {
            get
            {
                if (_points.Count == 0)
                {
                    return new Rect2I(Vector2I.Zero, Vector2I.Zero);
                }

                Vector2I firstPoint = _points[0];
                int minX = firstPoint.X;
                int maxX = firstPoint.X;
                int minY = firstPoint.Y;
                int maxY = firstPoint.Y;

                for (int pointIndex = 1; pointIndex < _points.Count; pointIndex++)
                {
                    Vector2I point = _points[pointIndex];
                    minX = Math.Min(minX, point.X);
                    maxX = Math.Max(maxX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxY = Math.Max(maxY, point.Y);
                }

                return new Rect2I(new Vector2I(minX, minY), new Vector2I(maxX - minX, maxY - minY));
            }
        }

        /// <summary>
        /// 当前点列表中的点数量。
        /// <para>该数量包含重复点。</para>
        /// </summary>
        public override int PointCount => _points.Count;

        /// <summary>
        /// 按写入顺序迭代输出所有全局坐标。
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
            return _points.Count == 0 ? Array.Empty<Vector2I>() : _points.ToArray();
        }

        /// <summary>
        /// 获取当前点列表的全局坐标只读切片。
        /// <para>该方法直接基于内部 <see cref="List{T}"/> 生成切片，不会额外复制数据。</para>
        /// </summary>
        public ReadOnlySpan<Vector2I> GetGlobalCoordinateSpan()
        {
            return System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_points);
        }
    }
}
