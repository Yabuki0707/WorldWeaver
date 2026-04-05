using System;

namespace WorldWeaver.MapSystem.ChunkSystem.State.Handler
{
    /// <summary>
    /// 正在从内存删除状态处理器
    /// <para>负责处理区块数据从内存中卸载的过程。</para>
    /// <para>此状态为过渡态，执行内存清理和资源释放操作。</para>
    /// </summary>
    public sealed class DeletingFromMemoryHandler : StateHandler
    {
        public override StateExecutionResult Execute(ChunkManager manager, Chunk chunk)
        {
            // 占位实现：当前版本暂未接入真实的内存卸载流程，先返回成功。
            return StateExecutionResult.Success;
        }
    }
}
