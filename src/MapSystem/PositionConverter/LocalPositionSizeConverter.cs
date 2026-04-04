using Godot;
using System.Runtime.CompilerServices;

namespace WorldWeaver.MapSystem.PositionConverter
{
    /// <summary>
    /// 局部坐标与尺寸转换器。
    /// </summary>
    public static class LocalPositionSizeConverter
    {
        // ================================================================================
        //                                  基础属性
        // ================================================================================

        /// <summary>
        /// 局部坐标系原点。(左上角)
        /// </summary>
        public static Vector2I Origin => new(0, 0);


        // ================================================================================
        //                                  ToGlobalPosition
        // ================================================================================

        /// <summary>
        /// 根据父级坐标和实际尺寸，将局部坐标转换为全局坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2I ToGlobalPosition(int localX, int localY, int parentX, int parentY, MapElementSize size)
            => new(parentX * size.Width + localX, parentY * size.Height + localY);

        /// <summary>
        /// 根据父级坐标和实际尺寸，将局部坐标转换为全局坐标。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2I ToGlobalPosition(Vector2I localPosition, Vector2I parentPosition, MapElementSize size)
            => ToGlobalPosition(localPosition.X, localPosition.Y, parentPosition.X, parentPosition.Y, size);


        // ================================================================================
        //                                  ToTileIndex
        // ================================================================================

        /// <summary>
        /// 根据实际尺寸，将局部坐标转换为一维 Tile 索引。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToTileIndex(int localX, int localY, MapElementSize size)
            => (localY * size.Width) + localX;

        /// <summary>
        /// 根据实际尺寸，将局部坐标转换为一维 Tile 索引。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToTileIndex(Vector2I localPosition, MapElementSize size)
            => ToTileIndex(localPosition.X, localPosition.Y, size);

        
    }
}
