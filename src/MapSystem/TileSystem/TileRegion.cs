using WorldWeaver.PixelShapeSystem;

namespace WorldWeaver.MapSystem.TileSystem
{
    /// <summary>
    /// Tile 区域。
    /// <para>继承自 <see cref="PixelRegion{T}"/>，表达“全局坐标形状 + 统一 TileRunId”。</para>
    /// </summary>
    public sealed class TileRegion : PixelRegion<int>
    {
        /// <summary>
        /// 区域内统一使用的 TileRunId。
        /// </summary>
        public int TileRunId => Value;

        /// <summary>
        /// 创建一个 Tile 区域。
        /// </summary>
        public TileRegion(PixelShape shape, int tileRunId) : base(shape, tileRunId)
        {
        }

        /// <summary>
        /// 使用现有的像素区域创建 Tile 区域。
        /// </summary>
        public TileRegion(PixelRegion<int> source) : base(
            source?.Shape ?? throw new System.ArgumentNullException(nameof(source)),
            source.Value)
        {
        }
    }
}
