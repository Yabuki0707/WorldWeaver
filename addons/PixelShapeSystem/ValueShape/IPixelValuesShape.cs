using System.Collections.Generic;
using Godot;

namespace WorldWeaver.PixelShapeSystem.ValueShape
{
    /// <summary>
    /// 像素值形状公共接口。
    /// <para><see cref="Shape"/> 描述点的空间分布与顺序，值容器描述对应顺序上的业务值。</para>
    /// <para>实现类可以基于数组或列表持有值，但都必须保证点序与值序一一对应。</para>
    /// </summary>
    public interface IPixelValuesShape<T>
    {
        /// <summary>
        /// 底层像素形状。
        /// </summary>
        PixelShape Shape { get; }

        /// <summary>
        /// 底层形状的坐标边界范围。
        /// </summary>
        Rect2I CoordinateBounds { get; }

        /// <summary>
        /// 当前对象是否携带值容器。
        /// </summary>
        bool HasValues();

        /// <summary>
        /// 点数量与值数量是否对齐。
        /// </summary>
        bool IsAligned();

        /// <summary>
        /// 当前值容器中的值数量。
        /// </summary>
        int ValueCount { get; }

        /// <summary>
        /// 获取全局坐标与对应值的配对迭代器。
        /// </summary>
        IEnumerable<(Vector2I GlobalPosition, T Value)> GetGlobalValueIterator();

        /// <summary>
        /// 获取全局坐标与对应值索引的配对迭代器。
        /// </summary>
        IEnumerable<(Vector2I GPosition, int ValueIndex)> GetGlobalValueIndexIterator();
    }
}
