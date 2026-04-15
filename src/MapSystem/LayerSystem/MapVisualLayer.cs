using Godot;
using WorldWeaver.MapSystem.ChunkSystem;
using WorldWeaver.MapSystem.ChunkSystem.Data;
using WorldWeaver.MapSystem.ChunkSystem.State;
using WorldWeaver.MapSystem.TileSystem;

namespace WorldWeaver.MapSystem.LayerSystem
{
    /// <summary>
    /// 地图视觉层。
    /// <para>该类是 MapLayer 在 TileMapLayer 上的可视化适配层，负责订阅 ChunkManager 事件并将地图数据写入 Godot 的格子渲染层。</para>
    /// <para>整块进入游戏时执行整块渲染，整块退出游戏时执行整块清除；局部 Tile 变更则直接消费事件参数中的数据，不再回读 ChunkManager。</para>
    /// </summary>
    [GlobalClass]
    public partial class MapVisualLayer : TileMapLayer
    {
        // ================================================================================
        //                                  核心属性
        // ================================================================================

        /// <summary>
        /// 所属地图层。
        /// <para>优先使用显式设置的引用；若为空，则在进入树阶段尝试从父节点推断。</para>
        /// </summary>
        [Export]
        public MapLayer OwnerLayer { get; set; }

        /// <summary>
        /// 当前是否已完成事件订阅。
        /// </summary>
        private bool _isSubscribed;


        // ================================================================================
        //                                  生命周期方法
        // ================================================================================

        /// <summary>
        /// 进入场景树时尽早解析所属 MapLayer。
        /// </summary>
        public override void _EnterTree()
        {
            // 在进入树阶段尽早解析 OwnerLayer，给后续订阅事件做准备。
            ResolveOwnerLayer();
        }

        /// <summary>
        /// 在所有节点 Ready 完成后再执行事件订阅。
        /// </summary>
        public override void _Ready()
        {
            // 通过延迟调用确保父层的 _Ready 已先完成，避免 TheChunkManager 尚未初始化。
            CallDeferred(nameof(SubscribeToChunkManagerEvents));
        }

        /// <summary>
        /// 退出场景树时取消所有事件订阅。
        /// </summary>
        public override void _ExitTree()
        {
            // 节点退出树时必须解绑事件，避免无效节点继续收到回调。
            UnsubscribeFromChunkManagerEvents();
        }


        // ================================================================================
        //                                  绑定与订阅方法
        // ================================================================================

        /// <summary>
        /// 解析所属地图层。
        /// </summary>
        private void ResolveOwnerLayer()
        {
            // 已有显式引用时直接使用，不再重复推断。
            if (OwnerLayer != null)
            {
                return;
            }

            // 若父节点本身就是 MapLayer，则自动绑定到父层。
            if (GetParent() is MapLayer parentLayer)
            {
                OwnerLayer = parentLayer;
                return;
            }

            // 无法解析所属图层时给出警告，方便场景配置阶段排查问题。
            GD.PushWarning("[MapVisualLayer] 未显式设置 OwnerLayer，且父节点不是 MapLayer，无法自动绑定地图层。");
        }

        /// <summary>
        /// 订阅所属图层的 ChunkManager 事件。
        /// </summary>
        private void SubscribeToChunkManagerEvents()
        {
            // 已订阅时直接跳过，避免重复绑定同一组事件。
            if (_isSubscribed)
            {
                return;
            }

            // 先确保 OwnerLayer 已经被解析出来。
            ResolveOwnerLayer();

            // 只有在 Layer 和 ChunkManager 都就绪后，视觉层才能安全订阅事件。
            if (OwnerLayer?.TheChunkManager == null)
            {
                GD.PushWarning("[MapVisualLayer] 无法订阅事件：OwnerLayer 或 TheChunkManager 尚未准备完成。");
                return;
            }

            // 视觉层只关心两类事件：区块状态推进，以及 Tile 局部变更。
            OwnerLayer.TheChunkManager.TilesChanged += OnTilesChanged;
            OwnerLayer.TheChunkManager.ChunkStateUpdated += OnChunkStateUpdated;
            _isSubscribed = true;
        }

        /// <summary>
        /// 取消订阅所属图层的 ChunkManager 事件。
        /// </summary>
        private void UnsubscribeFromChunkManagerEvents()
        {
            // 未订阅或上下文已经失效时，不再尝试解绑。
            if (!_isSubscribed || OwnerLayer?.TheChunkManager == null)
            {
                return;
            }

            // 与订阅过程对称，逐项解除事件绑定。
            OwnerLayer.TheChunkManager.TilesChanged -= OnTilesChanged;
            OwnerLayer.TheChunkManager.ChunkStateUpdated -= OnChunkStateUpdated;
            _isSubscribed = false;
        }


        // ================================================================================
        //                                  事件处理方法
        // ================================================================================

        /// <summary>
        /// 处理区块状态推进事件。
        /// <para>1. 进入 LoadedInGame 时整块渲染；</para>
        /// <para>2. 进入 DeletingFromGame 时整块清除。</para>
        /// </summary>
        private void OnChunkStateUpdated(object sender, ChunkStateUpdatedEventArgs args)
        {
            // 视觉层依赖 ChunkManager 提供整块数据，因此管理器未就绪时直接跳过。
            if (OwnerLayer?.TheChunkManager == null)
            {
                return;
            }

            // 当前视觉层只关心会影响显示结果的两个状态节点。
            switch (args.NewNode)
            {
                case ChunkStateNode.LoadedInGame:
                {
                    // 区块进入游戏后，读取整块数据并一次性写入 TileMapLayer。
                    Chunk chunk = OwnerLayer.TheChunkManager.GetChunk(args.ChunkPosition);
                    if (chunk?.Data == null)
                    {
                        GD.PushWarning($"[MapVisualLayer] 区块 {args.ChunkPosition} 进入 LoadedInGame，但未找到可渲染的 ChunkData。");
                        return;
                    }

                    RenderChunk(chunk.Data, args.ChunkPosition);
                    break;
                }

                case ChunkStateNode.DeletingFromGame:
                    // 区块准备退出游戏时，直接清空该区块对应的整块可视范围。
                    ClearChunk(args.ChunkPosition);
                    break;
            }
        }

        /// <summary>
        /// 处理 Tile 局部变更事件。
        /// <para>局部刷新直接消费事件里提供的 TileValueShape 与变更类型，不再逐点回读 ChunkManager。</para>
        /// </summary>
        private void OnTilesChanged(object sender, TilesChangedEventArgs args)
        {
            // 没有有效变化数据时不触发局部刷新。
            if (args?.TileValueShape == null)
            {
                return;
            }

            // 按操作类型执行最直接的局部刷新逻辑，避免额外查询管理器。
            RenderTileShape(args.TileValueShape, args.ChangeType);
        }


        // ================================================================================
        //                                  区块渲染方法
        // ================================================================================

        /// <summary>
        /// 根据整块数据渲染整个区块。
        /// </summary>
        public void RenderChunk(ChunkData chunkData, ChunkPosition chunkPosition)
        {
            // 缺少图层上下文或区块数据时，无法完成整块渲染。
            if (OwnerLayer == null || chunkData == null)
            {
                return;
            }
            Vector2I originGlobalTilePosition = chunkPosition.GetOriginGlobalTilePosition(OwnerLayer);
            // 双层循环遍历区块内全部局部坐标，并将每个 Tile 写入对应的全局格子。
            for (int localY = 0; localY < chunkData.Height; localY++)
            {
                for (int localX = 0; localX < chunkData.Width; localX++)
                {
                    // 先得到区块内的局部 Tile 坐标。
                    Vector2I tilePosition = new Vector2I(localX, localY);
                    // 当前格子的 TileRunId 直接来自区块数据。
                    int tileRunId = chunkData.GetTile(tilePosition) ?? 0;
                    tilePosition +=originGlobalTilePosition;
                    ApplyTileToCell(tilePosition, tileRunId);
                }
            }
        }

        /// <summary>
        /// 清除整个区块在可视层中的所有格子。
        /// </summary>
        public void ClearChunk(ChunkPosition chunkPosition)
        {
            // 没有所属图层时无法确定区块尺寸，因此不能执行整块清除。
            if (OwnerLayer == null)
            {
                return;
            }
            Vector2I originGlobalTilePosition = chunkPosition.GetOriginGlobalTilePosition(OwnerLayer);
            // 双层循环遍历区块内全部局部坐标，并将每个 Tile 写入对应的全局格子。
            for (int localY = 0; localY < OwnerLayer.ChunkSize.Height; localY++)
            {
                for (int localX = 0; localX < OwnerLayer.ChunkSize.Height; localX++)
                {
                    // 先得到全局 Tile 坐标。
                    Vector2I globalTilePosition = new Vector2I(localX, localY)+originGlobalTilePosition;
                    EraseCell(globalTilePosition);
                }
            }
        }


        // ================================================================================
        //                                  Shape渲染方法
        // ================================================================================

        /// <summary>
        /// 根据局部变更类型刷新 shape 命中的 Tile。
        /// <para>Set / Restore 直接使用 TileValueShape 中的 runId。</para>
        /// <para>Remove / Delete 直接将命中格子清空。</para>
        /// </summary>
        public void RenderTileShape(TileValuesArrayShape tileValueShape, TileChangeType changeType)
        {
            // 没有有效变更形状时无需处理。
            if (tileValueShape == null)
            {
                return;
            }

            // 按变更类型走不同的局部刷新路径，避免把所有操作都退化成回读查询。
            switch (changeType)
            {
                case TileChangeType.Set:
                case TileChangeType.Restore:
                    RenderTileShapeByValues(tileValueShape);
                    break;

                case TileChangeType.Remove:
                case TileChangeType.Delete:
                    ClearTileShape(tileValueShape);
                    break;
            }
        }

        /// <summary>
        /// 使用 TileValueShape 中自带的 runId 直接刷新命中的格子。
        /// </summary>
        private void RenderTileShapeByValues(TileValuesArrayShape tileValueShape)
        {
            // Set / Restore 的事件结果已经携带最终 runId，这里按点序直接写入即可。
            foreach ((Vector2I globalTilePosition, int valueIndex) in tileValueShape.GetGlobalValueIndexIterator())
            {
                // 通过索引访问与坐标严格对齐的 runId，避免额外查找和中间映射。
                int tileRunId = tileValueShape.TileRunIds[valueIndex];
                ApplyTileToCell(globalTilePosition, tileRunId);
            }
        }

        /// <summary>
        /// 将 shape 命中的格子直接清空。
        /// </summary>
        private void ClearTileShape(TileValuesArrayShape tileValueShape)
        {
            // Remove / Delete 不需要读取值，直接对命中的所有全局坐标执行清除即可。
            foreach (Vector2I globalTilePosition in tileValueShape.Shape.GetGlobalCoordinateIterator())
            {
                ApplyTileToCell(globalTilePosition, 0);
            }
        }


        // ================================================================================
        //                                  Tile应用方法
        // ================================================================================

        /// <summary>
        /// 将 TileRunId 应用到 TileMapLayer 的单元格。
        /// <para>runId 小于等于 0 时表示禁用渲染，直接清除格子；runId 大于 0 时先换算成 atlas 坐标再写入。</para>
        /// <para>当前统一使用 sourceId=0 的 TileSet 源。</para>
        /// </summary>
        private void ApplyTileToCell(Vector2I globalTilePosition, int tileRunId)
        {
            // runId 小于等于 0 时统一视为不渲染，对应的可视结果就是清空当前格子。
            if (tileRunId <= 0)
            {
                EraseCell(globalTilePosition);
                return;
            }

            // 正常可见 Tile 先由 runId 找到类型，再取出真正用于渲染的 atlas 坐标。
            Vector2I atlasCoordinates = TileTypeManager.GetTypeByRunId(tileRunId).TileTypeTextureId;
            SetCell(globalTilePosition, 0, atlasCoordinates);
        }
    }
}
