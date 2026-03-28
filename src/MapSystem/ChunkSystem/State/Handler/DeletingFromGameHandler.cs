using System;

namespace WorldWeaver.MapSystem.ChunkSystem.State.Handler
{
    /// <summary>
    /// 正在从游戏删除状态处理器
    /// <para>负责处理区块从游戏场景中卸载的过程。</para>
    /// <para>此状态为过渡态，执行游戏对象的销毁和场景清理。</para>
    /// </summary>
    public sealed class DeletingFromGameHandler : StateHandler
    {
        public override StateExecutionResult Execute(Chunk chunk) => StateExecutionResult.Success;
    }
}
