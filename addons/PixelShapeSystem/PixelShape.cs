using System.Collections.Generic;
using Godot;

namespace WorldWeaver.PixelShapeSystem
{
    /// <summary>
    /// 像素形状抽象基类。
    /// <para>该类型只负责描述一个形状覆盖了哪些离散像素点，不承担地图局部坐标、数组索引等上层语义。</para>
    /// <para>所有子类都应保证其公开输出的全局坐标顺序稳定，以便与外部附加的数据数组保持一一对应关系。</para>
    /// </summary>
    public abstract class PixelShape
    {
        // ================================================================================
        //                                  基础属性
        // ================================================================================

        /// <summary>
        /// 图形的边界差值盒（坐标包围盒）。
        /// <para><see cref="Rect2I.Position"/> 表示最小边界坐标（Min），<see cref="Rect2I.Size"/> 表示边界差值（Max - Min）。</para>
        /// <para>最大边界坐标（Max）满足：<c>Max = Position + Size</c>。</para>
        /// <para>例如：当 <c>Min=(1,1)</c> 且 <c>Max=(3,3)</c> 时，<c>Size=(2,2)</c>。</para>
        /// </summary>
        public abstract Rect2I BoundingBox { get; }

        /// <summary>
        /// 图形当前覆盖的离散点数量。
        /// <para>该数量描述形状实际包含多少个有效像素点，而不是包围盒面积。</para>
        /// </summary>
        public abstract int PointCount { get; }


        // ================================================================================
        //                                  迭代器
        // ================================================================================

        /// <summary>
        /// 获取当前图形覆盖点的全局坐标迭代器。
        /// <para>迭代结果中的每一项都表示一个被该图形覆盖的全局像素坐标。</para>
        /// <para>该方法适用于流式消费坐标，不要求一次性分配列表或数组。</para>
        /// </summary>
        public abstract IEnumerable<Vector2I> GetGlobalCoordinateIterator();


        // ================================================================================
        //                                  列表
        // ================================================================================

        /// <summary>
        /// 获取当前图形覆盖点的全局坐标列表。
        /// <para>返回值应与 <see cref="GetGlobalCoordinateIterator"/> 的点顺序保持一致。</para>
        /// </summary>
        public abstract List<Vector2I> GetGlobalCoordinateList();


        // ================================================================================
        //                                  数组
        // ================================================================================

        /// <summary>
        /// 获取当前图形覆盖点的全局坐标数组。
        /// <para>返回值应与 <see cref="GetGlobalCoordinateIterator"/> 的点顺序保持一致。</para>
        /// </summary>
        public abstract Vector2I[] GetGlobalCoordinateArray();
    }
}
