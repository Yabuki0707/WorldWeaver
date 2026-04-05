using System;

namespace WorldWeaver.MapSystem.ChunkSystem.State.Handler
{
    /// <summary>
    /// 空状态处理器。
    /// <para>用于表达“当前存在状态推进需求，但该节点无需执行额外副作用”。</para>
    /// <para>执行结果恒为 <see cref="StateExecutionResult.Success"/>。</para>
    /// </summary>
    public sealed class EmptyStateHandler : StateHandler
    {
        /// <summary>
        /// 共享单例，避免重复分配。
        /// </summary>
        public static readonly EmptyStateHandler INSTANCE = new();

        /// <summary>
        /// 私有构造，强制使用单例。
        /// </summary>
        private EmptyStateHandler()
        {
        }

        /// <summary>
        /// 空处理器执行逻辑。
        /// <para>不做任何副作用，直接返回成功。</para>
        /// </summary>
        public override StateExecutionResult Execute(ChunkManager manager, Chunk chunk)
        {
            return StateExecutionResult.Success;
        }
    }
}
