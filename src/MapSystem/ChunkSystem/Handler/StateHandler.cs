using System;

namespace WorldWeaver.MapSystem.ChunkSystem.State.Handler
{
    /// <summary>
    /// 状态处理执行结果。
    /// </summary>
    public enum StateExecutionResult
    {
        /// <summary>
        /// 执行成功，可推进状态。
        /// </summary>
        Success = 0,

        /// <summary>
        /// 临时失败，建议后续重试。
        /// </summary>
        RetryLater = 1,

        /// <summary>
        /// 永久失败，建议回退并阻塞当前路径。
        /// </summary>
        PermanentFailure = 2
    }

    /// <summary>
    /// 状态处理器抽象基类。
    /// <para>由 ChunkManager 调用，用于执行状态对应的副作用。</para>
    /// </summary>
    public abstract class StateHandler : Object
    {
        /// <summary>
        /// 执行状态副作用。
        /// </summary>
        /// <param name="manager">驱动当前流程的 ChunkManager。</param>
        /// <param name="chunk">目标区块。</param>
        /// <returns>执行结果。</returns>
        public abstract StateExecutionResult Execute(ChunkManager manager, Chunk chunk);
    }
}
