namespace WorldWeaver.MapSystem.ChunkSystem.State.Handler
{
    /// <summary>
    /// 正在加载到游戏状态处理器。
    /// <para>当前项目中区块进入游戏态的真实视觉接入由 <c>MapVisualLayer</c> 事件驱动完成。</para>
    /// <para>因此本处理器当前只负责做进入游戏态前的前置校验，确认区块已持有合法的内存数据。</para>
    /// </summary>
    public sealed class LoadingInGameHandler : StateHandler
    {
        // ================================================================================
        //                                  状态处理方法
        // ================================================================================

        /// <summary>
        /// 执行进入游戏态前的前置校验。
        /// </summary>
        public override StateExecutionResult Execute(ChunkManager manager, Chunk chunk)
        {
            if (!HandlerExecutionUtility.ValidateContext(manager, chunk, nameof(LoadingInGameHandler)))
            {
                return StateExecutionResult.PermanentFailure;
            }

            // 当前实现不直接创建游戏对象，仅要求区块已持有合法的内存数据。
            return HandlerExecutionUtility.ValidateLoadedChunkData(manager, chunk, nameof(LoadingInGameHandler))
                ? StateExecutionResult.Success
                : StateExecutionResult.PermanentFailure;
        }
    }
}
