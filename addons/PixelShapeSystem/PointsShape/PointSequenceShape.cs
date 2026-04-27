using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;

namespace WorldWeaver.PixelShapeSystem.PointsShape
{
    /// <summary>
    /// 静态点序列形状。
    /// <para>该类型在构造阶段接收点序列，并保留输入顺序与重复点，不执行去重校验。</para>
    /// <para>适用于明确需要保留原始点序的场景，而不是“点集合”语义。</para>
    /// </summary>
    public sealed class PointSequenceShape : StaticPointsShapeBase
    {
        // ================================================================================
        //                                  构造方法
        // ================================================================================

        /// <summary>
        /// 创建一个空的静态点序列形状。
        /// </summary>
        public PointSequenceShape()
        {
        }

        /// <summary>
        /// 使用一组全局坐标创建静态点序列形状。
        /// <para>构造过程中不会去重，输入顺序与重复点都会被完整保留。</para>
        /// </summary>
        /// <param name="inputPoints">输入点序列。</param>
        public PointSequenceShape(IEnumerable<Vector2I> inputPoints)
        {
            ArgumentNullException.ThrowIfNull(inputPoints);

            // 使用哨兵值初始化边界，写入第一个点时自动收敛。
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;

            // 可计数集合可以预先分配目标数组，避免构造过程中反复扩容。
            if (inputPoints is ICollection<Vector2I> pointCollection)
            {
                points = pointCollection.Count == 0
                    ? Array.Empty<Vector2I>()
                    : new Vector2I[pointCollection.Count];

                int pointIndex = 0;
                // IList<T> 支持稳定索引读取，这里直接 for 读取并直接写入目标数组。
                if (inputPoints is IList<Vector2I> pointList)
                {
                    int pointCount = pointList.Count;
                    for (int sourceIndex = 0; sourceIndex < pointCount; sourceIndex++)
                    {
                        Vector2I point = pointList[sourceIndex];
                        // 序列形状保留原始输入顺序与重复点。
                        points[pointIndex] = point;
                        pointIndex++;
                        PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
                    }
                }
                else
                {
                    foreach (Vector2I point in inputPoints)
                    {
                        // 序列形状保留原始输入顺序与重复点。
                        points[pointIndex] = point;
                        pointIndex++;
                        PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
                    }
                }

                // 防御性处理：某些 ICollection 的 Count 可能大于实际枚举数。
                if (pointIndex < points.Length)
                {
                    Array.Resize(ref points, pointIndex);
                }
            }
            else
            {
                List<Vector2I> normalizedPoints = new();
                foreach (Vector2I point in inputPoints)
                {
                    // 序列形状保留原始输入顺序与重复点。
                    normalizedPoints.Add(point);
                    PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
                }

                // 静态点形状最终只持有不可变数组。
                points = normalizedPoints.Count == 0 ? Array.Empty<Vector2I>() : normalizedPoints.ToArray();
            }

            // 使用最终点数创建边界。
            coordinateBounds = PointsShape.CreateCoordinateBounds(points.Length, minX, maxX, minY, maxY);
        }

        /// <summary>
        /// 使用一组全局坐标创建静态点序列形状。
        /// </summary>
        /// <param name="inputPoints">输入点数组。</param>
        public PointSequenceShape(params Vector2I[] inputPoints)
        {
            ArgumentNullException.ThrowIfNull(inputPoints);

            int pointCount = inputPoints.Length;
            // 数组源已知规模，直接创建等长静态数组。
            points = pointCount == 0 ? Array.Empty<Vector2I>() : new Vector2I[pointCount];

            // 使用哨兵值初始化边界，写入第一个点时自动收敛。
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;

            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                Vector2I point = inputPoints[pointIndex];
                // 数组源直接索引读取，目标数组直接索引写入。
                points[pointIndex] = point;
                PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
            }

            // 使用最终点数创建边界。
            coordinateBounds = PointsShape.CreateCoordinateBounds(points.Length, minX, maxX, minY, maxY);
        }

        /// <summary>
        /// 使用一组全局坐标创建静态点序列形状。
        /// </summary>
        /// <param name="inputPoints">输入点列表。</param>
        public PointSequenceShape(List<Vector2I> inputPoints)
        {
            ArgumentNullException.ThrowIfNull(inputPoints);

            int pointCount = inputPoints.Count;
            // 列表源已知规模，直接创建等长静态数组。
            points = pointCount == 0 ? Array.Empty<Vector2I>() : new Vector2I[pointCount];

            // 使用哨兵值初始化边界，写入第一个点时自动收敛。
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            // List<T> 源转为 Span，避免构造时反复走 List<T> 索引器。
            ReadOnlySpan<Vector2I> sourcePoints = CollectionsMarshal.AsSpan(inputPoints);

            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                Vector2I point = sourcePoints[pointIndex];
                // 序列形状保留原始输入顺序与重复点。
                points[pointIndex] = point;
                PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
            }

            // 使用最终点数创建边界。
            coordinateBounds = PointsShape.CreateCoordinateBounds(points.Length, minX, maxX, minY, maxY);
        }
    }
}
