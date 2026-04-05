using System;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.PixelShapeSystem.PointsShape
{
    /// <summary>
    /// 静态离散点集合形状。
    /// <para>该类型只在构造阶段接收点数据，构造完成后不再允许继续追加点。</para>
    /// <para>输入中的重复点会在构造阶段被自动跳过，对外始终保持“点集合”语义。</para>
    /// </summary>
    public sealed class PointSetShape : PixelShape
    {
        /// <summary>
        /// 构造完成后的唯一点数组缓存。
        /// </summary>
        private readonly Vector2I[] _pointsCache;

        /// <summary>
        /// 构造完成后的坐标边界范围缓存。
        /// </summary>
        private readonly Rect2I _coordinateBoundsCache;

        /// <summary>
        /// 创建一个空的静态点集合形状。
        /// </summary>
        public PointSetShape()
        {
            _pointsCache = Array.Empty<Vector2I>();
            _coordinateBoundsCache = new Rect2I(Vector2I.Zero, Vector2I.Zero);
        }

        /// <summary>
        /// 使用一组全局坐标创建静态点集合形状。
        /// <para>构造过程中会去除重复点，并保留首次出现顺序。</para>
        /// </summary>
        /// <param name="points">输入点序列。</param>
        public PointSetShape(IEnumerable<Vector2I> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            (_pointsCache, _coordinateBoundsCache) = BuildUniquePoints(points);
        }

        /// <summary>
        /// 使用一组全局坐标创建静态点集合形状。
        /// </summary>
        /// <param name="points">输入点数组。</param>
        public PointSetShape(params Vector2I[] points) : this((IEnumerable<Vector2I>)points)
        {
        }

        /// <summary>
        /// 该点集合的坐标边界范围。
        /// </summary>
        public override Rect2I CoordinateBounds => _coordinateBoundsCache;

        /// <summary>
        /// 去重后的有效点数量。
        /// </summary>
        public override int PointCount => _pointsCache.Length;

        /// <summary>
        /// 按内部稳定顺序迭代输出所有全局坐标。
        /// </summary>
        public override IEnumerable<Vector2I> GetGlobalCoordinateIterator()
        {
            foreach (Vector2I point in _pointsCache)
            {
                yield return point;
            }
        }

        /// <summary>
        /// 获取当前点集合的全局坐标列表副本。
        /// </summary>
        public override List<Vector2I> GetGlobalCoordinateList()
        {
            return new List<Vector2I>(_pointsCache);
        }

        /// <summary>
        /// 获取当前点集合的全局坐标数组副本。
        /// </summary>
        public override Vector2I[] GetGlobalCoordinateArray()
        {
            if (_pointsCache.Length == 0)
            {
                return Array.Empty<Vector2I>();
            }

            Vector2I[] globalCoordinates = new Vector2I[_pointsCache.Length];
            Array.Copy(_pointsCache, globalCoordinates, _pointsCache.Length);
            return globalCoordinates;
        }

        /// <summary>
        /// 获取当前点集合的全局坐标只读切片。
        /// <para>该方法直接暴露内部缓存视图，不会额外复制数组。</para>
        /// </summary>
        public ReadOnlySpan<Vector2I> GetGlobalCoordinateSpan()
        {
            return _pointsCache;
        }

        /// <summary>
        /// 将输入点序列去重并整理为唯一点数组与坐标边界范围。
        /// </summary>
        /// <param name="points">输入点序列。</param>
        /// <returns>去重后的点数组以及对应的坐标边界范围。</returns>
        private static (Vector2I[] Points, Rect2I CoordinateBounds) BuildUniquePoints(IEnumerable<Vector2I> points)
        {
            HashSet<long> uniquePointKeys = new();
            List<Vector2I> normalizedPoints = new();
            bool hasAnyPoint = false;
            int minX = 0;
            int maxX = 0;
            int minY = 0;
            int maxY = 0;

            foreach (Vector2I point in points)
            {
                if (!uniquePointKeys.Add(point.ToKey()))
                {
                    GD.PushWarning($"[PixelShapeSystem/PointSetShape]: 重复点写入已被跳过，point={point}。");
                    continue;
                }

                normalizedPoints.Add(point);

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
                normalizedPoints.ToArray(),
                new Rect2I(new Vector2I(minX, minY), new Vector2I(maxX - minX, maxY - minY))
            );
        }
    }
}
