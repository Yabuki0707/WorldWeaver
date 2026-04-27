using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Godot;

namespace WorldWeaver.PixelShapeSystem.PointsShape
{
    /// <summary>
    /// 动态离散点集合形状。
    /// <para>该类型支持在构造后继续追加点，并在写入阶段拒绝重复点。</para>
    /// <para>点数据由基类的可变列表保存，本类型额外持有 <see cref="HashSet{T}"/> 作为判重缓存。</para>
    /// </summary>
    public sealed class MutablePointSetShape : DynamicPointsShapeBase
    {
        // ================================================================================
        //                                  核心缓存
        // ================================================================================

        /// <summary>
        /// 已接受点的键集合，用于写入阶段判重。
        /// </summary>
        private readonly HashSet<long> _pointKeys = new();


        // ================================================================================
        //                                  构造方法
        // ================================================================================

        /// <summary>
        /// 创建一个空的动态点集合形状。
        /// </summary>
        public MutablePointSetShape()
        {
        }

        /// <summary>
        /// 使用一组全局坐标初始化动态点集合形状。
        /// <para>初始化过程与后续追加一样，都会在写入阶段执行去重。</para>
        /// </summary>
        /// <param name="inputPoints">输入点序列。</param>
        public MutablePointSetShape(IEnumerable<Vector2I> inputPoints)
        {
            AddPoints(inputPoints);
        }

        /// <summary>
        /// 使用一组全局坐标初始化动态点集合形状。
        /// </summary>
        /// <param name="inputPoints">输入点数组。</param>
        public MutablePointSetShape(params Vector2I[] inputPoints)
        {
            AddPoints(inputPoints);
        }

        /// <summary>
        /// 使用一组全局坐标初始化动态点集合形状。
        /// </summary>
        /// <param name="inputPoints">输入点列表。</param>
        public MutablePointSetShape(List<Vector2I> inputPoints)
        {
            AddPoints(inputPoints);
        }


        // ================================================================================
        //                                  点追加方法
        // ================================================================================

        /// <summary>
        /// 追加一个点。
        /// <para>该方法面向单点追加，不适合大规模批量追加；批量输入应使用 <c>AddPoints</c> 系列方法。</para>
        /// <para>若该点已存在，则会输出警告并跳过。</para>
        /// </summary>
        /// <param name="point">要追加的全局坐标点。</param>
        public override void AddPoint(Vector2I point)
        {
            if (!_pointKeys.Add(point.ToKey()))
            {
                GD.PushWarning($"[PixelShapeSystem/MutablePointSetShape]: 重复点写入已被跳过，point={point}。");
                return;
            }

            points.Add(point);
            PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
        }

        /// <summary>
        /// 追加一组点。
        /// <para>该方法会按输入的具体集合类型选择遍历策略，不会调用 <see cref="AddPoint(Vector2I)"/>。</para>
        /// <para>输入中的重复点会被跳过，并在遍历结束后统一输出警告。</para>
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
                // 仅在遇到重复点时创建字符串，避免正常路径产生额外分配。
                StringBuilder duplicatePointMessage = null;

                // 已知输入规模时预留最大可能容量，重复点会在结束后通过 SetCount 收缩。
                points.EnsureCapacity(targetPointCount);
                _pointKeys.EnsureCapacity(targetPointCount);
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
                        // 重复点只记录警告并跳过，保留首次写入的点。
                        if (!_pointKeys.Add(point.ToKey()))
                        {
                            AppendDuplicatePointMessage(ref duplicatePointMessage, point);
                            continue;
                        }

                        // 写入唯一点并同步扩展边界。
                        targetPoints[writeIndex] = point;
                        writeIndex++;
                        PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
                    }
                }
                else
                {
                    foreach (Vector2I point in inputPoints)
                    {
                        // 重复点只记录警告并跳过，保留首次写入的点。
                        if (!_pointKeys.Add(point.ToKey()))
                        {
                            AppendDuplicatePointMessage(ref duplicatePointMessage, point);
                            continue;
                        }

                        // 写入唯一点并同步扩展边界。
                        targetPoints[writeIndex] = point;
                        writeIndex++;
                        PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
                    }
                }

                // 输入中存在重复点时，收缩掉预扩展列表的尾部空位。
                if (writeIndex < targetPointCount)
                {
                    CollectionsMarshal.SetCount(points, writeIndex);
                }

                PushDuplicatePointWarning(duplicatePointMessage);
                return;
            }

            // 无法提前知道规模时，只能逐个追加；仍然不调用 AddPoint，避免批量路径走单点接口。
            StringBuilder deferredDuplicatePointMessage = null;
            foreach (Vector2I point in inputPoints)
            {
                // 重复点只记录警告并跳过，保留首次写入的点。
                if (!_pointKeys.Add(point.ToKey()))
                {
                    AppendDuplicatePointMessage(ref deferredDuplicatePointMessage, point);
                    continue;
                }

                // 写入唯一点并同步扩展边界。
                points.Add(point);
                PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
            }

            PushDuplicatePointWarning(deferredDuplicatePointMessage);
        }

        /// <summary>
        /// 追加一个数组中的所有点。
        /// <para>该方法不会调用 <see cref="AddPoint(Vector2I)"/>。</para>
        /// <para>输入中的重复点会被跳过，并在遍历结束后统一输出警告。</para>
        /// </summary>
        /// <param name="inputPoints">要追加的点数组。</param>
        public override void AddPoints(Vector2I[] inputPoints)
        {
            ArgumentNullException.ThrowIfNull(inputPoints);

            int pointCount = inputPoints.Length;
            int writeIndex = points.Count;
            int targetPointCount = writeIndex + pointCount;
            // 仅在遇到重复点时创建字符串，避免正常路径产生额外分配。
            StringBuilder duplicatePointMessage = null;

            // 按最大可能新增点数扩展，重复点会在遍历后收缩。
            points.EnsureCapacity(targetPointCount);
            _pointKeys.EnsureCapacity(targetPointCount);
            CollectionsMarshal.SetCount(points, targetPointCount);
            // 目标列表转为 Span 写入，避免追加批量点时反复走 List<T> 索引器。
            // 数组源本身已支持直接索引访问，不需要再包装为 Span。
            Span<Vector2I> targetPoints = CollectionsMarshal.AsSpan(points);

            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                Vector2I point = inputPoints[pointIndex];
                // 重复点只记录警告并跳过，保留首次写入的点。
                if (!_pointKeys.Add(point.ToKey()))
                {
                    AppendDuplicatePointMessage(ref duplicatePointMessage, point);
                    continue;
                }

                // 写入唯一点并同步扩展边界。
                targetPoints[writeIndex] = point;
                writeIndex++;
                PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
            }

            // 输入中存在重复点时，收缩掉预扩展列表的尾部空位。
            if (writeIndex < targetPointCount)
            {
                CollectionsMarshal.SetCount(points, writeIndex);
            }

            PushDuplicatePointWarning(duplicatePointMessage);
        }

        /// <summary>
        /// 追加一个列表中的所有点。
        /// <para>该方法不会调用 <see cref="AddPoint(Vector2I)"/>。</para>
        /// <para>输入中的重复点会被跳过，并在遍历结束后统一输出警告。</para>
        /// </summary>
        /// <param name="inputPoints">要追加的点列表。</param>
        public override void AddPoints(List<Vector2I> inputPoints)
        {
            ArgumentNullException.ThrowIfNull(inputPoints);

            int pointCount = inputPoints.Count;
            int writeIndex = points.Count;
            int targetPointCount = writeIndex + pointCount;
            // 仅在遇到重复点时创建字符串，避免正常路径产生额外分配。
            StringBuilder duplicatePointMessage = null;

            // 按最大可能新增点数扩展，重复点会在遍历后收缩。
            points.EnsureCapacity(targetPointCount);
            _pointKeys.EnsureCapacity(targetPointCount);
            CollectionsMarshal.SetCount(points, targetPointCount);
            // List 源与目标列表都转为 Span，避免追加批量点时反复走 List<T> 索引器。
            ReadOnlySpan<Vector2I> sourcePoints = CollectionsMarshal.AsSpan(inputPoints);
            Span<Vector2I> targetPoints = CollectionsMarshal.AsSpan(points);

            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                Vector2I point = sourcePoints[pointIndex];
                // 重复点只记录警告并跳过，保留首次写入的点。
                if (!_pointKeys.Add(point.ToKey()))
                {
                    AppendDuplicatePointMessage(ref duplicatePointMessage, point);
                    continue;
                }

                // 写入唯一点并同步扩展边界。
                targetPoints[writeIndex] = point;
                writeIndex++;
                PointsShape.ExpandCoordinateBounds(point, ref minX, ref maxX, ref minY, ref maxY);
            }

            // 输入中存在重复点时，收缩掉预扩展列表的尾部空位。
            if (writeIndex < targetPointCount)
            {
                CollectionsMarshal.SetCount(points, writeIndex);
            }

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
                duplicatePointMessage = new StringBuilder("[PixelShapeSystem/MutablePointSetShape]: 重复点写入已被跳过，points=");
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
