using Godot;
using System;
using rasu.Map.Chunk;
using rasu.Map.Layer;
using rasu.Map.Position;

namespace rasu.Map.Tile
{
    /// <summary>
    /// 全局Tile位置，表示Tile在世界中的全局坐标位置。
    /// 与局部Tile坐标（区块内的Tile坐标）相对，全局Tile坐标是唯一的。
    /// </summary>
    public readonly struct GlobalTilePosition : IPosition<GlobalTilePosition>
    {
        /*******************************
              基本属性与基本方法
        ********************************/

        /// <summary>
        /// 零点全局Tile坐标（0,0）
        /// </summary>
        public static GlobalTilePosition Zero => new(0, 0);

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
        public GlobalTilePosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// 构造函数：根据Vector2I坐标构造全局Tile位置
        /// </summary>
        /// <param name="position">全局Tile坐标向量</param>
        public GlobalTilePosition(Vector2I position)
        {
            X = position.X;
            Y = position.Y;
        }

        /// <summary>
        /// 从 long 类型的 key 构造全局Tile位置
        /// </summary>
        /// <param name="key">long 类型的 key（由 ToKey 方法生成）</param>
        public GlobalTilePosition(long key)
        {
            X = (int)(key >> 32);
            Y = (int)(key & 0xFFFFFFFFL);
        }

        /// <summary>
        /// 将全局Tile位置转换为 Vector2I 类型
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
        public GlobalTilePosition Abs()
        {
            return new(Math.Abs(X), Math.Abs(Y));
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        public override string ToString()
        {
            return $"GlobalTilePosition({X}, {Y})";
        }

        /// <summary>
        /// 检查是否相等
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is GlobalTilePosition other)
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
        public static bool operator ==(GlobalTilePosition left, GlobalTilePosition right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 检查是否不相等
        /// </summary>
        public static bool operator !=(GlobalTilePosition left, GlobalTilePosition right)
        {
            return !(left == right);
        }


        /*******************************
              坐标转换
        ********************************/

        /// <summary>
        /// 将全局Tile坐标转换为所属区块坐标
        /// </summary>
        /// <param name="chunkSizeExp">区块大小指数(2^Exp)，用于位运算</param>
        /// <returns>区块坐标</returns>
        public ChunkPosition ToChunkPosition(Vector2I chunkSizeExp)
        {
            // 借助位运算向下取整的特性实现区块坐标的转换
            int chunkX = X >> chunkSizeExp.X;
            int chunkY = Y >> chunkSizeExp.Y;
            return new(chunkX, chunkY);
        }

        /// <summary>
        /// 将全局Tile坐标转换为所属区块坐标
        /// 该方法可避免将 区块大小(ChunkSize) 与 区块大小指数(ChunkSizeExp) 混淆，因而较为推荐。
        /// </summary>
        /// <param name="layer">地图层，用于获取区块大小指数</param>
        /// <returns>区块坐标</returns>
        public ChunkPosition ToChunkPosition(MapLayer layer)
        {
            return ToChunkPosition(layer.ChunkSizeExp);
        }


        /// <summary>
        /// 将全局Tile坐标转换为区块内局部坐标
        /// </summary>
        /// <param name="chunkSizeExp">区块大小指数(2^Exp)，用于位运算</param>
        /// <param name="chunkPosition">输出参数，返回所属区块坐标</param>
        /// <returns>区块内局部坐标</returns>
        public LocalTilePosition ToLocalTilePosition(Vector2I chunkSizeExp,out ChunkPosition chunkPosition)
        {
            // 借助位运算向下取整的特性实现区块坐标的转换
            int chunkX = X >> chunkSizeExp.X;
            int chunkY = Y >> chunkSizeExp.Y;
            chunkPosition = new(chunkX, chunkY); 
            int localX = X-(chunkX<<chunkSizeExp.X);
            int localY = Y-(chunkY<<chunkSizeExp.Y);   
            return new(localX, localY);
        }

        /// <summary>
        /// 将全局Tile坐标转换为区块内局部坐标
        /// </summary>
        /// <param name="layer">地图层，用于获取区块大小指数</param>
        /// <param name="chunkPosition">输出参数，返回所属区块坐标</param>
        /// <returns>区块内局部坐标</returns>
        public LocalTilePosition ToLocalTilePosition(MapLayer layer,out ChunkPosition chunkPosition)
        {
            return ToLocalTilePosition(layer.ChunkSizeExp,out chunkPosition);
        }


        /// <summary>
        /// 将全局Tile坐标转换为区块内局部坐标
        /// </summary>
        /// <param name="chunkSizeMark">区块大小掩码，用于位运算</param>
        /// <returns>区块内局部坐标</returns>
        public LocalTilePosition ToLocalTilePosition(Vector2I chunkSizeMark)
        {
            //利用 2 的幂减一（如 2ⁿ−1）的二进制低 n 位全为 1 的特性，通过按位与（&）直接提取整数的低 n 位，从而高效计算模 2ⁿ 的余数（即区块内局部坐标），支持负数。
            int localX = X&chunkSizeMark.X;
            int localY = Y&chunkSizeMark.Y;   
            return new(localX, localY);
        }

        /// <summary>
        /// 将全局Tile坐标转换为区块内局部坐标
        /// </summary>
        /// <param name="layer">地图层，用于获取区块大小掩码</param>
        /// <returns>区块内局部坐标</returns>
        public LocalTilePosition ToLocalTilePosition(MapLayer layer)
        {
            return ToLocalTilePosition(layer.ChunkSizeMark);
        }

    }
}
