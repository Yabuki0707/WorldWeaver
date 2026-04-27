using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;

namespace WorldWeaver.PixelShapeSystem.PointsShape
{
    /// <summary>
    /// 动态点序列形状。
    /// <para>该类型支持在构造后继续追加点，并保留输入顺序与重复点，不执行去重校验。</para>
    /// <para>适用于高性能地收集原始点序，而不要求“点集合”语义的场景。</para>
    /// </summary>
    public sealed class MutablePointSequenceShape : DynamicPointsShapeBase
    {
        // ================================================================================
        //                                  构造方法
        // ================================================================================

        /// <summary>
        /// 创建一个空的动态点序列形状。
        /// </summary>
        public MutablePointSequenceShape()
        {
        }

        /// <summary>
        /// 使用一组全局坐标初始化动态点序列形状。
        /// </summary>
        /// <param name="inputPoints">输入点序列。</param>
        public MutablePointSequenceShape(IEnumerable<Vector2I> inputPoints)
        {
            AddPoints(inputPoints);
        }

        /// <summary>
        /// 使用一组全局坐标初始化动态点序列形状。
        /// </summary>
        /// <param name="inputPoints">输入点数组。</param>
        public MutablePointSequenceShape(params Vector2I[] inputPoints)
        {
            AddPoints(inputPoints);
        }

        /// <summary>
        /// 使用一组全局坐标初始化动态点序列形状。
        /// </summary>
        /// <param name="inputPoints">输入点列表。</param>
        public MutablePointSequenceShape(List<Vector2I> inputPoints)
        {
            AddPoints(inputPoints);
        }


        // ================================================================================
        //                                  点追加方法
        // ================================================================================

        /// <summary>
        /// 追加一个点。
        /// <para>该方法面向单点追加，不适合大规模批量追加；批量输入应使用 <c>AddPoints</c> 系列方法。</para>
        /// </summary>
        /// <param name="point">要追加的全局坐标点。</param>
        public override void AddPoint(Vector2I point)
        {
            points.Add(point);
            PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
        }

        /// <summary>
        /// 追加一组点。
        /// <para>该方法会按输入的具体集合类型选择遍历策略，不会调用 <see cref="AddPoint(Vector2I)"/>。</para>
        /// </summary>
        /// <param name="inputPoints">要追加的点序列。</param>
        public override void AddPoints(IEnumerable<Vector2I> inputPoints)
        {
            ArgumentNullException.ThrowIfNull(inputPoints);

            // 数组和列表有更明确的遍历策略，优先进入专用追加路径。
            if (inputPoints is Vector2I[] pointArray)
            {
                AddPoints(pointArray);
                return;
            }

            // List<T> 参数可以直接转为 Span，避免反复走 List<T> 索引器。
            if (inputPoints is List<Vector2I> pointListSource)
            {
                AddPoints(pointListSource);
                return;
            }

            // 可计数集合可以预扩展目标列表，再通过目标 Span 直接写入新增区域。
            if (inputPoints is ICollection<Vector2I> pointCollection)
            {
                int writeIndex = points.Count;
                int targetPointCount = points.Count + pointCollection.Count;
                // 已知输入规模时预扩展列表，随后通过 Span 直接写入目标区域。
                points.EnsureCapacity(targetPointCount);
                CollectionsMarshal.SetCount(points, targetPointCount);
                // 目标列表已通过 SetCount 固定长度，转为 Span 写入可避免反复走 List<T> 索引器。
                Span<Vector2I> targetPoints = CollectionsMarshal.AsSpan(points);

                // IList<T> 支持稳定索引读取；源端不保证连续内存，因此不转 Span。
                if (inputPoints is IList<Vector2I> pointList)
                {
                    int pointCount = pointList.Count;
                    for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
                    {
                        Vector2I point = pointList[pointIndex];
                        // 序列形状保留原始输入顺序与重复点。
                        targetPoints[writeIndex] = point;
                        writeIndex++;
                        PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
                    }
                }
                else
                {
                    foreach (Vector2I point in inputPoints)
                    {
                        // 序列形状保留原始输入顺序与重复点。
                        targetPoints[writeIndex] = point;
                        writeIndex++;
                        PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
                    }
                }

                // 防御性处理：某些 ICollection 的 Count 可能大于实际枚举数。
                if (writeIndex < targetPointCount)
                {
                    CollectionsMarshal.SetCount(points, writeIndex);
                }

                return;
            }

            // 无法提前知道规模时逐个追加；仍然不调用 AddPoint，避免批量路径走单点接口。
            foreach (Vector2I point in inputPoints)
            {
                // 序列形状保留原始输入顺序与重复点。
                points.Add(point);
                PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
            }
        }

        /// <summary>
        /// 追加一个数组中的所有点。
        /// <para>该方法不会调用 <see cref="AddPoint(Vector2I)"/>。</para>
        /// </summary>
        /// <param name="inputPoints">要追加的点数组。</param>
        public override void AddPoints(Vector2I[] inputPoints)
        {
            ArgumentNullException.ThrowIfNull(inputPoints);

            int pointCount = inputPoints.Length;
            int writeIndex = points.Count;
            int targetPointCount = writeIndex + pointCount;

            // 数组源已知规模，预扩展列表并直接写入新增区域。
            points.EnsureCapacity(targetPointCount);
            CollectionsMarshal.SetCount(points, targetPointCount);
            // 目标列表转为 Span 写入，避免追加批量点时反复走 List<T> 索引器。
            // 数组源本身已支持直接索引访问，不需要再包装为 Span。
            Span<Vector2I> targetPoints = CollectionsMarshal.AsSpan(points);

            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                Vector2I point = inputPoints[pointIndex];
                // 数组源直接索引读取，目标列表通过 Span 写入。
                targetPoints[writeIndex] = point;
                writeIndex++;
                PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
            }
        }

        /// <summary>
        /// 追加一个列表中的所有点。
        /// <para>该方法不会调用 <see cref="AddPoint(Vector2I)"/>。</para>
        /// </summary>
        /// <param name="inputPoints">要追加的点列表。</param>
        public override void AddPoints(List<Vector2I> inputPoints)
        {
            ArgumentNullException.ThrowIfNull(inputPoints);

            int pointCount = inputPoints.Count;
            int writeIndex = points.Count;
            int targetPointCount = writeIndex + pointCount;

            // 列表源已知规模，预扩展列表并直接写入新增区域。
            points.EnsureCapacity(targetPointCount);
            CollectionsMarshal.SetCount(points, targetPointCount);
            // List 源与目标列表都转为 Span，避免追加批量点时反复走 List<T> 索引器。
            ReadOnlySpan<Vector2I> sourcePoints = CollectionsMarshal.AsSpan(inputPoints);
            Span<Vector2I> targetPoints = CollectionsMarshal.AsSpan(points);

            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                Vector2I point = sourcePoints[pointIndex];
                // 序列形状保留原始输入顺序与重复点。
                targetPoints[writeIndex] = point;
                writeIndex++;
                PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
            }
        }
    }
}
