namespace WorldWeaver.MapSystem.ChunkSystem.State.Handler
{
    /// <summary>
    /// 正在保存信息状态处理器（异步）。
    /// <para>负责通过结果轮询方式驱动后台保存任务。</para>
    /// <para>若区块当前没有内存数据，则直接视为无需保存并成功通过。</para>
    /// </summary>
    public sealed class SavingInformationInThreadHandler : StateHandler
    {
        // ================================================================================
        //                                  状态处理方法
        // ================================================================================

        /// <summary>
        /// 执行异步保存逻辑。
        /// </summary>
        public override StateExecutionResult Execute(ChunkManager manager, Chunk chunk)
        {
            if (!HandlerExecutionUtility.ValidateContext(manager, chunk, nameof(SavingInformationInThreadHandler)))
            {
                return StateExecutionResult.PermanentFailure;
            }

            // 轮询异步保存任务结果；任务未完成时返回 RetryLater。
            ChunkPersistence.PersistenceRequestResult requestResult =
                ChunkPersistence.TrySaveAsync(manager.OwnerLayer, chunk, manager.OwnerLayer.StorageFilePath);

            return HandlerExecutionUtility.ToStateExecutionResult(requestResult);
        }
    }
}
