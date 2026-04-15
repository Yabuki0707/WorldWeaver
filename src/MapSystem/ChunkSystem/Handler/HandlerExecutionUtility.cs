using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;

namespace WorldWeaver.MapSystem.ChunkSystem.State.Handler
{
    /// <summary>
    /// Handler 执行辅助工具。
    /// <para>负责收敛公共上下文校验、ChunkData 校验以及持久化结果到状态执行结果的映射。</para>
    /// </summary>
    internal static class HandlerExecutionUtility
    {
        /// <summary>
        /// 校验当前 handler 的执行上下文。
        /// </summary>
        public static bool ValidateContext(ChunkManager manager, Chunk chunk, string handlerName)
        {
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

            if (chunk == null)
            {
                GD.PushError($"[{handlerName}] 执行失败：chunk 为 null。");
                return false;
            }

            if (chunk == Chunk.EMPTY)
            {
                GD.PushError($"[{handlerName}] 执行失败：chunk 为 Empty。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 校验当前区块是否持有与图层尺寸一致的 ChunkData。
        /// </summary>
        public static bool ValidateLoadedChunkData(ChunkManager manager, Chunk chunk, string handlerName)
        {
            if (chunk.Data == null)
            {
                GD.PushError($"[{handlerName}] 执行失败：区块 {chunk.Uid} 当前没有可用的 ChunkData。");
                return false;
            }

            if (chunk.Data.ElementSize != manager.OwnerLayer.ChunkSize)
            {
                GD.PushError($"[{handlerName}] 执行失败：区块 {chunk.Uid} 的 ChunkData 尺寸与图层 ChunkSize 不一致。期望={manager.OwnerLayer.ChunkSize}，实际={chunk.Data.ElementSize}。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 将持久化结果映射为状态执行结果。
        /// </summary>
        public static StateExecutionResult ToStateExecutionResult(ChunkPersistence.PersistenceRequestResult requestResult)
        {
            return requestResult switch
            {
                ChunkPersistence.PersistenceRequestResult.Success => StateExecutionResult.Success,
                ChunkPersistence.PersistenceRequestResult.RetryLater => StateExecutionResult.RetryLater,
                ChunkPersistence.PersistenceRequestResult.PermanentFailure => StateExecutionResult.PermanentFailure,
                _ => StateExecutionResult.RetryLater
            };
        }
    }
}
