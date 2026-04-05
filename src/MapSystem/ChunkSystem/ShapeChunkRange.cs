using System;
using Godot;
using WorldWeaver.MapSystem.TileSystem;
using WorldWeaver.PixelShapeSystem;

namespace WorldWeaver.MapSystem.ChunkSystem
{
    /// <summary>
    /// shape 区块范围缓存。
    /// <para>构造阶段根据 shape 的坐标边界范围计算父级区块范围，并缓存对应 ChunkData 二维 range。</para>
    /// </summary>
    internal sealed class ShapeChunkRange : IShapeChunkSlice
    {
        /// <summary>
        /// 包围盒映射后的最小父级区块坐标（左上角）。
        /// </summary>
        public ChunkPosition MinChunkPosition { get; }

        /// <summary>
        /// 包围盒映射后的最大父级区块坐标（右下角）。
        /// </summary>
        public ChunkPosition MaxChunkPosition { get; }

        /// <summary>
        /// 以 [x,y] 形式组织的区块数据二维缓存。
        /// </summary>
        public ChunkData[,] ChunkDataRange { get; }

        /// <summary>
        /// 创建区块范围缓存并执行切分逻辑。
        /// </summary>
        public ShapeChunkRange(ChunkManager owner, PixelShape shape)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            if (shape.PointCount == 0)
            {
                MinChunkPosition = ChunkPosition.Zero;
                MaxChunkPosition = ChunkPosition.Zero;
                ChunkDataRange = new ChunkData[0, 0];
                return;
            }

            // 1) 根据 shape 的全局坐标边界范围计算父级区块范围（左上/右下）。
            Rect2I coordinateBounds = shape.CoordinateBounds;
            MinChunkPosition =
                GlobalTilePositionConverter.ToChunkPosition(coordinateBounds.Position, owner.OwnerLayer.ChunkSize);
            MaxChunkPosition =
                GlobalTilePositionConverter.ToChunkPosition(coordinateBounds.Position + coordinateBounds.Size, owner.OwnerLayer.ChunkSize);

            // 2) 按父级区块范围初始化二维缓存。
            int width = MaxChunkPosition.X - MinChunkPosition.X + 1;
            int height = MaxChunkPosition.Y - MinChunkPosition.Y + 1;
            ChunkDataRange = new ChunkData[width, height];

            // 3) 填充缓存：存在且 data 已加载的区块写入缓存。
            for (int rangeY = 0; rangeY < height; rangeY++)
            {
                for (int rangeX = 0; rangeX < width; rangeX++)
                {
                    ChunkPosition chunkPosition = new(MinChunkPosition.X + rangeX, MinChunkPosition.Y + rangeY);
                    Chunk chunk = owner.GetChunk(chunkPosition);
                    if (chunk == null || chunk == Chunk.EMPTY || chunk.Data == null)
                    {
                        continue;
                    }

                    ChunkDataRange[rangeX, rangeY] = chunk.Data;
                }
            }
        }

        /// <summary>
        /// 通过区块坐标获取缓存中的 ChunkData。
        /// </summary>
        public ChunkData GetChunkData(ChunkPosition chunkPosition)
        {
            int rangeX = chunkPosition.X - MinChunkPosition.X;
            int rangeY = chunkPosition.Y - MinChunkPosition.Y;

            if (rangeX < 0 ||
                rangeY < 0 ||
                rangeX >= ChunkDataRange.GetLength(0) ||
                rangeY >= ChunkDataRange.GetLength(1))
            {
                return null;
            }

            return ChunkDataRange[rangeX, rangeY];
        }
    }
}
