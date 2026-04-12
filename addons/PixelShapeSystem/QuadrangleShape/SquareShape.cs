using Godot;

namespace WorldWeaver.PixelShapeSystem.QuadrangleShape
{
    /// <summary>
    /// 正方形像素形状。
    /// <para>该形状是宽高相等的矩形像素形状，使用边长与左上角原点实时推导覆盖坐标。</para>
    /// </summary>
    public sealed class SquareShape : RectangleShape
    {
        // ================================================================================
        //                                  核心属性
        // ================================================================================

        /// <summary>
        /// 正方形边长。
        /// <para>边长同时对应宽度与高度。</para>
        /// </summary>
        public int SideLength => Width;


        // ================================================================================
        //                                  构造方法
        // ================================================================================

        /// <summary>
        /// 创建正方形像素形状。
        /// </summary>
        public SquareShape(int sideLength, Vector2I origin) : base(sideLength, sideLength, origin)
        {
        }
    }
}
