using System;

namespace WorldWeaver.PixelShapeSystem
{
    /// <summary>
    /// 形状与值的组合区域。
    /// <para><see cref="Shape"/> 用于描述覆盖哪些离散像素点，<see cref="Value"/> 用于描述这些点对应的统一业务值。</para>
    /// <para>该类型不会改变 <see cref="PixelShape"/> 的几何语义，只是在其外层附加额外信息。</para>
    /// </summary>
    /// <typeparam name="T">区域附带的值类型。</typeparam>
    public class PixelRegion<T>
    {
        /// <summary>
        /// 区域对应的形状。
        /// </summary>
        public PixelShape Shape { get; }

        /// <summary>
        /// 区域统一附带的值。
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// 创建一个形状与值的组合区域。
        /// </summary>
        /// <param name="shape">区域形状，不能为空。</param>
        /// <param name="value">区域值。</param>
        public PixelRegion(PixelShape shape, T value)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            Value = value;
        }
    }
}
