using System;

namespace WorldWeaver.MapSystem.ChunkSystem.Data
{
    /// <summary>
    /// 实时区块读取器。
    /// <para>该策略不缓存 ChunkData，每次读取都实时访问 <see cref="ChunkManager"/>。</para>
    /// </summary>
    internal sealed class LiveChunkReader : IShapeChunkReadStrategy
    {
        // ================================================================================
        //                                  核心属性
        // ================================================================================

        /// <summary>
        /// 所属 chunk 管理器。
        /// </summary>
        private readonly ChunkManager _owner;


        // ================================================================================
        //                                  构造方法
        // ================================================================================

        /// <summary>
        /// 创建实时区块读取器。
        /// </summary>
        public LiveChunkReader(ChunkManager owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }


        // ================================================================================
        //                                  读取方法
        // ================================================================================

        /// <summary>
        /// 实时查询指定区块的 ChunkData。
        /// </summary>
        public ChunkData GetChunkData(ChunkPosition chunkPosition)
        {
            // 每次读取都直接访问 ChunkManager，保持实时性。
            Chunk chunk = _owner.GetChunk(chunkPosition);
            return chunk?.Data;
        }
    }
}
