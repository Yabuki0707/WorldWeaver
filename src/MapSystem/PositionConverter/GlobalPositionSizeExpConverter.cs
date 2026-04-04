using Godot;
using System.Runtime.CompilerServices;

namespace WorldWeaver.MapSystem.PositionConverter
{
    /// <summary>
    /// 全局坐标与指数尺寸转换器。
    /// <para>基于指数和掩码执行位运算换算。</para>
    /// </summary>
    public static class GlobalPositionSizeExpConverter
    {
        // ================================================================================
        //                                  ToLocalPosition
        // ================================================================================

        /// <summary>
        /// 根据尺寸掩码，将全局坐标转换为局部坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2I ToLocalPosition(int globalX, int globalY, MapElementSize parentSize)
            => new(globalX & parentSize.WidthMask, globalY & parentSize.HeightMask);

        /// <summary>
        /// 根据尺寸掩码，将全局坐标转换为局部坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2I ToLocalPosition(Vector2I globalPosition, MapElementSize parentSize)
            => ToLocalPosition(globalPosition.X, globalPosition.Y, parentSize);


        // ================================================================================
        //                                  ToGlobalParentPosition
        // ================================================================================

        /// <summary>
        /// 根据尺寸指数，将全局坐标转换为父级全局坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2I ToGlobalParentPosition(int globalX, int globalY, MapElementSize parentSize)
            => new(globalX >> parentSize.WidthExp, globalY >> parentSize.HeightExp);

        /// <summary>
        /// 根据尺寸指数，将全局坐标转换为父级全局坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2I ToGlobalParentPosition(Vector2I globalPosition, MapElementSize parentSize)
            => ToGlobalParentPosition(globalPosition.X, globalPosition.Y, parentSize);


        // ================================================================================
        //                                  ToLocalAndGlobalParentPosition
        // ================================================================================

        /// <summary>
        /// 根据尺寸指数，同时求出局部坐标与父级全局坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Vector2I localPosition, Vector2I parentPosition) ToLocalAndGlobalParentPosition(int globalX, int globalY, MapElementSize parentSize)
        {
            int parentX = globalX >> parentSize.WidthExp;
            int parentY = globalY >> parentSize.HeightExp;
            return (
                new Vector2I(globalX - (parentX << parentSize.WidthExp), globalY - (parentY << parentSize.HeightExp)),
                new Vector2I(parentX, parentY)
            );
        }

        /// <summary>
        /// 根据尺寸指数，同时求出局部坐标与父级全局坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Vector2I localPosition, Vector2I parentPosition) ToLocalAndGlobalParentPosition(Vector2I globalPosition, MapElementSize parentSize)
            => ToLocalAndGlobalParentPosition(globalPosition.X, globalPosition.Y, parentSize);


        // ================================================================================
        //                                  ToOriginGlobalChildPosition
        // ================================================================================

        /// <summary>
        /// 根据本级 base 坐标与尺寸，求出对应子级原点（左上角）的全局坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2I ToOriginGlobalChildPosition(int globalBaseX, int globalBaseY, MapElementSize size)
            => new(globalBaseX << size.WidthExp, globalBaseY << size.HeightExp);

        /// <summary>
        /// 根据本级 base 坐标与尺寸，求出对应子级原点（左上角）的全局坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2I ToOriginGlobalChildPosition(Vector2I globalBasePosition, MapElementSize size)
            => ToOriginGlobalChildPosition(globalBasePosition.X, globalBasePosition.Y, size);
        
    }
}
