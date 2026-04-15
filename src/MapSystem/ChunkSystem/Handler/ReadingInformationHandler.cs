using WorldWeaver.MapSystem.ChunkSystem.Data;

namespace WorldWeaver.MapSystem.ChunkSystem.Handler
{
    /// <summary>
    /// 正在读取信息状态处理器（同步）。
    /// <para>负责在主线程中阻塞式读取区块持久化数据。</para>
    /// <para>文件不存在是正常情况，此时保持 <see cref="Chunk.Data"/> 为 <see langword="null"/>，交由后续 <see cref="LoadingInMemoryHandler"/> 负责生成或初始化内存数据。</para>
    /// </summary>
    public sealed class ReadingInformationHandler : PersistenceStateHandler
    {
        // ================================================================================
        //                                  状态处理方法
        // ================================================================================

        /// <summary>
        /// 执行同步读取逻辑。
        /// </summary>
        public override StateExecutionResult Execute(ChunkManager manager, Chunk chunk)
        {
            if (!ValidateHandlerExecutionObjects(manager, chunk))
            {
                return StateExecutionResult.PermanentFailure;
            }

            // 若当前区块已经持有数据，则无需重复读取，直接视为成功。
            if (chunk.Data != null)
            {
                return StateExecutionResult.Success;
            }

            // 同步读取区块持久化储存对象。
            ChunkPersistence.PersistenceRequestResult requestResult =
                ChunkPersistence.LoadBlocking(manager.OwnerLayer, chunk, manager.OwnerLayer.StorageFilePath, out ChunkDataStorage loadedStorage);

            if (requestResult != ChunkPersistence.PersistenceRequestResult.Success)
            {
                return ToStateExecutionResult(requestResult);
            }

            // 文件不存在时 loadedStorage 为 null，属于正常情况，后续由内存加载阶段生成新数据。
            if (loadedStorage == null)
            {
                return StateExecutionResult.Success;
            }

            // 将储存对象转换为运行时 ChunkData，再挂载到当前区块并校验尺寸匹配。
            ChunkData loadedData = loadedStorage.ToData();
            if (loadedData == null)
            {
                return StateExecutionResult.PermanentFailure;
            }

            if (!chunk.InitializeValidChunkData(loadedData, manager.OwnerLayer.ChunkSize))
            {
                loadedData.Dispose();
                return StateExecutionResult.PermanentFailure;
            }

            return StateExecutionResult.Success;
        }
    }
}
