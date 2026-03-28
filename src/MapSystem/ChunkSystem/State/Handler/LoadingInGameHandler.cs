using System;

namespace WorldWeaver.MapSystem.ChunkSystem.State.Handler
{
    /// <summary>
    /// 正在加载到游戏状态处理器
    /// <para>负责处理区块加载到游戏场景的过程。</para>
    /// <para>此状态为过渡态，执行游戏对象的实例化和场景构建。</para>
    /// </summary>
    public sealed class LoadingInGameHandler : StateHandler
    {
        public override StateExecutionResult Execute(Chunk chunk) => StateExecutionResult.Success;
    }
}
