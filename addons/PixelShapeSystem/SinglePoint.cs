using System.Collections.Generic;
using Godot;

namespace WorldWeaver.PixelShapeSystem
{
    /// <summary>
    /// 单点像素形状。
    /// <para>用于表示全局坐标系中的一个离散像素点。</para>
    /// <para>由于仅包含单点，因此其边界差值盒的 <see cref="Rect2I.Size"/> 始终为 <c>(0,0)</c>。</para>
    /// </summary>
    public sealed class SinglePoint : PixelShape
    {
        // ================================================================================
        //                                  构造
        // ================================================================================

        /// <summary>
        /// 使用指定的全局坐标创建单点形状。
        /// </summary>
        /// <param name="x">点的全局 X 坐标。</param>
        /// <param name="y">点的全局 Y 坐标。</param>
        public SinglePoint(int x, int y) : this(new Vector2I(x, y))
        {
        }

        /// <summary>
        /// 使用指定的全局坐标创建单点形状。
        /// </summary>
        /// <param name="position">点的全局坐标。</param>
        public SinglePoint(Vector2I position)
        {
            BoundingBox = new Rect2I(position, Vector2I.Zero);
        }


        // ================================================================================
        //                                  基础属性
        // ================================================================================

        /// <summary>
        /// 单点形状的边界差值盒。
        /// <para>单点的最小边界与最大边界重合，因此差值始终为零。</para>
        /// </summary>
        public override Rect2I BoundingBox { get; }

        /// <summary>
        /// 单点形状覆盖的离散点数量。
        /// </summary>
        public override int PointCount => 1;


        // ================================================================================
        //                                  迭代器
        // ================================================================================

        /// <summary>
        /// 获取该单点的全局坐标迭代器。
        /// </summary>
        public override IEnumerable<Vector2I> GetGlobalCoordinateIterator()
        {
            yield return BoundingBox.Position;
        }


        // ================================================================================
        //                                  列表
        // ================================================================================

        /// <summary>
        /// 获取该单点的全局坐标列表。
        /// </summary>
        public override List<Vector2I> GetGlobalCoordinateList()
        {
            return new List<Vector2I> { BoundingBox.Position };
        }


        // ================================================================================
        //                                  数组
        // ================================================================================

        /// <summary>
        /// 获取该单点的全局坐标数组。
        /// </summary>
        public override Vector2I[] GetGlobalCoordinateArray()
        {
            return new[] { BoundingBox.Position };
        }
    }
}
