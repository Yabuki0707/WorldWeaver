using System;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.PixelShapeSystem.ValueShape
{
    /// <summary>
    /// 挂载值列表的像素形状。
    /// <para><see cref="Shape"/> 描述点的空间分布与顺序，<see cref="Values"/> 描述对应顺序上的业务值。</para>
    /// <para>约定：当 <see cref="Values"/> 不为 <see langword="null"/> 时，<c>Values[i]</c> 始终对应 <see cref="Shape"/> 输出序列中的第 <c>i</c> 个点。</para>
    /// </summary>
    /// <typeparam name="TShape">具体形状类型。</typeparam>
    /// <typeparam name="T">值列表中元素的类型。</typeparam>
    public class PixelValueListShape<TShape, T> : IPixelValueShape<TShape, T>
        where TShape : PixelShape
    {
        /// <summary>
        /// 具体类型的底层像素形状。
        /// </summary>
        public TShape Shape { get; }

        /// <summary>
        /// 与点序对齐的值列表。
        /// <para>为 <see langword="null"/> 时表示该对象仅承载点形状，不承载值数据。</para>
        /// </summary>
        public List<T> Values { get; }

        /// <summary>
        /// 当前对象是否携带值列表。
        /// </summary>
        public bool HasValues => Values != null;

        /// <summary>
        /// 点数量与值数量是否对齐。
        /// </summary>
        public bool IsAligned => Values == null || Shape.PointCount == Values.Count;

        /// <summary>
        /// 当前值列表中的值数量。
        /// </summary>
        public int ValueCount => Values?.Count ?? 0;

        /// <summary>
        /// Shape 不含任何点或未携带值列表时为 <see langword="true"/>。
        /// </summary>
        public bool IsEmpty => Shape.PointCount == 0 || Values == null || Values.Count == 0;

        public Memory<T> ValueMemory => Values != null ? new Memory<T>(Values.ToArray()) : Memory<T>.Empty;

        /// <summary>
        /// 按索引读写列表中的值。
        /// <para>索引必须同时在 <c>Shape.PointCount</c> 与 <c>Values.Count</c> 范围内，越界抛 <see cref="ArgumentOutOfRangeException"/>。</para>
        /// </summary>
        public T this[int i]
        {
            get
            {
                if (Values == null || i < 0 || i >= Shape.PointCount || i >= Values.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(i));
                }

                return Values[i];
            }
            set
            {
                if (Values == null || i < 0 || i >= Shape.PointCount || i >= Values.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(i));
                }

                Values[i] = value;
            }
        }

        /// <summary>
        /// 创建一个挂载值列表的像素形状。
        /// </summary>
        public PixelValueListShape(TShape shape, List<T> values)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            Values = values;

            if (!IsAligned)
            {
                GD.PushWarning(
                    $"[PixelShapeSystem/PixelValueListShape]: Values.Count={Values.Count} 与 Shape.PointCount={Shape.PointCount} 不一致。");
            }
        }

        /// <summary>
        /// 获取全局坐标与对应值的配对迭代器。
        /// </summary>
        public IEnumerable<(Vector2I GlobalPosition, T Value)> GetGlobalValueIterator()
        {
            if (!HasValues)
            {
                GD.PushError("[PixelShapeSystem/PixelValueListShape]: GetGlobalValueIterator 调用失败，当前对象未携带 Values。");
                yield break;
            }

            if (!IsAligned)
            {
                GD.PushError(
                    $"[PixelShapeSystem/PixelValueListShape]: GetGlobalValueIterator 调用失败，Values.Count={Values.Count} 与 Shape.PointCount={Shape.PointCount} 不一致。");
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
