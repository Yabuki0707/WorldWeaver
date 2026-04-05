using System;

namespace WorldWeaver.MapSystem.ChunkSystem.State.Handler
{
    /// <summary>
    /// 正在保存信息状态处理器（异步）
    /// <para>负责处理区块元数据的异步保存操作。</para>
    /// <para>此状态为过渡态，在后台线程中非阻塞式保存区块信息。</para>
    /// </summary>
    public sealed class SavingInformationInThreadHandler : StateHandler
    {
        public override StateExecutionResult Execute(ChunkManager manager, Chunk chunk)
        {
            // 占位实现：当前版本暂未接入真实的异步保存流程，先返回成功。
            return StateExecutionResult.Success;
        }
    }
}
