namespace WorldWeaver.MapSystem.ChunkSystem.State.Handler
{
    /// <summary>
    /// 正在从游戏删除状态处理器。
    /// <para>当前项目中区块离开游戏态的可视化卸载由 <c>MapVisualLayer</c> 事件驱动完成。</para>
    /// <para>因此本处理器当前只负责做流程占位与上下文校验，不直接销毁额外的游戏对象。</para>
    /// </summary>
    public sealed class DeletingFromGameHandler : StateHandler
    {
        // ================================================================================
        //                                  状态处理方法
        // ================================================================================

        /// <summary>
        /// 执行离开游戏态的占位逻辑。
        /// </summary>
        public override StateExecutionResult Execute(ChunkManager manager, Chunk chunk)
        {
            if (!HandlerExecutionUtility.ValidateContext(manager, chunk, nameof(DeletingFromGameHandler)))
            {
                return StateExecutionResult.PermanentFailure;
            }

            // 当前没有额外的区块游戏对象需要在这里显式销毁，直接允许流程推进。
            return StateExecutionResult.Success;
        }
    }
}
