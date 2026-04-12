using System;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.PixelShapeSystem.PointsShape
{
    /// <summary>
    /// 动态离散点集合形状。
    /// <para>该类型支持在构造后继续追加点，并在读取阶段按需将新增点归并到稳定缓存中。</para>
    /// <para>重复点会在写入阶段被拒绝，对外始终保持“点集合”语义。</para>
    /// </summary>
    public sealed class MutablePointSetShape : PixelShape
    {
        // ================================================================================
        //                                  核心缓存
        // ================================================================================

        /// <summary>
        /// 尚未归并到稳定缓存中的新增点。
        /// </summary>
        private readonly List<Vector2I> _pendingPoints;

        /// <summary>
        /// 已接受点的键集合，用于在写入阶段快速判重。
        /// </summary>
        private readonly HashSet<long> _normalizedPointKeys;

        /// <summary>
        /// 已归并完成的稳定点数组缓存。
        /// </summary>
        private Vector2I[] _pointsCache;

        /// <summary>
        /// 已归并完成的坐标边界范围缓存。
        /// </summary>
        private Rect2I _coordinateBoundsCache;

        /// <summary>
        /// 当前是否存在尚未归并到稳定缓存的新增点。
        /// </summary>
        private bool _isDirty;


        // ================================================================================
        //                                  构造方法
        // ================================================================================

        /// <summary>
        /// 创建一个空的动态点集合形状。
        /// </summary>
        public MutablePointSetShape()
        {
            _pendingPoints = new List<Vector2I>();
            _normalizedPointKeys = new HashSet<long>();
            _pointsCache = Array.Empty<Vector2I>();
            _coordinateBoundsCache = new Rect2I(Vector2I.Zero, Vector2I.Zero);
            _isDirty = false;
        }

        /// <summary>
        /// 使用一组全局坐标初始化动态点集合形状。
        /// <para>初始化过程与后续追加一样，都会在写入阶段执行去重。</para>
        /// </summary>
        /// <param name="points">输入点序列。</param>
        public MutablePointSetShape(IEnumerable<Vector2I> points) : this()
        {
            AddPoints(points);
        }

        /// <summary>
        /// 使用一组全局坐标初始化动态点集合形状。
        /// </summary>
        /// <param name="points">输入点数组。</param>
        public MutablePointSetShape(params Vector2I[] points) : this((IEnumerable<Vector2I>)points)
        {
        }


        // ================================================================================
        //                                  点追加方法
        // ================================================================================

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
        /// <para>若该点已存在，则会输出警告并跳过。</para>
        /// </summary>
        /// <param name="point">要追加的全局坐标点。</param>
        public void AddPoint(Vector2I point)
        {
            if (TryAppendUniquePoint(point))
            {
                _isDirty = true;
            }
        }

        /// <summary>
        /// 追加一组点。
        /// <para>输入中的重复点会被逐个跳过，其余点会进入待归并列表。</para>
        /// </summary>
        /// <param name="points">要追加的点序列。</param>
        public void AddPoints(IEnumerable<Vector2I> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            // 标记本次追加过程中是否接受过至少一个新点。
            bool hasAnyAcceptedPoint = false;

            foreach (Vector2I point in points)
            {
                if (TryAppendUniquePoint(point))
                {
                    hasAnyAcceptedPoint = true;
                }
            }

            if (hasAnyAcceptedPoint)
            {
                _isDirty = true;
            }
        }

        /// <summary>
        /// 追加一个列表中的所有点。
        /// <para>该重载直接使用索引遍历 <see cref="List{T}"/>，避免额外的接口枚举开销。</para>
        /// </summary>
        /// <param name="points">要追加的点列表。</param>
        public void AddPoints(List<Vector2I> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            // 标记本次追加过程中是否接受过至少一个新点。
            bool hasAnyAcceptedPoint = false;

            for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
            {
                if (TryAppendUniquePoint(points[pointIndex]))
                {
                    hasAnyAcceptedPoint = true;
                }
            }

            if (hasAnyAcceptedPoint)
            {
                _isDirty = true;
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


        // ================================================================================
        //                                  PixelShape基础属性
        // ================================================================================

        /// <summary>
        /// 当前点集合的坐标边界范围。
        /// <para>访问时会先确保待归并数据已刷新到稳定缓存中。</para>
        /// </summary>
        public override Rect2I CoordinateBounds
        {
            get
            {
                EnsureNormalized();
                return _coordinateBoundsCache;
            }
        }

        /// <summary>
        /// 当前点集合的有效点数量。
        /// <para>访问时会先确保待归并数据已刷新到稳定缓存中。</para>
        /// </summary>
        public override int PointCount
        {
            get
            {
                EnsureNormalized();
                return _pointsCache.Length;
            }
        }


        // ================================================================================
        //                                  迭代器
        // ================================================================================

        /// <summary>
        /// 按稳定顺序迭代输出所有全局坐标。
        /// </summary>
        public override IEnumerable<Vector2I> GetGlobalCoordinateIterator()
        {
            EnsureNormalized();

            foreach (Vector2I point in _pointsCache)
            {
                yield return point;
            }
        }


        // ================================================================================
        //                                  列表与数组输出
        // ================================================================================

        /// <summary>
        /// 获取当前点集合的全局坐标列表副本。
        /// </summary>
        public override List<Vector2I> GetGlobalCoordinateList()
        {
            EnsureNormalized();
            return new List<Vector2I>(_pointsCache);
        }

        /// <summary>
        /// 获取当前点集合的全局坐标数组副本。
        /// </summary>
        public override Vector2I[] GetGlobalCoordinateArray()
        {
            EnsureNormalized();

            if (_pointsCache.Length == 0)
            {
                return Array.Empty<Vector2I>();
            }

            // 返回稳定缓存的数组副本，避免外部直接修改内部状态。
            Vector2I[] globalCoordinates = new Vector2I[_pointsCache.Length];
            Array.Copy(_pointsCache, globalCoordinates, _pointsCache.Length);
            return globalCoordinates;
        }

        /// <summary>
        /// 获取当前点集合的全局坐标只读切片。
        /// <para>该方法直接暴露内部稳定缓存视图，不会额外复制数组。</para>
        /// </summary>
        public ReadOnlySpan<Vector2I> GetGlobalCoordinateSpan()
        {
            EnsureNormalized();
            return _pointsCache;
        }


        // ================================================================================
        //                                  私有方法
        // ================================================================================

        /// <summary>
        /// 将待归并列表中的新增点刷入稳定缓存。
        /// <para>仅在对象被标记为脏时执行，以减少重复读取时的重建成本。</para>
        /// </summary>
        private void EnsureNormalized()
        {
            if (!_isDirty)
            {
                return;
            }

            if (_pendingPoints.Count == 0)
            {
                _isDirty = false;
                return;
            }

            // 标记当前是否已存在稳定缓存。
            bool hasAnyNormalizedPoint = _pointsCache.Length > 0;

            // 当前集合的边界值。
            int minX;
            int maxX;
            int minY;
            int maxY;

            if (hasAnyNormalizedPoint)
            {
                // 若已有稳定缓存，则先用缓存边界作为初值。
                minX = _coordinateBoundsCache.Position.X;
                maxX = _coordinateBoundsCache.Position.X + _coordinateBoundsCache.Size.X;
                minY = _coordinateBoundsCache.Position.Y;
                maxY = _coordinateBoundsCache.Position.Y + _coordinateBoundsCache.Size.Y;

                // 合并旧缓存与待归并点。
                Vector2I[] mergedCache = new Vector2I[_pointsCache.Length + _pendingPoints.Count];
                Array.Copy(_pointsCache, mergedCache, _pointsCache.Length);

                for (int pointIndex = 0; pointIndex < _pendingPoints.Count; pointIndex++)
                {
                    // 当前待归并的点。
                    Vector2I point = _pendingPoints[pointIndex];
                    mergedCache[_pointsCache.Length + pointIndex] = point;

                    // 同步扩展边界范围。
                    minX = Math.Min(minX, point.X);
                    maxX = Math.Max(maxX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxY = Math.Max(maxY, point.Y);
                }

                _pointsCache = mergedCache;
            }
            else
            {
                // 若当前没有稳定缓存，则直接以待归并点构建首份缓存。
                _pointsCache = _pendingPoints.ToArray();

                // 第一个点用于初始化边界。
                Vector2I firstPoint = _pendingPoints[0];
                minX = firstPoint.X;
                maxX = firstPoint.X;
                minY = firstPoint.Y;
                maxY = firstPoint.Y;

                for (int pointIndex = 1; pointIndex < _pendingPoints.Count; pointIndex++)
                {
                    // 当前待归并的点。
                    Vector2I point = _pendingPoints[pointIndex];

                    // 持续扩展边界范围。
                    minX = Math.Min(minX, point.X);
                    maxX = Math.Max(maxX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxY = Math.Max(maxY, point.Y);
                }
            }

            // 刷新稳定缓存后，更新边界并清空待归并列表。
            _coordinateBoundsCache = new Rect2I(new Vector2I(minX, minY), new Vector2I(maxX - minX, maxY - minY));
            _pendingPoints.Clear();
            _isDirty = false;
        }

        /// <summary>
        /// 尝试向集合中追加一个唯一点。
        /// </summary>
        /// <param name="point">待追加的点。</param>
        /// <returns>若该点首次出现并成功加入待归并列表，则返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        private bool TryAppendUniquePoint(Vector2I point)
        {
            if (!_normalizedPointKeys.Add(point.ToKey()))
            {
                GD.PushWarning($"[PixelShapeSystem/MutablePointSetShape]: 重复点写入已被跳过，point={point}。");
                return false;
            }

            _pendingPoints.Add(point);
            return true;
        }
    }
}
