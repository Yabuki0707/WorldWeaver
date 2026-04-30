using System;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.PixelShapeSystem.PointsShape
{
    /// <summary>
    /// 点形状抽象基类。
    /// <para>用于暴露点形状的零复制坐标切片输出能力，并提供点形状共享的边界工具。</para>
    /// </summary>
    public abstract class PointsShape : PixelShape, IReadOnlyList<Vector2I>
    {
        /// <summary>
        /// 空点形状共用的错误坐标边界。
        /// </summary>
        public static Rect2I EmptyCoordinateBounds { get; } = new(
            new Vector2I(int.MaxValue, int.MaxValue),
            new Vector2I(int.MinValue, int.MinValue));


        // ================================================================================
        //                                  IReadOnlyList<Vector2I>
        // ================================================================================

        /// <summary>
        /// 当前点形状中的点数量。
        /// </summary>
        public int Count => PointCount;

        /// <summary>
        /// 按当前点形状的稳定输出顺序获取指定索引处的全局坐标。
        /// </summary>
        /// <param name="index">全局坐标输出序列中的索引。</param>
        public abstract Vector2I this[int index] { get; }


        // ================================================================================
        //                                  Span输出
        // ================================================================================

        /// <summary>
        /// 获取当前点形状的全局坐标只读切片。
        /// <para>该方法用于高性能读取，不应修改底层存储。</para>
        /// </summary>
        public abstract ReadOnlySpan<Vector2I> GetGlobalCoordinateSpan();

        /// <summary>
        /// 根据边界缓存创建坐标边界。
        /// <para>
        /// <see cref="ExpandCoordinateBounds"/> 负责高速写入边界缓存，允许最小值或最大值单侧仍为哨兵的半边界状态；
        /// 本方法负责在最终创建 <see cref="Rect2I"/> 前对半边界进行兜底归一化。
        /// </para>
        /// </summary>
        /// <param name="pointCount">有效点数量。</param>
        /// <param name="minX">最小 X 坐标。</param>
        /// <param name="maxX">最大 X 坐标。</param>
        /// <param name="minY">最小 Y 坐标。</param>
        /// <param name="maxY">最大 Y 坐标。</param>
        public static Rect2I CreateCoordinateBounds(
            int pointCount,
            int minX,
            int maxX,
            int minY,
            int maxY)
        {
            // 空形状直接返回错误边界，用非法坐标表达“无有效点”的语义。
            if (pointCount == 0)
            {
                return EmptyCoordinateBounds;
            }

            // X 轴最小值有效时，若最大值仍为哨兵，则在边界创建阶段将最大值兜底为最小值。
            if (minX != int.MaxValue)
            {
                if (maxX == int.MinValue)
                {
                    maxX = minX;
                }
            }
            // X 轴最小值无效但最大值有效时，在边界创建阶段使用最大值反向兜底最小值。
            else if (maxX != int.MinValue)
            {
                minX = maxX;
            }

            // Y 轴最小值有效时，若最大值仍为哨兵，则在边界创建阶段将最大值兜底为最小值。
            if (minY != int.MaxValue)
            {
                if (maxY == int.MinValue)
                {
                    maxY = minY;
                }
            }
            // Y 轴最小值无效但最大值有效时，在边界创建阶段使用最大值反向兜底最小值。
            else if (maxY != int.MinValue)
            {
                minY = maxY;
            }

            // 两侧都保持哨兵时保留错误尺寸，否则使用归一化后的最大最小差值。
            return new Rect2I(
                new Vector2I(minX, minY),
                new Vector2I(
                    minX == int.MaxValue && maxX == int.MinValue ? int.MinValue : maxX - minX,
                    minY == int.MaxValue && maxY == int.MinValue ? int.MinValue : maxY - minY));
        }

        /// <summary>
        /// 根据新点扩展边界缓存。
        /// <para>
        /// 该方法服务于高速写入路径，每个坐标轴只进行一次分支更新；
        /// 因此允许最小值或最大值单侧仍为哨兵的半边界状态，最终由 <see cref="CreateCoordinateBounds"/> 统一兜底。
        /// </para>
        /// </summary>
        /// <param name="point">新写入的点。</param>
        /// <param name="minX">最小 X 坐标。</param>
        /// <param name="maxX">最大 X 坐标。</param>
        /// <param name="minY">最小 Y 坐标。</param>
        /// <param name="maxY">最大 Y 坐标。</param>
        public static void ExpandCoordinateBounds(
            Vector2I point,
            ref int minX,
            ref int maxX,
            ref int minY,
            ref int maxY)
        {
            // X 轴高速写入只更新一侧边界，允许另一侧继续保持哨兵值。
            if (point.X < minX)
            {
                minX = point.X;
            }
            else if (point.X > maxX)
            {
                maxX = point.X;
            }

            // Y 轴高速写入只更新一侧边界，允许另一侧继续保持哨兵值。
            if (point.Y < minY)
            {
                minY = point.Y;
            }
            else if (point.Y > maxY)
            {
                maxY = point.Y;
            }
        }
    }
}
