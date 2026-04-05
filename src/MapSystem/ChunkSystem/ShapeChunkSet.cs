using System;

namespace WorldWeaver.MapSystem.ChunkSystem
{
    /// <summary>
    /// shape 区块集合访问器。
    /// <para>不缓存 chunk data，每次查询都直接调用 <see cref="ChunkManager.GetChunk"/>。</para>
    /// </summary>
    internal sealed class ShapeChunkSet : IShapeChunkSlice
    {
        /// <summary>
        /// 所属 chunk 管理器。
        /// </summary>
        private readonly ChunkManager _owner;

        /// <summary>
        /// 创建区块集合访问器。
        /// </summary>
        public ShapeChunkSet(ChunkManager owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>
        /// 直接查询指定区块的 ChunkData。
        /// </summary>
        public ChunkData GetChunkData(ChunkPosition chunkPosition)
        {
            Chunk chunk = _owner.GetChunk(chunkPosition);
            if (chunk == null || chunk == Chunk.EMPTY)
            {
                return null;
            }

            return chunk.Data;
        }
    }
}
