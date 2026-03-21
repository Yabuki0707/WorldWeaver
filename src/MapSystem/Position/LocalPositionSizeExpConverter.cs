using Godot;
using System.Runtime.CompilerServices;

namespace rasu.Map.Position
{
    /// <summary>
    /// 局部坐标与指数大小转换器
    /// <para>提供基于位运算的层级坐标转换方法，用于局部坐标的验证、转换和规范化。</para>
    /// </summary>
    public static class LocalPositionSizeExpConverter
    {
        // ================================================================================
        //                              ToGlobalPosition
        // ================================================================================

        /// <summary>
        /// 根据局部坐标和父级坐标，计算全局坐标
        /// <para>利用左移位运算的乘法特性，将父级坐标左移对应指数位后，与局部坐标相加得到全局坐标。</para>
        /// </summary>
        /// <param name="localX">局部坐标X</param>
        /// <param name="localY">局部坐标Y</param>
        /// <param name="parentX">父级坐标X</param>
        /// <param name="parentY">父级坐标Y</param>
        /// <param name="sizeExp">大小指数</param>
        /// <returns>全局坐标元组</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y) ToGlobalPosition(int localX, int localY, int parentX, int parentY, Vector2I sizeExp)
            => ((parentX << sizeExp.X) + localX, (parentY << sizeExp.Y) + localY);

        /// <summary>
        /// ToGlobalPosition的Vector2I坐标重载
        /// </summary>
        /// <param name="localPosition">局部坐标</param>
        /// <param name="parentPosition">父级坐标</param>
        /// <param name="sizeExp">大小指数</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y) ToGlobalPosition(Vector2I localPosition, Vector2I parentPosition, Vector2I sizeExp)
            => ToGlobalPosition(localPosition.X, localPosition.Y, parentPosition.X, parentPosition.Y, sizeExp);

        
        
        // ================================================================================
        //                           IsValidPositionByMark
        // ================================================================================

        /// <summary>
        /// 检查局部坐标是否在指定大小范围内
        /// <para>利用按位与运算和掩码检查坐标是否在范围内，如果结果等于原坐标则表示在范围内。</para>
        /// </summary>
        /// <param name="localX">局部坐标X</param>
        /// <param name="localY">局部坐标Y</param>
        /// <param name="sizeMark">大小掩码</param>
        /// <returns>如果坐标在有效范围内返回true，否则返回false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidPositionByMark(int localX, int localY, Vector2I sizeMark)
            => (localX & sizeMark.X) == localX && (localY & sizeMark.Y) == localY;

        /// <summary>
        /// IsValidPositionByMark的Vector2I坐标重载
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidPositionByMark(Vector2I localPosition, Vector2I sizeMark)
            => IsValidPositionByMark(localPosition.X, localPosition.Y, sizeMark);


        // ================================================================================
        //                           IsValidPositionByExp
        // ================================================================================

        /// <summary>
        /// 检查局部坐标是否在指定大小范围内
        /// <para>将指数转换为掩码后调用IsValidPositionByMark执行。</para>
        /// </summary>
        /// <param name="localX">局部坐标X</param>
        /// <param name="localY">局部坐标Y</param>
        /// <param name="sizeExp">大小指数</param>
        /// <returns>如果坐标在有效范围内返回true，否则返回false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidPositionByExp(int localX, int localY, Vector2I sizeExp)
            => IsValidPositionByMark(localX, localY, new Vector2I((1 << sizeExp.X) - 1, (1 << sizeExp.Y) - 1));

        /// <summary>
        /// IsValidPositionByExp的Vector2I坐标重载
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidPositionByExp(Vector2I localPosition, Vector2I sizeExp)
            => IsValidPositionByExp(localPosition.X, localPosition.Y, sizeExp);


        // ================================================================================
        //                              IsValidPosition
        // ================================================================================

        /// <summary>
        /// 检查局部坐标是否在指定大小范围内
        /// <para>利用右移位运算检查坐标是否超出范围，如果结果为0则表示在范围内。</para>
        /// </summary>
        /// <param name="localX">局部坐标X</param>
        /// <param name="localY">局部坐标Y</param>
        /// <param name="sizeExp">大小指数</param>
        /// <returns>如果坐标在有效范围内返回true，否则返回false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidPosition(int localX, int localY, Vector2I sizeExp)
            => IsValidPositionByExp(localX, localY, sizeExp);

        /// <summary>
        /// IsValidPosition的Vector2I坐标重载
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidPosition(Vector2I localPosition, Vector2I sizeExp)
            => IsValidPosition(localPosition.X, localPosition.Y, sizeExp);


        // ================================================================================
        //                           ToValidPositionByMark
        // ================================================================================

        /// <summary>
        /// 将局部坐标转换为合法坐标（限制在指定大小范围内）
        /// <para>利用 2 的幂减一（如 2ⁿ−1）的二进制低 n 位全为 1 的特性，通过按位与（&amp;）直接提取整数的低 n 位，从而高效计算模 2ⁿ 的余数（即合法坐标），支持负数。</para>
        /// </summary>
        /// <param name="localX">局部坐标X</param>
        /// <param name="localY">局部坐标Y</param>
        /// <param name="sizeMark">大小掩码</param>
        /// <returns>合法的局部坐标元组</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y) ToValidPositionByMark(int localX, int localY, Vector2I sizeMark)
            => (localX & sizeMark.X, localY & sizeMark.Y);

        /// <summary>
        /// ToValidPositionByMark的Vector2I坐标重载
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y) ToValidPositionByMark(Vector2I localPosition, Vector2I sizeMark)
            => ToValidPositionByMark(localPosition.X, localPosition.Y, sizeMark);


        // ================================================================================
        //                           ToValidPositionByExp
        // ================================================================================

        /// <summary>
        /// 将局部坐标转换为合法坐标（限制在指定大小范围内）
        /// <para>将指数转换为掩码后调用ToValidPositionByMark执行。</para>
        /// </summary>
        /// <param name="localX">局部坐标X</param>
        /// <param name="localY">局部坐标Y</param>
        /// <param name="sizeExp">大小指数</param>
        /// <returns>合法的局部坐标元组</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y) ToValidPositionByExp(int localX, int localY, Vector2I sizeExp)
            => ToValidPositionByMark(localX, localY, new Vector2I((1 << sizeExp.X) - 1, (1 << sizeExp.Y) - 1));

        /// <summary>
        /// ToValidPositionByExp的Vector2I坐标重载
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y) ToValidPositionByExp(Vector2I localPosition, Vector2I sizeExp)
            => ToValidPositionByExp(localPosition.X, localPosition.Y, sizeExp);


        // ================================================================================
        //                              ToValidPosition
        // ================================================================================

        /// <summary>
        /// 将局部坐标转换为合法坐标（限制在指定大小范围内）
        /// <para>利用按位与运算和掩码，将坐标限制在有效范围内。</para>
        /// </summary>
        /// <param name="localX">局部坐标X</param>
        /// <param name="localY">局部坐标Y</param>
        /// <param name="sizeExp">大小指数</param>
        /// <returns>合法的局部坐标元组</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y) ToValidPosition(int localX, int localY, Vector2I sizeExp)
            => ToValidPositionByExp(localX, localY, sizeExp);

        /// <summary>
        /// ToValidPosition的Vector2I坐标重载
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y) ToValidPosition(Vector2I localPosition, Vector2I sizeExp)
            => ToValidPosition(localPosition.X, localPosition.Y, sizeExp);


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
