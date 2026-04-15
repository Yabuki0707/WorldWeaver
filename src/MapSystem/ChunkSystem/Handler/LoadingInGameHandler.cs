namespace WorldWeaver.MapSystem.ChunkSystem.Handler
{
    /// <summary>
    /// 正在加载到游戏状态处理器。
    /// <para>当前项目中区块进入游戏态的真实视觉接入由 <c>MapVisualLayer</c> 事件驱动完成。</para>
    /// <para>因此本处理器当前只负责做进入游戏态前的执行对象校验，不直接创建游戏对象。</para>
    /// </summary>
    public sealed class LoadingInGameHandler : StateHandler
    {
        // ================================================================================
        //                                  状态处理方法
        // ================================================================================

        /// <summary>
        /// 执行进入游戏态前的占位逻辑。
        /// </summary>
        public override StateExecutionResult Execute(ChunkManager manager, Chunk chunk)
        {
            if (!ValidateHandlerExecutionObjects(manager, chunk))
            {
                return StateExecutionResult.PermanentFailure;
            }

            // 当前实现不直接创建游戏对象，视觉接入由 MapVisualLayer 事件链路负责。
            return StateExecutionResult.Success;
        }
    }
}
