namespace WorldWeaver.MapSystem.ChunkSystem.Handler
{
    /// <summary>
    /// 持久化状态处理器抽象基类。
    /// <para>负责收敛持久化层请求结果到状态执行结果的转换逻辑。</para>
    /// </summary>
    public abstract class PersistenceStateHandler : StateHandler
    {
        /// <summary>
        /// 将持久化请求结果映射为状态执行结果。
        /// </summary>
        protected static StateExecutionResult ToStateExecutionResult(ChunkPersistence.PersistenceRequestResult requestResult)
        {
            return requestResult switch
            {
                ChunkPersistence.PersistenceRequestResult.Success => StateExecutionResult.Success,
                ChunkPersistence.PersistenceRequestResult.RetryLater => StateExecutionResult.RetryLater,
                ChunkPersistence.PersistenceRequestResult.PermanentFailure => StateExecutionResult.PermanentFailure,
                _ => StateExecutionResult.RetryLater
            };
        }
    }
}
