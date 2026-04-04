using Godot;

namespace WorldWeaver.PixelShapeSystem
{
    /// <summary>
    /// 可进行大小扩展的像素形状能力接口。
    /// <para>只有当某个图形支持基于原始形状进行 X/Y 方向的大小扩展时，才应实现该接口。</para>
    /// <para>若扩展后会改变图形的基本语义，则不应强行实现该接口。</para>
    /// </summary>
    public interface IScalablePixelShape
    {
        /// <summary>
        /// 图形相对于原始形状的大小扩展倍率。
        /// <para>该值描述图形在 X 与 Y 方向上的扩展比例，而不是包围盒跨度。</para>
        /// </summary>
        Vector2I Scale { get; }
    }
}
