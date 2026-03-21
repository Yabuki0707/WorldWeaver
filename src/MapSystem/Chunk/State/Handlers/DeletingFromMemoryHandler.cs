using System;

namespace rasu.Map.Chunk.State.Handlers
{
    /// <summary>
    /// 正在从内存删除状态处理器
    /// <para>负责处理区块数据从内存中卸载的过程。</para>
    /// <para>此状态为过渡态，执行内存清理和资源释放操作。</para>
    /// </summary>
    public sealed class DeletingFromMemoryHandler : StateHandler
    {
        public override bool? Execute(Chunk chunk) => true;
    }
}
