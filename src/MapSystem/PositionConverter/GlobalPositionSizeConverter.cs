using Godot;
using System.Runtime.CompilerServices;

namespace WorldWeaver.MapSystem.PositionConverter
{
    /// <summary>
    /// 全局坐标与尺寸转换器。
    /// <para>基于实际宽高执行坐标换算。</para>
    /// </summary>
    public static class GlobalPositionSizeConverter
    {
        // ================================================================================
        //                                  ToLocalPosition
        // ================================================================================

        /// <summary>
        /// 根据父级实际尺寸，将全局坐标转换为局部坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2I ToLocalPosition(int globalX, int globalY, MapElementSize parentSize)
            => new(FloorMod(globalX, parentSize.Width), FloorMod(globalY, parentSize.Height));

        /// <summary>
        /// 根据父级实际尺寸，将全局坐标转换为局部坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2I ToLocalPosition(Vector2I globalPosition, MapElementSize parentSize)
            => ToLocalPosition(globalPosition.X, globalPosition.Y, parentSize);


        // ================================================================================
        //                                  ToGlobalParentPosition
        // ================================================================================

        /// <summary>
        /// 根据父级实际尺寸，将全局坐标转换为父级全局坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2I ToGlobalParentPosition(int globalX, int globalY, MapElementSize parentSize)
            => new(FloorDiv(globalX, parentSize.Width), FloorDiv(globalY, parentSize.Height));

        /// <summary>
        /// 根据父级实际尺寸，将全局坐标转换为父级全局坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2I ToGlobalParentPosition(Vector2I globalPosition, MapElementSize parentSize)
            => ToGlobalParentPosition(globalPosition.X, globalPosition.Y, parentSize);


        // ================================================================================
        //                                  ToLocalAndGlobalParentPosition
        // ================================================================================

        /// <summary>
        /// 根据父级实际尺寸，同时求出局部坐标与父级全局坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Vector2I localPosition, Vector2I parentPosition) ToLocalAndGlobalParentPosition(int globalX, int globalY, MapElementSize parentSize)
        {
            (int parentX, int localX) = FloorDivWithRemainder(globalX, parentSize.Width);
            (int parentY, int localY) = FloorDivWithRemainder(globalY, parentSize.Height);
            return (new Vector2I(localX, localY), new Vector2I(parentX, parentY));
        }

        /// <summary>
        /// 根据父级实际尺寸，同时求出局部坐标与父级全局坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Vector2I localPosition, Vector2I parentPosition) ToLocalAndGlobalParentPosition(Vector2I globalPosition, MapElementSize parentSize)
            => ToLocalAndGlobalParentPosition(globalPosition.X, globalPosition.Y, parentSize);


        // ================================================================================
        //                                  ToOriginGlobalChildPosition
        // ================================================================================

        /// <summary>
        /// 根据本级全局坐标和尺寸，求出对应子级原点的全局坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2I ToOriginGlobalChildPosition(int globalParentX, int globalParentY, MapElementSize size)
            => new(globalParentX * size.Width, globalParentY * size.Height);

        /// <summary>
        /// 根据本级全局坐标和尺寸，求出对应子级原点的全局坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2I ToOriginGlobalChildPosition(Vector2I globalParentPosition, MapElementSize size)
            => ToOriginGlobalChildPosition(globalParentPosition.X, globalParentPosition.Y, size);
        

        // ================================================================================
        //                                  内部工具
        // ================================================================================

        /// <summary>
        /// 执行向下取整的整除。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FloorDiv(int value, int divisor)
        {
            int q = value / divisor;
            int r = value % divisor;
            return (r != 0 && value < 0) ? q - 1 : q;
        }

        /// <summary>
        /// 计算始终落在 `[0, divisor)` 范围内的模值。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FloorMod(int value, int divisor)
        {
            int r = value % divisor;
            return r < 0 ? r + divisor : r;
        }

        /// <summary>
        /// 同时计算向下取整的商与对应余数。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (int quotient, int remainder) FloorDivWithRemainder(int value, int divisor)
        {
            int q = value / divisor;
            int r = value % divisor;
            if (r < 0)
            {
                return (q - 1, r + divisor);
            }

            return (q, r);
        }
    }
}
