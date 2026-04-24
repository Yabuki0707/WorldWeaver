using System;

namespace WorldWeaver.MapSystem.ChunkSystem.Data
{
    /// <summary>
    /// 缓存区块范围读取器。
    /// <para>该策略会在构造阶段缓存指定区块范围内的 ChunkData，后续读取直接命中本地二维缓存。</para>
    /// </summary>
    internal sealed class CachedChunkRangeReader : IShapeChunkReadStrategy
    {
        // ================================================================================
        //                                  核心属性
        // ================================================================================

        /// <summary>
        /// 缓存范围内的最小区块坐标（包含端点）。
        /// </summary>
        public ChunkPosition MinChunkPosition { get; }

        /// <summary>
        /// 缓存范围内的最大区块坐标（包含端点）。
        /// </summary>
        public ChunkPosition MaxChunkPosition { get; }

        /// <summary>
        /// 以 [x,y] 形式组织的区块数据二维缓存。
        /// </summary>
        public ChunkData[,] ChunkDataRange { get; }


        // ================================================================================
        //                                  构造方法
        // ================================================================================

        /// <summary>
        /// 创建缓存区块范围读取器。
        /// </summary>
        public CachedChunkRangeReader(ChunkManager owner, ChunkPosition minChunkPosition, ChunkPosition maxChunkPosition)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            MinChunkPosition = minChunkPosition;
            MaxChunkPosition = maxChunkPosition;

            // 根据闭区间区块范围初始化二维缓存，因此宽高都需要 +1。
            int width = MaxChunkPosition.X - MinChunkPosition.X + 1;
            int height = MaxChunkPosition.Y - MinChunkPosition.Y + 1;
            ChunkDataRange = new ChunkData[width, height];

            // 填充缓存：存在且 Data 已加载的区块写入缓存。
            for (int rangeY = 0; rangeY < height; rangeY++)
            {
                for (int rangeX = 0; rangeX < width; rangeX++)
                {
                    // 根据缓存相对索引还原真实区块坐标。
                    ChunkPosition chunkPosition = new(MinChunkPosition.X + rangeX, MinChunkPosition.Y + rangeY);
                    Chunk chunk = owner.GetChunk(chunkPosition);
                    if (Chunk.IsNullOrEmpty(chunk) || chunk.Data == null)
                    {
                        continue;
                    }

                    ChunkDataRange[rangeX, rangeY] = chunk.Data;
                }
            }
        }


        // ================================================================================
        //                                  读取方法
        // ================================================================================

        /// <summary>
        /// 从二维缓存中读取指定区块的 ChunkData。
        /// </summary>
        public ChunkData GetChunkData(ChunkPosition chunkPosition)
        {
            // 将绝对区块坐标换算到缓存内的相对索引。
            int rangeX = chunkPosition.X - MinChunkPosition.X;
            int rangeY = chunkPosition.Y - MinChunkPosition.Y;

            return ChunkDataRange[rangeX, rangeY];
        }
    }
}
