using System;

namespace WorldWeaver.MapSystem.ChunkSystem.State.Handler
{
    /// <summary>
    /// 正在加载到内存状态处理器
    /// <para>负责处理区块数据加载到内存的过程。</para>
    /// <para>此状态为过渡态，执行实际的内存加载操作。</para>
    /// </summary>
    public sealed class LoadingInMemoryHandler : StateHandler
    {
        public override StateExecutionResult Execute(Chunk chunk) => StateExecutionResult.Success;
    }
}
