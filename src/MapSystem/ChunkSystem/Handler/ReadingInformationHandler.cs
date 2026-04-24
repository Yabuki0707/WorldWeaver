using WorldWeaver.MapSystem.ChunkSystem.Data;
using WorldWeaver.MapSystem.ChunkSystem.Persistence;

namespace WorldWeaver.MapSystem.ChunkSystem.Handler
{
    /// <summary>
    /// 正在读取信息状态处理器。
    /// <para>负责通过持久化缓存器夺取区块储存信息；缓存未命中时只创建读取待办任务。</para>
    /// <para>文件不存在是正常情况，此时保持 <see cref="Chunk.Data"/> 为 <see langword="null"/>，交由后续 <see cref="LoadingInMemoryHandler"/> 负责生成或初始化内存数据。</para>
    /// </summary>
    public sealed class ReadingInformationHandler : PersistenceStateHandler
    {
        // ================================================================================
        //                                  状态处理方法
        // ================================================================================

        /// <summary>
        /// 执行缓存驱动的读取逻辑。
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

            // 优先夺取缓存；未命中时由缓存器创建 Read 待办任务并等待后续 tick 回收结果。
            PersistenceRequestResult requestResult =
                manager.PersistenceCache.TryTakeOut(chunk, out ChunkDataStorage loadedStorage);

            if (requestResult != PersistenceRequestResult.Success)
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
