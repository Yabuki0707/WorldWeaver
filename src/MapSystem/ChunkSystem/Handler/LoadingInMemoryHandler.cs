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
        public override StateExecutionResult Execute(ChunkManager manager, Chunk chunk)
        {
            // 占位实现：当前版本暂未接入真实的内存加载流程，先返回成功。
            return StateExecutionResult.Success;
        }
    }
}
