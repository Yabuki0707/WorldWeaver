using System;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.PixelShapeSystem.ValueShape
{
    /// <summary>
    /// 像素值形状公共接口。
    /// <para><typeparamref name="TShape"/> 协变——<c>IPixelValueShape&lt;RectangleShape, int&gt;</c> 可隐式转为 <c>IPixelValueShape&lt;PixelShape, int&gt;</c>。</para>
    /// <para>不关心具体形状时填 <see cref="PixelShape"/> 即可。</para>
    /// </summary>
    public interface IPixelValueShape<out TShape, T>
        where TShape : PixelShape
    {
        /// <summary>
        /// 具体类型的底层像素形状。
        /// </summary>
        TShape Shape { get; }

        /// <summary>
        /// 当前对象是否携带值容器。
        /// </summary>
        bool HasValues { get; }

        /// <summary>
        /// 点数量与值数量是否对齐。
        /// </summary>
        bool IsAligned { get; }

        /// <summary>
        /// 当前值容器中的值数量。
        /// </summary>
        int ValueCount { get; }

        /// <summary>
        /// Shape 不含任何点或未携带值容器时为 <see langword="true"/>。
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// 以 <see cref="Memory{T}"/> 暴露的值视图。统一值返回 <see cref="Memory{T}.Empty"/>。
        /// </summary>
        Memory<T> ValueMemory { get; }

        /// <summary>
        /// 按索引读写值。统一值范围按 <c>Shape.PointCount</c>，其余按 <c>ValueCount</c>。越界抛 <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        T this[int i] { get; set; }

        /// <summary>
        /// 获取全局坐标与对应值的配对迭代器。
        /// </summary>
        IEnumerable<(Vector2I GlobalPosition, T Value)> GetGlobalValueIterator();
    }
}
