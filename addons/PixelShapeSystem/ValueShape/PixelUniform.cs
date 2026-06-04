using System;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.PixelShapeSystem.ValueShape
{
    /// <summary>
    /// 统一值像素形状。
    /// <para><see cref="Shape"/> 描述点的空间分布与顺序，<see cref="Value"/> 描述所有点共享的统一业务值。</para>
    /// <para>与 <see cref="PixelValueArrayShape{T}"/> / <see cref="PixelValueListShape{T}"/> 不同，本类不分配值数组——迭代器每次返回同一个 <see cref="Value"/>。</para>
    /// </summary>
    /// <typeparam name="TShape">具体形状类型。</typeparam>
    /// <typeparam name="T">统一值的类型。</typeparam>
    public class PixelUniform<TShape, T> : IPixelValueShape<TShape, T>
        where TShape : PixelShape
    {
        /// <summary>
        /// 具体类型的底层像素形状。
        /// </summary>
        public TShape Shape { get; }

        /// <summary>
        /// 所有点共享的统一值。
        /// </summary>
        public T Value { get; set; }

        /// <summary>
        /// 当前对象是否携带值——始终为 <see langword="true"/>。
        /// </summary>
        public bool HasValues => true;

        /// <summary>
        /// 点数量与值数量是否对齐——统一值在定义上始终对齐。
        /// </summary>
        public bool IsAligned => true;

        /// <summary>
        /// 当前值数量——统一值始终为 1。
        /// </summary>
        public int ValueCount => 1;

        /// <summary>
        /// 是否为空——Shape 不含任何点时视为空。
        /// </summary>
        public bool IsEmpty => Shape.PointCount == 0;

        /// <summary>
        /// 统一值没有实际值数组，返回 <see cref="ReadOnlySpan{T}.Empty"/>。
        /// </summary>
        public Memory<T> ValueMemory => Memory<T>.Empty;

        /// <summary>
        /// 按索引读写统一值。范围 0..Shape.PointCount，越界抛 <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public T this[int i]
        {
            get
            {
                if (i < 0 || i >= Shape.PointCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(i));
                }

                return Value;
            }
            set
            {
                if (i < 0 || i >= Shape.PointCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(i));
                }

                Value = value;
            }
        }

        /// <summary>
        /// 创建一个统一值像素形状。
        /// </summary>
        /// <param name="shape">区域形状，不能为空。</param>
        /// <param name="value">统一值。</param>
        public PixelUniform(TShape shape, T value)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            Value = value;
        }

        /// <summary>
        /// 获取全局坐标与统一值的配对迭代器。
        /// <para>每次迭代返回同一个 <see cref="Value"/>，不分配额外数组。</para>
        /// </summary>
        public IEnumerable<(Vector2I GlobalPosition, T Value)> GetGlobalValueIterator()
        {
            foreach (Vector2I globalPosition in Shape.GetGlobalCoordinateIterator())
            {
                yield return (globalPosition, Value);
            }
        }

    }
}
