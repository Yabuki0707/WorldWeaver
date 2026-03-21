using Godot;
using System.Runtime.CompilerServices;

namespace rasu.Map.Position
{
    /// <summary>
    /// 全局坐标与指数大小转换器
    /// <para>提供基于位运算的层级坐标转换方法，使用范围为使用指数大小的全局坐标系。</para>
    /// <para>推荐使用 using GPosSizeExpConv = Map.GlobalPositionSizeExpConverter; 简化调用。</para>
    /// </summary>
    public static class GlobalPositionSizeExpConverter
    {
        // ================================================================================
        //                              ToLocalPositionByMark
        // ================================================================================

        /// <summary>
        /// 根据全局坐标和父级大小掩码，计算局部坐标
        /// <para>利用 2 的幂减一（如 2ⁿ−1）的二进制低 n 位全为 1 的特性，通过按位与（&amp;）直接提取整数的低 n 位，从而高效计算模 2ⁿ 的余数（即局部坐标），支持负数。</para>
        /// </summary>
        /// <param name="globalX">全局坐标X</param>
        /// <param name="globalY">全局坐标Y</param>
        /// <param name="parentSizeMark">父级大小掩码</param>
        /// <returns>局部坐标元组</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y) ToLocalPositionByMark(int globalX, int globalY, Vector2I parentSizeMark)
            => (globalX & parentSizeMark.X, globalY & parentSizeMark.Y);



        /// <summary>
        /// ToLocalPositionByMark的Vector2I坐标重载
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y) ToLocalPositionByMark(Vector2I globalPosition, Vector2I parentSizeMark)
            => ToLocalPositionByMark(globalPosition.X, globalPosition.Y, parentSizeMark);




        // ================================================================================
        //                              ToLocalPositionByExp
        // ================================================================================

        /// <summary>
        /// 根据全局坐标和父级大小指数，计算局部坐标
        /// <para>将指数转换为掩码后调用ToLocalPositionByMark执行。</para>
        /// </summary>
        /// <param name="globalX">全局坐标X</param>
        /// <param name="globalY">全局坐标Y</param>
        /// <param name="parentSizeExp">父级大小指数</param>
        /// <returns>局部坐标元组</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y) ToLocalPositionByExp(int globalX, int globalY, Vector2I parentSizeExp)
            => ToLocalPositionByMark(globalX, globalY, new Vector2I((1 << parentSizeExp.X) - 1, (1 << parentSizeExp.Y) - 1));





        /// <summary>
        /// ToLocalPositionByExp的Vector2I坐标重载
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y) ToLocalPositionByExp(Vector2I globalPosition, Vector2I parentSizeExp)
            => ToLocalPositionByExp(globalPosition.X, globalPosition.Y, parentSizeExp);






        // ================================================================================
        //                            ToGlobalParentPosition
        // ================================================================================

        /// <summary>
        /// 根据全局坐标和父级大小指数，计算全局父级坐标
        /// <para>利用右移位运算的向下取整的特性，直接推算出父级坐标系的坐标。</para>
        /// </summary>
        /// <param name="globalX">全局坐标X</param>
        /// <param name="globalY">全局坐标Y</param>
        /// <param name="parentSizeExp">父级大小指数</param>
        /// <returns>全局父级坐标元组</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y) ToGlobalParentPosition(int globalX, int globalY, Vector2I parentSizeExp)
            => (globalX >> parentSizeExp.X, globalY >> parentSizeExp.Y);



        /// <summary>
        /// ToGlobalParentPosition的Vector2I坐标重载
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y) ToGlobalParentPosition(Vector2I globalPosition, Vector2I parentSizeExp)
            => ToGlobalParentPosition(globalPosition.X, globalPosition.Y, parentSizeExp);




        // ================================================================================
        //                       ToLocalAndGlobalParentPosition
        // ================================================================================

        /// <summary>
        /// 根据全局坐标和父级大小指数，同时计算局部坐标和全局父级坐标
        /// <para>利用右移位运算的向下取整的特性，先推算出父级坐标，再通过减法得到局部坐标。</para>
        /// </summary>
        /// <param name="globalX">全局坐标X</param>
        /// <param name="globalY">全局坐标Y</param>
        /// <param name="parentSizeExp">父级大小指数</param>
        /// <returns>局部坐标和全局父级坐标的元组</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ((int x, int y) localPosition, (int x, int y) globalParentPosition) ToLocalAndGlobalParentPosition(int globalX, int globalY, Vector2I parentSizeExp)
        {
            // 计算全局父级坐标
            int parentX = globalX >> parentSizeExp.X;
            int parentY = globalY >> parentSizeExp.Y;
            (int x, int y) globalParentPosition = (parentX, parentY);

            // 计算局部坐标
            (int x, int y) localPosition = (globalX - (parentX << parentSizeExp.X),
                                            globalY - (parentY << parentSizeExp.Y));
            return (localPosition, globalParentPosition);
        }



        /// <summary>
        /// ToLocalAndGlobalParentPosition的Vector2I坐标重载
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ((int x, int y) localPosition, (int x, int y) globalParentPosition) ToLocalAndGlobalParentPosition(Vector2I globalPosition, Vector2I parentSizeExp)
            => ToLocalAndGlobalParentPosition(globalPosition.X, globalPosition.Y, parentSizeExp);




        // ================================================================================
        //                         ToOriginGlobalChildPosition
        // ================================================================================

        /// <summary>
        /// 根据全局父级坐标和大小指数，计算原点全局子坐标
        /// <para>利用左移位运算的乘法特性，直接推算出子坐标系的原点坐标。</para>
        /// </summary>
        /// <param name="globalParentX">全局父级坐标X</param>
        /// <param name="globalParentY">全局父级坐标Y</param>
        /// <param name="sizeExp">大小指数</param>
        /// <returns>原点全局子坐标元组</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y) ToOriginGlobalChildPosition(int globalParentX, int globalParentY, Vector2I sizeExp)
            => (globalParentX << sizeExp.X, globalParentY << sizeExp.Y);



        /// <summary>
        /// ToOriginGlobalChildPosition的Vector2I坐标重载
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y) ToOriginGlobalChildPosition(Vector2I globalParentPosition, Vector2I sizeExp)
            => ToOriginGlobalChildPosition(globalParentPosition.X, globalParentPosition.Y, sizeExp);




        // ================================================================================
        //                                 实用转换
        // ================================================================================

        /// <summary>
        /// 将坐标元组转换为Vector2I
        /// </summary>
        /// <param name="pos">坐标元组</param>
        /// <returns>Vector2I坐标</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2I ToVector2I((int x, int y) pos)
            => new(pos.x, pos.y);

    }
}
