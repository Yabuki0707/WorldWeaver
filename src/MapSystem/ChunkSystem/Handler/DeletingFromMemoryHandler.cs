namespace WorldWeaver.MapSystem.ChunkSystem.Handler
{
    /// <summary>
    /// 正在从内存删除状态处理器。
    /// <para>负责在区块离开内存前释放当前持有的 <see cref="ChunkData"/>。</para>
    /// <para>该阶段只处理内存层资源回收，不负责保存；保存逻辑应已在更前置的保存状态完成。</para>
    /// </summary>
    public sealed class DeletingFromMemoryHandler : StateHandler
    {
        // ================================================================================
        //                                  状态处理方法
        // ================================================================================

        /// <summary>
        /// 执行内存释放逻辑。
        /// </summary>
        public override StateExecutionResult Execute(ChunkManager manager, Chunk chunk)
        {
            if (!ValidateHandlerExecutionObjects(manager, chunk))
            {
                return StateExecutionResult.PermanentFailure;
            }

            // 当前阶段只释放区块持有的内存数据，允许空数据直接通过。
            chunk.ReleaseChunkData();
            return StateExecutionResult.Success;
        }
    }
}
