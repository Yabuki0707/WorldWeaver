using Godot;

namespace WorldWeaver.PixelShapeSystem
{
    /// <summary>
    /// 几何像素形状接口。
    /// <para>该接口用于表达“形状拥有明确几何原点”的能力。</para>
    /// <para>原点语义为形状起始的左上角坐标，具体像素覆盖范围由实现类结合自身尺寸规则实时计算。</para>
    /// </summary>
    public interface IGeometricPixelShape
    {
        /// <summary>
        /// 图形原点。
        /// <para>原点即图形起始的左上角坐标。</para>
        /// </summary>
        Vector2I Origin { get; set; }
    }
}
