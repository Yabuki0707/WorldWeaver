using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Godot;

namespace WorldWeaver.PixelShapeSystem.PointsShape
{
    /// <summary>
    /// 静态离散点集合形状。
    /// <para>该类型只在构造阶段接收点数据，构造完成后不再允许继续追加点。</para>
    /// <para>输入中的重复点会在构造阶段被自动跳过，对外始终保持“点集合”语义。</para>
    /// </summary>
    public sealed class PointSetShape : StaticPointsShapeBase
    {
        // ================================================================================
        //                                  构造方法
        // ================================================================================

        /// <summary>
        /// 创建一个空的静态点集合形状。
        /// </summary>
        public PointSetShape()
        {
        }

        /// <summary>
        /// 使用一组全局坐标创建静态点集合形状。
        /// <para>构造过程中会去除重复点，并保留首次出现顺序。</para>
        /// </summary>
        /// <param name="inputPoints">输入点序列。</param>
        public PointSetShape(IEnumerable<Vector2I> inputPoints)
        {
            ArgumentNullException.ThrowIfNull(inputPoints);

            // 用于构造阶段判重，构造完成后不再持有。
            HashSet<long> uniquePointKeys = new();
            // 使用哨兵值初始化边界，写入第一个有效点时自动收敛。
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            // 仅在遇到重复点时创建字符串，避免正常路径产生额外分配。
            StringBuilder duplicatePointMessage = null;
            int writeIndex = 0;

            // 可计数集合可以预先分配目标数组和判重集合，重复点会在遍历后压缩。
            if (inputPoints is ICollection<Vector2I> pointCollection)
            {
                points = pointCollection.Count == 0
                    ? Array.Empty<Vector2I>()
                    : new Vector2I[pointCollection.Count];
                uniquePointKeys.EnsureCapacity(pointCollection.Count);

                // IList<T> 支持稳定索引读取，这里直接 for 读取并直接写入目标数组。
                if (inputPoints is IList<Vector2I> pointListSource)
                {
                    int pointCount = pointListSource.Count;
                    for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
                    {
                        Vector2I point = pointListSource[pointIndex];
                        // 重复点只记录警告并跳过，保留首次出现的点。
                        if (!uniquePointKeys.Add(point.ToKey()))
                        {
                            AppendDuplicatePointMessage(ref duplicatePointMessage, point);
                            continue;
                        }

                        // 写入唯一点并同步扩展边界。
                        points[writeIndex] = point;
                        writeIndex++;
                        PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
                    }
                }
                else
                {
                    foreach (Vector2I point in inputPoints)
                    {
                        // 重复点只记录警告并跳过，保留首次出现的点。
                        if (!uniquePointKeys.Add(point.ToKey()))
                        {
                            AppendDuplicatePointMessage(ref duplicatePointMessage, point);
                            continue;
                        }

                        // 写入唯一点并同步扩展边界。
                        points[writeIndex] = point;
                        writeIndex++;
                        PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
                    }
                }

                // 输入中存在重复点时，收缩掉预分配数组的尾部空位。
                if (writeIndex < points.Length)
                {
                    Array.Resize(ref points, writeIndex);
                }
            }
            // 不可计数集合只能遍历输入点，记录重复点并跳过。
            else
            {
                List<Vector2I> normalizedPoints = new();
                foreach (Vector2I point in inputPoints)
                {
                    // 重复点只记录警告并跳过，保留首次出现的点。
                    if (!uniquePointKeys.Add(point.ToKey()))
                    {
                        AppendDuplicatePointMessage(ref duplicatePointMessage, point);
                        continue;
                    }

                    // 写入唯一点并同步扩展边界。
                    normalizedPoints.Add(point);
                    PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
                }

                // 静态点形状最终只持有不可变数组。
                points = normalizedPoints.Count == 0 ? Array.Empty<Vector2I>() : normalizedPoints.ToArray();
            }

            // 使用最终有效点数量创建边界，重复点不会参与 PointCount。
            coordinateBounds = PointsShape.CreateCoordinateBounds(points.Length, minX, maxX, minY, maxY);
            PushDuplicatePointWarning(duplicatePointMessage);
        }

        /// <summary>
        /// 使用一组全局坐标创建静态点集合形状。
        /// </summary>
        /// <param name="inputPoints">输入点数组。</param>
        public PointSetShape(params Vector2I[] inputPoints)
        {
            ArgumentNullException.ThrowIfNull(inputPoints);

            int pointCount = inputPoints.Length;
            // 按最大可能唯一点数分配，重复点会在遍历后压缩。
            points = pointCount == 0 ? Array.Empty<Vector2I>() : new Vector2I[pointCount];

            // 构造阶段临时判重，构造完成后释放。
            HashSet<long> uniquePointKeys = new(pointCount);
            // 使用哨兵值初始化边界，写入第一个有效点时自动收敛。
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            // 仅在遇到重复点时创建字符串，避免正常路径产生额外分配。
            StringBuilder duplicatePointMessage = null;
            int writeIndex = 0;

            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                Vector2I point = inputPoints[pointIndex];
                // 重复点只记录警告并跳过，保留首次出现的点。
                if (!uniquePointKeys.Add(point.ToKey()))
                {
                    AppendDuplicatePointMessage(ref duplicatePointMessage, point);
                    continue;
                }

                // 数组源直接索引读取，目标数组直接索引写入。
                points[writeIndex] = point;
                writeIndex++;
                PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
            }

            // 输入中存在重复点时，收缩掉预分配数组的尾部空位。
            if (writeIndex < points.Length)
            {
                Array.Resize(ref points, writeIndex);
            }

            // 使用最终有效点数量创建边界，重复点不会参与 PointCount。
            coordinateBounds = PointsShape.CreateCoordinateBounds(points.Length, minX, maxX, minY, maxY);
            PushDuplicatePointWarning(duplicatePointMessage);
        }

        /// <summary>
        /// 使用一组全局坐标创建静态点集合形状。
        /// </summary>
        /// <param name="inputPoints">输入点列表。</param>
        public PointSetShape(List<Vector2I> inputPoints)
        {
            ArgumentNullException.ThrowIfNull(inputPoints);

            int pointCount = inputPoints.Count;
            // 按最大可能唯一点数分配，重复点会在遍历后压缩。
            points = pointCount == 0 ? Array.Empty<Vector2I>() : new Vector2I[pointCount];

            // 构造阶段临时判重，构造完成后释放。
            HashSet<long> uniquePointKeys = new(pointCount);
            // 使用哨兵值初始化边界，写入第一个有效点时自动收敛。
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            // 仅在遇到重复点时创建字符串，避免正常路径产生额外分配。
            StringBuilder duplicatePointMessage = null;
            // List<T> 源转为 Span，避免构造时反复走 List<T> 索引器。
            ReadOnlySpan<Vector2I> sourcePoints = CollectionsMarshal.AsSpan(inputPoints);
            int writeIndex = 0;

            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                Vector2I point = sourcePoints[pointIndex];
                // 重复点只记录警告并跳过，保留首次出现的点。
                if (!uniquePointKeys.Add(point.ToKey()))
                {
                    AppendDuplicatePointMessage(ref duplicatePointMessage, point);
                    continue;
                }

                // 写入唯一点并同步扩展边界。
                points[writeIndex] = point;
                writeIndex++;
                PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
            }

            // 输入中存在重复点时，收缩掉预分配数组的尾部空位。
            if (writeIndex < points.Length)
            {
                Array.Resize(ref points, writeIndex);
            }

            // 使用最终有效点数量创建边界，重复点不会参与 PointCount。
            coordinateBounds = PointsShape.CreateCoordinateBounds(points.Length, minX, maxX, minY, maxY);
            PushDuplicatePointWarning(duplicatePointMessage);
        }


        // ================================================================================
        //                                  重复点警告
        // ================================================================================

        /// <summary>
        /// 将重复点追加到警告信息中。
        /// </summary>
        /// <param name="duplicatePointMessage">重复点警告信息构建器。</param>
        /// <param name="point">重复点坐标。</param>
        private static void AppendDuplicatePointMessage(
            ref StringBuilder duplicatePointMessage,
            Vector2I point)
        {
            if (duplicatePointMessage == null)
            {
                // 首次发现重复点时才创建 StringBuilder。
                duplicatePointMessage = new StringBuilder("[PixelShapeSystem/PointSetShape]: 重复点写入已被跳过，points=");
            }
            else
            {
                // 多个重复点共享同一条警告，避免重复刷屏。
                duplicatePointMessage.Append(", ");
            }

            duplicatePointMessage
                .Append('(')
                .Append(point.X)
                .Append(", ")
                .Append(point.Y)
                .Append(')');
        }

        /// <summary>
        /// 若存在重复点警告信息，则将其统一输出。
        /// </summary>
        /// <param name="duplicatePointMessage">重复点警告信息构建器。</param>
        private static void PushDuplicatePointWarning(StringBuilder duplicatePointMessage)
        {
            if (duplicatePointMessage != null)
            {
                GD.PushWarning(duplicatePointMessage.ToString());
            }
        }
    }
}
