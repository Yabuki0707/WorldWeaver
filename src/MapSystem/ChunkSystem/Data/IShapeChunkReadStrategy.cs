namespace WorldWeaver.MapSystem.ChunkSystem.Data
{
    /// <summary>
    /// Shape 区块读取策略接口。
    /// <para>该接口只负责回答一个问题：指定区块坐标当前应读取哪个 <see cref="ChunkData"/>。</para>
    /// <para>返回 <see langword="null"/> 表示对应区块不存在或无可用 ChunkData。</para>
    /// </summary>
    internal interface IShapeChunkReadStrategy
    {
        /// <summary>
        /// 获取指定区块坐标对应的 ChunkData。
        /// </summary>
        ChunkData GetChunkData(ChunkPosition chunkPosition);
    }
}
