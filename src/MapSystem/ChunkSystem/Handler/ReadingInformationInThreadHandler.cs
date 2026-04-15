using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;

namespace WorldWeaver.MapSystem.ChunkSystem.State.Handler
{
    /// <summary>
    /// 正在读取信息状态处理器（异步）。
    /// <para>负责通过结果轮询方式驱动后台加载任务。</para>
    /// <para>文件不存在是正常情况，此时保持 <see cref="Chunk.Data"/> 为 <see langword="null"/>，交由后续 <see cref="LoadingInMemoryHandler"/> 负责生成或初始化内存数据。</para>
    /// </summary>
    public sealed class ReadingInformationInThreadHandler : StateHandler
    {
        // ================================================================================
        //                                  状态处理方法
        // ================================================================================

        /// <summary>
        /// 执行异步读取逻辑。
        /// </summary>
        public override StateExecutionResult Execute(ChunkManager manager, Chunk chunk)
        {
            if (!HandlerExecutionUtility.ValidateContext(manager, chunk, nameof(ReadingInformationInThreadHandler)))
            {
                return StateExecutionResult.PermanentFailure;
            }

            // 若当前区块已经持有合法数据，则无需再发起异步读取。
            if (chunk.Data != null)
            {
                return HandlerExecutionUtility.ValidateLoadedChunkData(manager, chunk, nameof(ReadingInformationInThreadHandler))
                    ? StateExecutionResult.Success
                    : StateExecutionResult.PermanentFailure;
            }

            // 轮询异步读取结果；任务未完成时会返回 RetryLater。
            ChunkPersistence.PersistenceRequestResult requestResult =
                ChunkPersistence.TryLoadAsync(manager.OwnerLayer, chunk, manager.OwnerLayer.StorageFilePath, out ChunkData loadedData);

            if (requestResult != ChunkPersistence.PersistenceRequestResult.Success)
            {
                return HandlerExecutionUtility.ToStateExecutionResult(requestResult);
            }

            // 文件不存在时 loadedData 为 null，属于正常情况，后续由内存加载阶段生成新数据。
            if (loadedData == null)
            {
                return StateExecutionResult.Success;
            }

            // 将后台读取完成的数据挂载到区块上。
            if (!chunk.InitializeValidChunkData(loadedData, manager.OwnerLayer.ChunkSize))
            {
                loadedData.Dispose();
                return StateExecutionResult.PermanentFailure;
            }

            return StateExecutionResult.Success;
        }
    }
}
