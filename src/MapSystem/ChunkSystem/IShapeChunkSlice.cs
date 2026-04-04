namespace WorldWeaver.MapSystem.ChunkSystem
{
    /// <summary>
    /// shape 区块切片访问接口。
    /// <para>返回 null 表示对应区块不存在或无可用 ChunkData。</para>
    /// </summary>
    internal interface IShapeChunkSlice
    {
        /// <summary>
        /// 获取指定区块坐标对应的 ChunkData。
        /// </summary>
        ChunkData GetChunkData(ChunkPosition chunkPosition);
    }
}
