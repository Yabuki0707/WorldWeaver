using System;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.PixelShapeSystem
{
    /// <summary>
    /// 挂载值数组的像素形状。
    /// <para><see cref="Shape"/> 描述点的空间分布与顺序，<see cref="Values"/> 描述对应顺序上的业务值。</para>
    /// <para>约定：当 <see cref="Values"/> 不为 <see langword="null"/> 时，<c>Values[i]</c> 始终对应 <see cref="Shape"/> 输出序列中的第 <c>i</c> 个点。</para>
    /// <para>部分派生类型允许 <see cref="Values"/> 为 <see langword="null"/>，用于表达“只返回点，不返回值”的结果对象。</para>
    /// </summary>
    /// <typeparam name="T">值数组中元素的类型。</typeparam>
    public class PixelValueShape<T>
    {
        /// <summary>
        /// 底层像素形状。
        /// </summary>
        public PixelShape Shape { get; }

        /// <summary>
        /// 与点序对齐的值数组。
        /// <para>为 <see langword="null"/> 时表示该对象仅承载点形状，不承载值数据。</para>
        /// </summary>
        public T[] Values { get; }

        /// <summary>
        /// 当前对象是否携带值数组。
        /// </summary>
        public bool HasValues => Values != null;

        /// <summary>
        /// 当前对象是否处于无效状态。
        /// <para>仅当对象携带值数组且其长度与点数不一致时，才视为无效。</para>
        /// </summary>
        public bool IsInvalid => Values != null && Shape.PointCount != Values.Length;

        /// <summary>
        /// 当前对象是否处于有效状态。
        /// </summary>
        public bool IsValid => !IsInvalid;

        /// <summary>
        /// 底层形状的边界差值盒。
        /// </summary>
        public Rect2I BoundingBox => Shape.BoundingBox;

        /// <summary>
        /// 创建一个挂载值数组的像素形状。
        /// </summary>
        /// <param name="shape">底层形状，不能为空。</param>
        /// <param name="values">与点序对应的值数组；允许为 <see langword="null"/>。</param>
        public PixelValueShape(PixelShape shape, T[] values)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            Values = values;

            if (IsInvalid)
            {
                GD.PushError(
                    $"[PixelShapeSystem/PixelValueShape]: 构造失败，Values.Length={Values.Length} 与 Shape.PointCount={Shape.PointCount} 不一致。");
            }
        }

        /// <summary>
        /// 获取全局坐标与对应值的配对迭代器。
        /// <para>若当前对象未携带值数组，或值数组长度与点数不一致，将输出错误并终止迭代。</para>
        /// </summary>
        public IEnumerable<(Vector2I GlobalPosition, T Value)> GetGlobalValueIterator()
        {
            if (Values == null)
            {
                GD.PushError("[PixelShapeSystem/PixelValueShape]: GetGlobalValueIterator 调用失败，当前对象未携带 Values。");
                yield break;
            }

            if (IsInvalid)
            {
                GD.PushError(
                    $"[PixelShapeSystem/PixelValueShape]: GetGlobalValueIterator 调用失败，Values.Length={Values.Length} 与 Shape.PointCount={Shape.PointCount} 不一致。");
                yield break;
            }

            int pointIndex = 0;
            foreach (Vector2I globalPosition in Shape.GetGlobalCoordinateIterator())
            {
                yield return (globalPosition, Values[pointIndex]);
                pointIndex++;
            }
        }
    }
}
