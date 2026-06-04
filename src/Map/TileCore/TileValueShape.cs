using System;
using WorldWeaver.PixelShapeSystem;
using WorldWeaver.PixelShapeSystem.ValueShape;

namespace WorldWeaver.Map.TileCore
{
    /// <summary>
    /// Tile 值形状。组合 <see cref="IPixelValueShape{T}"/>，索引器委托 <see cref="ValueShape"/>。
    /// </summary>
    public sealed class TileValueShape
    {
        /// <summary>
        /// 内部像素值形状。
        /// </summary>
        public IPixelValueShape<PixelShape, int> ValueShape { get; }

        /// <summary>
        /// 包装一个像素值形状。
        /// </summary>
        public TileValueShape(IPixelValueShape<PixelShape, int> valueShape)
        {
            ValueShape = valueShape ?? throw new ArgumentNullException(nameof(valueShape));
        }

        /// <summary>
        /// 按索引读写 TileRunId，直接委托 <see cref="ValueShape"/> 的索引器。
        /// </summary>
        public int this[int i]
        {
            get => ValueShape[i];
            set => ValueShape[i] = value;
        }
    }
}
