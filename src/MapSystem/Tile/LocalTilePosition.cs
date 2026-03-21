using Godot;
using System;
using rasu.Map.Chunk;
using rasu.Map.Layer;
using rasu.Map.Position;


namespace rasu.Map.Tile
{
    /// <summary>
    /// 局部Tile位置，表示Tile在区块内的局部坐标位置。
    /// 与全局Tile坐标相对，局部Tile坐标仅在所属区块内有效。
    /// 注意：LocalTilePosition不负责验证坐标是否合法，只负责储存。
    /// </summary>
    public readonly struct LocalTilePosition : IPosition<LocalTilePosition>
    {
        /*******************************
              基本属性与基本方法
        ********************************/

        /// <summary>
        /// 零点局部Tile坐标（0,0）
        /// </summary>
        public static LocalTilePosition Zero => new(0, 0);

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
        public LocalTilePosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// 构造函数：根据Vector2I坐标构造局部Tile位置
        /// </summary>
        /// <param name="position">局部Tile坐标向量</param>
        public LocalTilePosition(Vector2I position)
        {
            X = position.X;
            Y = position.Y;
        }

        /// <summary>
        /// 从 long 类型的 key 构造局部Tile位置
        /// </summary>
        /// <param name="key">long 类型的 key（由 ToKey 方法生成）</param>
        public LocalTilePosition(long key)
        {
            X = (int)(key >> 32);
            Y = (int)(key & 0xFFFFFFFFL);
        }

        /// <summary>
        /// 将局部Tile位置转换为 Vector2I 类型
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
        public LocalTilePosition Abs()
        {
            return new(Math.Abs(X), Math.Abs(Y));
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        public override string ToString()
        {
            return $"LocalTilePosition({X}, {Y})";
        }

        /// <summary>
        /// 检查是否相等
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is LocalTilePosition other)
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
        public static bool operator ==(LocalTilePosition left, LocalTilePosition right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 检查是否不相等
        /// </summary>
        public static bool operator !=(LocalTilePosition left, LocalTilePosition right)
        {
            return !(left == right);
        }


        /*******************************
              坐标校验
        ********************************/

        /// <summary>
        /// 校验局部Tile坐标是否在区块范围内
        /// </summary>
        /// <param name="chunkSizeExp">区块大小指数(2^Exp)</param>
        /// <returns>如果坐标在有效范围内返回true，否则返回false</returns>
        public bool IsValid(Vector2I chunkSizeExp)
        {
            return (X >> chunkSizeExp.X) == 0 && (Y >> chunkSizeExp.Y) == 0;
        }

        /// <summary>
        /// 校验局部Tile坐标是否在区块范围内
        /// </summary>
        /// <param name="layer">地图层，用于获取区块大小指数</param>
        /// <returns>如果坐标在有效范围内返回true，否则返回false</returns>
        public bool IsValid(MapLayer layer)
        {
            return IsValid(layer.ChunkSizeExp);
        }


        /*******************************
              坐标转换
        ********************************/

        /// <summary>
        /// 将局部Tile位置转换为全局Tile位置
        /// </summary>
        /// <param name="chunkPosition">所属区块的位置</param>
        /// <param name="chunkSizeExp">区块大小的指数（如区块大小为16，则传入4）</param>
        /// <returns>全局Tile位置</returns>
        public GlobalTilePosition ToGlobalTilePosition(Vector2I chunkSizeExp, ChunkPosition chunkPosition)
        {
            int globalX = (chunkPosition.X << chunkSizeExp.X) + X;
            int globalY = (chunkPosition.Y << chunkSizeExp.Y) + Y;
            return new(globalX, globalY);
        }

        /// <summary>
        /// 将局部Tile位置转换为全局Tile位置
        /// 该方法可避免将 区块大小(ChunkSize) 与 区块大小指数(ChunkSizeExp) 混淆，因而较为推荐。
        /// </summary>
        /// <param name="chunkPosition">所属区块的位置</param>
        /// <param name="chunkManager">区块管理器，用于获取区块大小指数</param>
        /// <returns>全局Tile位置</returns>
        public GlobalTilePosition ToGlobalTilePosition(MapLayer layer, ChunkPosition chunkPosition)
        {
            return ToGlobalTilePosition(layer.ChunkSizeExp, chunkPosition);
        }


        /// <summary>
        /// 根据局部坐标获取 Tile 索引
        /// </summary>
        /// <param name="chunkSizeExp">区块大小指数(2^Exp)，用于位运算</param>
        /// <returns>Tile 索引</returns>
        public int ToTileIndex(Vector2I chunkSizeExp)
        {
            return (Y << chunkSizeExp.X) + X;
        }

        /// <summary>
        /// 根据局部坐标获取 Tile 索引
        /// </summary>
        /// <param name="chunkWeightExp">区块宽度指数(2^Exp)，用于位运算</param>
        /// <returns>Tile 索引</returns>
        public int ToTileIndex(int chunkWeightExp)
        {
            return (Y << chunkWeightExp) + X;
        }

        /// <summary>
        /// 根据局部坐标获取 Tile 索引
        /// 该方法可避免将 区块大小(ChunkSize) 与 区块大小指数(ChunkSizeExp) 混淆，因而较为推荐。
        /// </summary>
        /// <param name="chunkManager">区块管理器</param>
        /// <returns>Tile 索引</returns>
        public int ToTileIndex(MapLayer layer)
        {
            return (Y << layer.ChunkSizeExp.X) + X;
        }
    }
}
