namespace WorldWeaver.MapSystem.ChunkSystem.Handler
{
    using WorldWeaver.MapSystem.ChunkSystem.Persistence;

    /// <summary>
    /// 正在保存信息状态处理器。
    /// <para>负责将当前 <see cref="Data.ChunkData"/> 提交到持久化缓存表，不直接写入磁盘。</para>
    /// <para>若区块当前没有内存数据，则直接视为无需提交并成功通过。</para>
    /// </summary>
    public sealed class SavingInformationHandler : PersistenceStateHandler
    {
        // ================================================================================
        //                                  状态处理方法
        // ================================================================================

        /// <summary>
        /// 执行缓存提交逻辑。
        /// </summary>
        public override StateExecutionResult Execute(ChunkManager manager, Chunk chunk)
        {
            if (!ValidateHandlerExecutionObjects(manager, chunk))
            {
                return StateExecutionResult.PermanentFailure;
            }

            // 保存状态只写入缓存，实际 Store 由缓存器后续清理周期延迟执行。
            PersistenceRequestResult requestResult = manager.PersistenceCache.TrySave(chunk);

            return ToStateExecutionResult(requestResult);
        }
    }
}
