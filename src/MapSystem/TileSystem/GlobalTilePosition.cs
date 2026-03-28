using Godot;
using System;
using WorldWeaver.MapSystem.ChunkSystem;
using WorldWeaver.MapSystem.LayerSystem;
using WorldWeaver.MapSystem.PositionConverter;

namespace WorldWeaver.MapSystem.TileSystem
{
    /// <summary>
    /// 全局 Tile 坐标，表示 Tile 在世界中的绝对位置。
    /// </summary>
    public readonly struct GlobalTilePosition : IPosition<GlobalTilePosition>, IEquatable<GlobalTilePosition>
    {
        // ================================================================================
        //                                  基本属性
        // ================================================================================

        public static GlobalTilePosition Zero => new(0, 0);

        public int X { get; }

        public int Y { get; }


        // ================================================================================
        //                                  构造函数
        // ================================================================================

        public GlobalTilePosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public GlobalTilePosition(Vector2I position)
        {
            X = position.X;
            Y = position.Y;
        }

        public GlobalTilePosition(long key)
        {
            X = (int)(key >> 32);
            Y = (int)(key & 0xFFFFFFFFL);
        }


        // ================================================================================
        //                                  基础工具
        // ================================================================================

        public Vector2I ToVector2I()
        {
            return new(X, Y);
        }

        public long ToKey()
        {
            return (long)X << 32 | (uint)Y;
        }

        public GlobalTilePosition Abs()
        {
            return new(Math.Abs(X), Math.Abs(Y));
        }

        public override string ToString()
        {
            return $"GlobalTilePosition({X}, {Y})";
        }


        // ================================================================================
        //                                  相等性
        // ================================================================================

        public override bool Equals(object obj)
        {
            return obj is GlobalTilePosition other && Equals(other);
        }

        public bool Equals(GlobalTilePosition other)
        {
            return X == other.X && Y == other.Y;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public static bool operator ==(GlobalTilePosition left, GlobalTilePosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GlobalTilePosition left, GlobalTilePosition right)
        {
            return !left.Equals(right);
        }


        // ================================================================================
        //                                  坐标转换
        // ================================================================================

        public ChunkPosition ToChunkPosition(Vector2I chunkSizeExp)
        {
            MapElementSize chunkElementSize = new(chunkSizeExp);
            Vector2I chunkPosition = GlobalPositionSizeExpConverter.ToGlobalParentPosition(X, Y, chunkElementSize);
            return new(chunkPosition);
        }

        public ChunkPosition ToChunkPosition(MapLayer layer)
        {
            return ToChunkPosition(layer.ChunkSizeExp);
        }

        public LocalTilePosition ToLocalTilePosition(Vector2I chunkSizeExp, out ChunkPosition chunkPosition)
        {
            MapElementSize chunkElementSize = new(chunkSizeExp);
            (Vector2I localPosition, Vector2I parentPosition) =
                GlobalPositionSizeExpConverter.ToLocalAndGlobalParentPosition(X, Y, chunkElementSize);
            chunkPosition = new(parentPosition);
            return new(localPosition);
        }

        public LocalTilePosition ToLocalTilePosition(MapLayer layer, out ChunkPosition chunkPosition)
        {
            Vector2I parentPosition = GlobalPositionSizeExpConverter.ToGlobalParentPosition(X, Y, layer.ChunkElementSize);
            Vector2I localPosition = GlobalPositionSizeExpConverter.ToLocalPositionByMark(X, Y, layer.ChunkElementSize);
            chunkPosition = new(parentPosition);
            return new(localPosition);
        }

        public LocalTilePosition ToLocalTilePosition(Vector2I chunkSizeMask)
        {
            return new(X & chunkSizeMask.X, Y & chunkSizeMask.Y);
        }

        public LocalTilePosition ToLocalTilePosition(MapLayer layer)
        {
            return ToLocalTilePosition(layer.ChunkSizeMask);
        }
    }
}
