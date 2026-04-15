using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;

namespace WorldWeaver.MapSystem.ChunkSystem.Handler
{
    /// <summary>
    /// 正在加载到内存状态处理器。
    /// <para>负责确保区块在进入内存稳定态前拥有合法的 <see cref="ChunkData"/>。</para>
    /// <para>若前序读取阶段没有加载到持久化数据，则会使用临时地形生成器基于区块全局范围和 <see cref="LayerSystem.MapLayer.WorldId"/> 生成初始 Tile 数据。</para>
    /// </summary>
    public sealed class LoadingInMemoryHandler : StateHandler
    {
        // ================================================================================
        //                                  状态处理方法
        // ================================================================================

        /// <summary>
        /// 执行内存加载逻辑。
        /// </summary>
        public override StateExecutionResult Execute(ChunkManager manager, Chunk chunk)
        {
            if (!ValidateHandlerExecutionObjects(manager, chunk))
            {
                return StateExecutionResult.PermanentFailure;
            }

            // 若区块已持有数据，则本阶段无需重复创建内存数据。
            if (chunk.Data != null)
            {
                return StateExecutionResult.Success;
            }

            // 根据区块左上角原点和 ChunkSize 计算本块覆盖的全局闭区间范围。
            Vector2I minGlobalPosition = chunk.CPosition.GetOriginGlobalTilePosition(manager.OwnerLayer);
            Vector2I maxGlobalPosition = new(
                minGlobalPosition.X + manager.OwnerLayer.ChunkSize.Width - 1,
                minGlobalPosition.Y + manager.OwnerLayer.ChunkSize.Height - 1);

            // 当前为简陋临时实现：若无持久化数据，则直接生成一块地形 TileRunId 数组。
            int[] generatedTileRunIds = TemporaryTerrainGenerator.GenerateTileRunIds(
                minGlobalPosition,
                maxGlobalPosition,
                manager.OwnerLayer.WorldId);

            if (generatedTileRunIds == null || generatedTileRunIds.Length != manager.OwnerLayer.ChunkSize.Area)
            {
                GD.PushError($"[LoadingInMemoryHandler] 区块 {chunk.Uid} 的临时地形生成结果非法，期望长度={manager.OwnerLayer.ChunkSize.Area}，实际长度={generatedTileRunIds?.Length ?? -1}。");
                return StateExecutionResult.PermanentFailure;
            }

            // 将生成结果封装为 ChunkData，并挂载到区块。
            ChunkData generatedData = new(manager.OwnerLayer.ChunkSize, generatedTileRunIds);
            if (!chunk.InitializeValidChunkData(generatedData, manager.OwnerLayer.ChunkSize))
            {
                generatedData.Dispose();
                return StateExecutionResult.PermanentFailure;
            }

            return StateExecutionResult.Success;
        }
    }
}
