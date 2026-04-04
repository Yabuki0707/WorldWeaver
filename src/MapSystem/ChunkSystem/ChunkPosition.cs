using Godot;
using System;
using WorldWeaver.MapSystem.GridSystem;
using WorldWeaver.MapSystem.LayerSystem;
using WorldWeaver.MapSystem.PositionConverter;
using WorldWeaver.MapSystem.TileSystem;

namespace WorldWeaver.MapSystem.ChunkSystem
{
    /// <summary>
    /// 区块位置，表示区块在世界中的坐标。
    /// </summary>
    public readonly struct ChunkPosition : IPosition<ChunkPosition>, IEquatable<ChunkPosition>
    {
        /*******************************
              基本属性与基础方法
        ********************************/

        /// <summary>
        /// 零点区块坐标。
        /// </summary>
        public static ChunkPosition Zero => new(0, 0);

        /// <summary>
        /// X 轴坐标。
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Y 轴坐标。
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// 根据坐标创建区块位置。
        /// </summary>
        public ChunkPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// 根据向量创建区块位置。
        /// </summary>
        public ChunkPosition(Vector2I position)
        {
            X = position.X;
            Y = position.Y;
        }

        /// <summary>
        /// 根据 long 键还原区块位置。
        /// </summary>
        public ChunkPosition(long key)
        {
            X = (int)(key >> 32);
            Y = (int)(key & 0xFFFFFFFFL);
        }

        /// <summary>
        /// 转换为 Vector2I。
        /// </summary>
        public Vector2I ToVector2I()
        {
            return new(X, Y);
        }

        /// <summary>
        /// 转换为 long 键。
        /// </summary>
        public long ToKey()
        {
            return (long)X << 32 | (uint)Y;
        }

        /// <summary>
        /// 返回绝对值坐标。
        /// </summary>
        public ChunkPosition Abs()
        {
            return new(Math.Abs(X), Math.Abs(Y));
        }

        /// <summary>
        /// 转换为字符串。
        /// </summary>
        public override string ToString()
        {
            return $"ChunkPosition({X}, {Y})";
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkPosition other && Equals(other);
        }

        public bool Equals(ChunkPosition other)
        {
            return X == other.X && Y == other.Y;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public static bool operator ==(ChunkPosition left, ChunkPosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChunkPosition left, ChunkPosition right)
        {
            return !left.Equals(right);
        }


        // ================================================================================
        //                                  坐标转换
        // ================================================================================

        /// <summary>
        /// 根据区块坐标与局部坐标还原全局 Tile 坐标。
        /// </summary>
        public Vector2I ToGlobalTilePosition(MapElementSize chunkSize, Vector2I localTilePosition)
        {
            return LocalTilePositionConverter.ToGlobalTilePosition(localTilePosition, chunkSize, this);
        }

        /// <summary>
        /// 根据图层配置还原全局 Tile 坐标。
        /// </summary>
        public Vector2I ToGlobalTilePosition(MapLayer layer, Vector2I localTilePosition)
        {
            return ToGlobalTilePosition(layer.ChunkSize, localTilePosition);
        }

        /// <summary>
        /// 获取区块原点对应的全局 Tile 坐标。
        /// </summary>
        public Vector2I GetOriginGlobalTilePosition(MapElementSize chunkSize)
        {
            return GlobalPositionSizeExpConverter.ToOriginGlobalChildPosition(X, Y, chunkSize);
        }

        /// <summary>
        /// 获取图层区块原点对应的全局 Tile 坐标。
        /// </summary>
        public Vector2I GetOriginGlobalTilePosition(MapLayer layer)
        {
            return GetOriginGlobalTilePosition(layer.ChunkSize);
        }

        /// <summary>
        /// 将区块坐标转换为网格坐标。
        /// </summary>
        public MapGridPosition ToGridPosition(MapElementSize gridSize)
        {
            Vector2I gridPosition = GlobalPositionSizeExpConverter.ToGlobalParentPosition(X, Y, gridSize);
            return new(gridPosition);
        }

        /// <summary>
        /// 将区块坐标转换为图层中的网格坐标。
        /// </summary>
        public MapGridPosition ToGridPosition(MapLayer layer)
        {
            return ToGridPosition(layer.GridSize);
        }
    }
}
