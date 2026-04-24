namespace WorldWeaver.MapSystem.ChunkSystem.Handler
{
    using WorldWeaver.MapSystem.ChunkSystem.Persistence;

    /// <summary>
    /// 持久化状态处理器抽象基类。
    /// <para>负责收敛持久化层请求结果到状态执行结果的转换逻辑。</para>
    /// </summary>
    public abstract class PersistenceStateHandler : StateHandler
    {
        /// <summary>
        /// 将持久化请求结果映射为状态执行结果。
        /// </summary>
        protected static StateExecutionResult ToStateExecutionResult(PersistenceRequestResult requestResult)
        {
            return requestResult switch
            {
                PersistenceRequestResult.Success => StateExecutionResult.Success,
                PersistenceRequestResult.RetryLater => StateExecutionResult.RetryLater,
                PersistenceRequestResult.PermanentFailure => StateExecutionResult.PermanentFailure,
                _ => StateExecutionResult.RetryLater
            };
        }
    }
}
