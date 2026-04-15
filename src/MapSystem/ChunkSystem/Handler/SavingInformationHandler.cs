namespace WorldWeaver.MapSystem.ChunkSystem.State.Handler
{
    /// <summary>
    /// 正在保存信息状态处理器（同步）。
    /// <para>负责在主线程中阻塞式将当前 <see cref="ChunkData"/> 写入持久化层。</para>
    /// <para>若区块当前没有内存数据，则直接视为无需保存并成功通过。</para>
    /// </summary>
    public sealed class SavingInformationHandler : StateHandler
    {
        // ================================================================================
        //                                  状态处理方法
        // ================================================================================

        /// <summary>
        /// 执行同步保存逻辑。
        /// </summary>
        public override StateExecutionResult Execute(ChunkManager manager, Chunk chunk)
        {
            if (!HandlerExecutionUtility.ValidateContext(manager, chunk, nameof(SavingInformationHandler)))
            {
                return StateExecutionResult.PermanentFailure;
            }

            // 同步保存当前区块数据。
            ChunkPersistence.PersistenceRequestResult requestResult =
                ChunkPersistence.SaveBlocking(manager.OwnerLayer, chunk, manager.OwnerLayer.StorageFilePath);

            return HandlerExecutionUtility.ToStateExecutionResult(requestResult);
        }
    }
}
