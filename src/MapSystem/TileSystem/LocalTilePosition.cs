using Godot;
using System;
using WorldWeaver.MapSystem.ChunkSystem;
using WorldWeaver.MapSystem.LayerSystem;
using WorldWeaver.MapSystem.PositionConverter;

namespace WorldWeaver.MapSystem.TileSystem
{
    /// <summary>
    /// 局部 Tile 坐标，表示 Tile 在所属区块内的位置。
    /// </summary>
    public readonly struct LocalTilePosition : IPosition<LocalTilePosition>, IEquatable<LocalTilePosition>
    {
        // ================================================================================
        //                                  基本属性
        // ================================================================================

        public static LocalTilePosition Zero => new(0, 0);

        public int X { get; }

        public int Y { get; }


        // ================================================================================
        //                                  构造函数
        // ================================================================================

        public LocalTilePosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public LocalTilePosition(Vector2I position)
        {
            X = position.X;
            Y = position.Y;
        }

        public LocalTilePosition(long key)
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

        public LocalTilePosition Abs()
        {
            return new(Math.Abs(X), Math.Abs(Y));
        }

        public override string ToString()
        {
            return $"LocalTilePosition({X}, {Y})";
        }


        // ================================================================================
        //                                  相等性
        // ================================================================================

        public override bool Equals(object obj)
        {
            return obj is LocalTilePosition other && Equals(other);
        }

        public bool Equals(LocalTilePosition other)
        {
            return X == other.X && Y == other.Y;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public static bool operator ==(LocalTilePosition left, LocalTilePosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LocalTilePosition left, LocalTilePosition right)
        {
            return !left.Equals(right);
        }


        // ================================================================================
        //                                  坐标校验
        // ================================================================================

        public bool IsValid(Vector2I chunkSizeExp)
        {
            return new MapElementSize(chunkSizeExp).IsValidLocalPosition(X, Y);
        }

        public bool IsValid(MapLayer layer)
        {
            return layer.ChunkElementSize.IsValidLocalPosition(X, Y);
        }


        // ================================================================================
        //                                  坐标转换
        // ================================================================================

        public GlobalTilePosition ToGlobalTilePosition(Vector2I chunkSizeExp, ChunkPosition chunkPosition)
        {
            MapElementSize chunkElementSize = new(chunkSizeExp);
            Vector2I globalPosition = LocalPositionSizeExpConverter.ToGlobalPosition(
                X,
                Y,
                chunkPosition.X,
                chunkPosition.Y,
                chunkElementSize
            );
            return new(globalPosition);
        }

        public GlobalTilePosition ToGlobalTilePosition(MapLayer layer, ChunkPosition chunkPosition)
        {
            Vector2I globalPosition = LocalPositionSizeExpConverter.ToGlobalPosition(
                X,
                Y,
                chunkPosition.X,
                chunkPosition.Y,
                layer.ChunkElementSize
            );
            return new(globalPosition);
        }


        // ================================================================================
        //                                  索引转换
        // ================================================================================

        public int ToTileIndex(Vector2I chunkSizeExp)
        {
            return (Y << chunkSizeExp.X) + X;
        }

        public int ToTileIndex(int chunkWeightExp)
        {
            return (Y << chunkWeightExp) + X;
        }

        public int ToTileIndex(MapLayer layer)
        {
            return (Y << layer.ChunkSizeExp.X) + X;
        }
    }
}
