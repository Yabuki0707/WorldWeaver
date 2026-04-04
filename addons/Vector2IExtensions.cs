using Godot;

namespace WorldWeaver
{
    /// <summary>
    /// <see cref="Vector2I"/> 的扩展方法。
    /// </summary>
    public static class Vector2IExtensions
    {
        /// <summary>
        /// 将二维整数坐标压缩为 64 位键。
        /// <para>高 32 位存储 X，低 32 位存储 Y，可用于字典键或去重键。</para>
        /// </summary>
        public static long ToKey(this Vector2I point)
        {
            return ((long)point.X << 32) | (uint)point.Y;
        }
    }
}
