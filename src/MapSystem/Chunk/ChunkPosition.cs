using Godot;
using System;
using rasu.Map;
using rasu.Map.Grid;
using rasu.Map.Layer;
using rasu.Map.Position;
using rasu.Map.Tile;


namespace rasu.Map.Chunk
{
    /// <summary>
    /// 区块位置，表示区块在世界中的坐标位置
    /// </summary>
    public readonly struct ChunkPosition : IPosition<ChunkPosition>
    {
        /*******************************
              基本属性与基本方法
        ********************************/

        /// <summary>
        /// 零点区块坐标（0,0）
        /// </summary>
        public static ChunkPosition Zero => new(0, 0);

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
        public ChunkPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// 构造函数：根据Vector2I坐标构造区块位置
        /// </summary>
        /// <param name="position">区块坐标向量</param>
        public ChunkPosition(Vector2I position)
        {
            X = position.X;
            Y = position.Y;
        }

        /// <summary>
        /// 从 long 类型的 key 构造区块位置
        /// </summary>
        /// <param name="key">long 类型的 key（由 ToKey 方法生成）</param>
        public ChunkPosition(long key)
        {
            X = (int)(key >> 32);
            Y = (int)(key & 0xFFFFFFFFL);
        }

        /// <summary>
        /// 将区块位置转换为 Vector2I 类型
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
        public ChunkPosition Abs()
        {
            return new(Math.Abs(X), Math.Abs(Y));
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        public override string ToString()
        {
            return $"ChunkPosition({X}, {Y})";
        }

        /// <summary>
        /// 检查是否相等
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is ChunkPosition other)
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
        public static bool operator ==(ChunkPosition left, ChunkPosition right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 检查是否不相等
        /// </summary>
        public static bool operator !=(ChunkPosition left, ChunkPosition right)
        {
            return !(left == right);
        }


        /*******************************
              坐标转换
        ********************************/

        /// <summary>
        /// 根据区块坐标与局部坐标还原全局 Tile 坐标
        /// </summary>
        /// <param name="chunkSizeExp">区块大小指数(2^Exp)，用于位运算</param>
        /// <param name="localTilePosition">区块内局部坐标</param>
        /// <returns>全局 Tile 坐标</returns>
        public GlobalTilePosition ToGlobalTilePosition(Vector2I chunkSizeExp,LocalTilePosition localTilePosition)
        {
            // 利用位运算将区块坐标左移对应指数位，再与局部坐标相加得到全局坐标
            int globalX = (X << chunkSizeExp.X) + localTilePosition.X;
            int globalY = (Y << chunkSizeExp.Y) + localTilePosition.Y;
            return new(globalX, globalY);
        }

        /// <summary>
        /// 根据区块坐标与局部坐标还原全局 Tile 坐标,要使用到区块管理器获取区块大小指数用于运算.
        /// 该方法可避免将 区块大小(ChunkSize) 与 区块大小指数(ChunkSizeExp) 混淆，因而较为推荐。
        /// </summary>
        /// <param name="layer">地图层，用于获取区块大小指数</param>
        /// <param name="localTilePosition">区块内局部坐标</param>
        /// <returns>全局 Tile 坐标</returns>
        public GlobalTilePosition ToGlobalTilePosition(MapLayer layer,LocalTilePosition localTilePosition)
        {
            return ToGlobalTilePosition(layer.ChunkSizeExp,localTilePosition);
        }


        /// <summary>
        /// 获取区块左上角原点的全局Tile坐标
        /// </summary>
        /// <param name="chunkSizeExp">区块大小指数(2^Exp)，用于位运算</param>
        /// <returns>区块左上角原点的全局Tile坐标</returns>
        public GlobalTilePosition GetOriginGlobalTilePosition(Vector2I chunkSizeExp)
        {
            return new(X << chunkSizeExp.X, Y << chunkSizeExp.Y);
        }

        /// <summary>
        /// 获取区块左上角原点的全局Tile坐标
        /// </summary>
        /// <param name="layer">地图层，用于获取区块大小指数</param>
        /// <returns>区块左上角原点的全局Tile坐标</returns>
        public GlobalTilePosition GetOriginGlobalTilePosition(MapLayer layer)
        {
            return GetOriginGlobalTilePosition(layer.ChunkSizeExp);
        }

        /// <summary>
        /// 将区块坐标转换为网格坐标
        /// </summary>
        /// <param name="gridSizeExp">网格大小指数(2^Exp)，用于位运算</param>
        /// <returns>网格坐标</returns>
        public MapGridPosition ToGridPosition(Vector2I gridSizeExp)
        {
            return new(X >> gridSizeExp.X, Y >> gridSizeExp.Y);
        }

        /// <summary>
        /// 将区块坐标转换为网格坐标
        /// </summary>
        /// <param name="layer">地图层，用于获取网格大小指数</param>
        /// <returns>网格坐标</returns>
        public MapGridPosition ToGridPosition(MapLayer layer)
        {
            return ToGridPosition(layer.GridSizeExp);
        }
    }
}
