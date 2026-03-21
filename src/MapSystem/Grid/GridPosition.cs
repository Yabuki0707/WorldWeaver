using Godot;
using System;
using rasu.Map.Position;


namespace rasu.Map.Grid
{
    /// <summary>
    /// 地图网格位置，表示网格在世界中的坐标位置
    /// </summary>
    public readonly struct MapGridPosition : IPosition<MapGridPosition>
    {
        /*******************************
              基本属性与基本方法
        ********************************/

        /// <summary>
        /// 零点网格坐标（0,0）
        /// </summary>
        public static MapGridPosition Zero => new(0, 0);

        /// <summary>
        /// 获取X轴坐标
        /// </summary>
        public readonly int X { get; }

        /// <summary>
        /// 获取Y轴坐标
        /// </summary>
        public readonly int Y { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="x">X轴坐标</param>
        /// <param name="y">Y轴坐标</param>
        public MapGridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// 构造函数：根据Vector2I坐标构造网格位置
        /// </summary>
        /// <param name="position">网格坐标向量</param>
        public MapGridPosition(Vector2I position)
        {
            X = position.X;
            Y = position.Y;
        }

        /// <summary>
        /// 从 long 类型的 key 构造网格位置
        /// </summary>
        /// <param name="key">long 类型的 key（由 ToKey 方法生成）</param>
        public MapGridPosition(long key)
        {
            X = (int)(key >> 32);
            Y = (int)(key & 0xFFFFFFFFL);
        }

        /// <summary>
        /// 将网格位置转换为 Vector2I 类型
        /// </summary>
        /// <returns>Vector2I 类型的坐标</returns>
        public Vector2I ToVector2I()
        {
            return new(X, Y);
        }

        /// <summary>
        /// 将坐标变为 long 类型的 key 作为字典的键
        /// </summary>
        /// <returns>long 类型的 key</returns>
        public long ToKey()
        {
            return (long)X << 32 | (uint)Y;
        }

        /// <summary>
        /// 返回坐标绝对值的新实例
        /// </summary>
        /// <returns>坐标绝对值的新实例</returns>
        public MapGridPosition Abs()
        {
            return new(Math.Abs(X), Math.Abs(Y));
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        public override string ToString()
        {
            return $"MapGridPosition({X}, {Y})";
        }

        /// <summary>
        /// 检查是否相等
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is MapGridPosition other)
            {
                return X == other.X && Y == other.Y;
            }
            return false;
        }

        /// <summary>
        /// 获取哈希码
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        /// <summary>
        /// 检查是否相等
        /// </summary>
        public static bool operator ==(MapGridPosition left, MapGridPosition right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 检查是否不相等
        /// </summary>
        public static bool operator !=(MapGridPosition left, MapGridPosition right)
        {
            return !(left == right);
        }
    }
}
