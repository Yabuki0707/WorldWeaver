using System;

namespace rasu.Map.Chunk.State.Handlers
{
    /// <summary>
    /// 正在保存信息状态处理器（同步）
    /// <para>负责处理区块元数据的同步保存操作。</para>
    /// <para>此状态为过渡态，在主线程中阻塞式保存区块信息。</para>
    /// </summary>
    public sealed class SavingInformationHandler : StateHandler
    {
        public override bool? Execute(Chunk chunk) => true;
    }
}
