using Godot;

namespace WorldWeaver.MapSystem.ChunkSystem.Handler
{
    /// <summary>
    /// 状态处理执行结果。
    /// </summary>
    public enum StateExecutionResult
    {
        /// <summary>
        /// 执行成功，可推进状态。
        /// </summary>
        Success = 0,

        /// <summary>
        /// 临时失败，建议后续重试。
        /// </summary>
        RetryLater = 1,

        /// <summary>
        /// 永久失败，建议回退并阻塞当前路径。
        /// </summary>
        PermanentFailure = 2
    }

    /// <summary>
    /// 状态处理器抽象基类。
    /// <para>由 ChunkManager 调用，用于执行状态对应的副作用。</para>
    /// </summary>
    public abstract class StateHandler : System.Object
    {
        /// <summary>
        /// 校验当前 handler 执行所依赖对象的有效性。
        /// <para>该校验只覆盖执行入口必需对象，不承担具体状态数据合法性检查。</para>
        /// </summary>
        protected bool ValidateHandlerExecutionObjects(ChunkManager manager, Chunk chunk)
        {
            string handlerName = GetType().Name;

            if (manager == null)
            {
                GD.PushError($"[{handlerName}] 执行失败：manager 为 null。");
                return false;
            }

            if (manager.OwnerLayer == null)
            {
                GD.PushError($"[{handlerName}] 执行失败：manager.OwnerLayer 为 null。");
                return false;
            }

            if (Chunk.IsNullOrEmpty(chunk))
            {
                GD.PushError($"[{handlerName}] 执行失败：chunk 为 null 或 Empty。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 执行状态副作用。
        /// </summary>
        /// <param name="manager">驱动当前流程的 ChunkManager。</param>
        /// <param name="chunk">目标区块。</param>
        /// <returns>执行结果。</returns>
        public abstract StateExecutionResult Execute(ChunkManager manager, Chunk chunk);
    }
}
