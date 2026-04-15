using Godot;
using System;
using System.Collections.Generic;
using WorldWeaver.MapSystem.ChunkSystem.Data;
using WorldWeaver.MapSystem.ChunkSystem.State;
using WorldWeaver.MapSystem.ChunkSystem.State.Handler;
using WorldWeaver.MapSystem.LayerSystem;
using WorldWeaver.MapSystem.TileSystem;

namespace WorldWeaver.MapSystem.ChunkSystem
{
    /// <summary>
    /// Tile 变化事件参数。
    /// <para>事件统一使用 <see cref="TileValueShape"/> 承载变化点与变化后的 TileRunId。</para>
    /// </summary>
    public class TilesChangedEventArgs(TileValuesArrayShape tileValueShape, TileChangeType changeType) : EventArgs
    {
        /// <summary>
        /// 变化点形状与变化后的 TileRunId。
        /// <para>其中坐标使用全局坐标。</para>
        /// </summary>
        public TileValuesArrayShape TileValueShape { get; } = tileValueShape;

        /// <summary>
        /// 变化类型。
        /// </summary>
        public TileChangeType ChangeType { get; } = changeType;
    }

    /// <summary>
    /// Chunk 状态更新事件参数。
    /// <para>该事件表达“当前节点发生了一次成功推进”。</para>
    /// </summary>
    public class ChunkStateUpdatedEventArgs(
        ChunkPosition chunkPosition,
        ChunkStateNode previousNode,
        ChunkStateNode newNode,
        bool isNewNodeStable) : EventArgs
    {
        /// <summary>
        /// Chunk 坐标。
        /// </summary>
        public ChunkPosition ChunkPosition { get; } = chunkPosition;

        /// <summary>
        /// 更新前的当前节点。
        /// </summary>
        public ChunkStateNode PreviousNode { get; } = previousNode;

        /// <summary>
        /// 更新后的当前节点。
        /// </summary>
        public ChunkStateNode NewNode { get; } = newNode;

        /// <summary>
        /// 更新后的当前节点是否为稳定节点。
        /// </summary>
        public bool IsNewNodeStable { get; } = isNewNodeStable;
    }

    /// <summary>
    /// Chunk 状态到达稳定节点时的事件参数。
    /// </summary>
    public class ChunkStateStableReachedEventArgs(ChunkPosition chunkPosition, ChunkStateNode previousNode, ChunkStateNode newNode) : EventArgs
    {
        /// <summary>
        /// Chunk 坐标。
        /// </summary>
        public ChunkPosition ChunkPosition { get; } = chunkPosition;

        /// <summary>
        /// 变化前的稳定节点。
        /// </summary>
        public ChunkStateNode PreviousNode { get; } = previousNode;

        /// <summary>
        /// 变化后的稳定节点。
        /// </summary>
        public ChunkStateNode NewNode { get; } = newNode;
    }

    /// <summary>
    /// Chunk 管理器，负责区块的创建、索引、移除与状态驱动。
    /// <para>每个 MapLayer 拥有一个独立的 ChunkManager 实例。</para>
    /// <para>当前架构下，ChunkManager 是唯一的状态驱动者：Chunk/ChunkState 只负责暴露需求与提交结果。</para>
    /// </summary>
    public class ChunkManager
    {
        /*******************************
                  数据存储
        ********************************/

        /// <summary>
        /// 活跃区块字典。[Key(long) -> Chunk]
        /// </summary>
        private readonly Dictionary<long, Chunk> _chunks;

        /// <summary>
        /// 区块创建事件。
        /// </summary>
        public event Action<ChunkPosition> ChunkCreated;

        /// <summary>
        /// 区块移除事件。
        /// </summary>
        public event Action<ChunkPosition> ChunkRemoved;

        /// <summary>
        /// Tile 变化事件。
        /// </summary>
        public event EventHandler<TilesChangedEventArgs> TilesChanged;

        /// <summary>
        /// Chunk 状态更新事件。
        /// </summary>
        public event EventHandler<ChunkStateUpdatedEventArgs> ChunkStateUpdated;

        /// <summary>
        /// Chunk 状态到达稳定节点事件。
        /// </summary>
        public event EventHandler<ChunkStateStableReachedEventArgs> ChunkStateStableReached;


        /*******************************
                  属性
        ********************************/

        /// <summary>
        /// 所属的地图层实例。
        /// </summary>
        public readonly MapLayer OwnerLayer;

        /// <summary>
        /// 区块数据操作员。
        /// <para>负责全局 shape 到 ChunkData 索引操作的切片与执行。</para>
        /// </summary>
        public ChunkDataOperator DataOperator { get; }

        /// <summary>
        /// 区块加载请求处理器。
        /// <para>负责接收区块加载请求，并将请求项合并进更新表。</para>
        /// </summary>
        public ChunkLoadRequestProcessor LoadRequestProcessor { get; }


        /*******************************
                  构造
        ********************************/

        /// <summary>
        /// 创建 ChunkManager。
        /// </summary>
        public ChunkManager(MapLayer owner)
        {
            // Manager 生命周期绑定 Layer，构造时建立核心容器与数据操作入口。
            OwnerLayer = owner;
            _chunks = new Dictionary<long, Chunk>(128);
            DataOperator = new ChunkDataOperator(this);
            LoadRequestProcessor = new ChunkLoadRequestProcessor();
        }


        /*******************************
                  区块管理
        ********************************/

        /// <summary>
        /// 检查指定位置的区块是否存在。
        /// </summary>
        public bool HasChunk(ChunkPosition chunkPosition)
        {
            // 统一以 long key 做 O(1) 查询。
            long key = chunkPosition.ToKey();
            return _chunks.ContainsKey(key);
        }

        /// <summary>
        /// 获取指定区块；若不存在则返回 null。
        /// </summary>
        public Chunk GetChunk(ChunkPosition chunkPosition)
        {
            // 不存在时返回 null，调用方自行决定是否创建。
            long key = chunkPosition.ToKey();
            return _chunks.GetValueOrDefault(key);
        }

        /// <summary>
        /// 获取当前全部区块的调试快照。
        /// <para>该方法仅用于调试输出，返回当前区块集合的列表副本。</para>
        /// </summary>
        public List<Chunk> GetChunksDebugSnapshot()
        {
            return [.. _chunks.Values];
        }
        

        /// <summary>
        /// 创建并注册区块。
        /// </summary>
        public bool CreateChunk(ChunkPosition chunkPosition)
        {
            // 同坐标已存在则拒绝重复创建。
            long key = chunkPosition.ToKey();
            if (_chunks.ContainsKey(key))
            {
                return false;
            }

            // 先生成稳定 UID，再构造 Chunk。
            string uid = Chunk.GenerateUid(OwnerLayer.WorldId, OwnerLayer.LayerId, chunkPosition);
            Chunk createdChunk = new(chunkPosition, uid);

            // 同步写入索引表，后续状态驱动由本 tick 的更新表统一决定。
            _chunks[key] = createdChunk;
            ChunkCreated?.Invoke(chunkPosition);
            return true;
        }

        /// <summary>
        /// 移除指定区块。
        /// </summary>
        public bool RemoveChunk(ChunkPosition chunkPosition)
        {
            // 不存在时返回 false，避免对调用方隐藏删除失败。
            long key = chunkPosition.ToKey();
            if (!_chunks.Remove(key, out Chunk chunk))
            {
                return false;
            }

            // 删除成功后再发事件，保证事件回调观察到的是最新状态。
            ChunkRemoved?.Invoke(chunk.CPosition);
            return true;
        }

        /// <summary>
        /// 提交一份新的区块加载请求表。
        /// <para>请求表中的区块会立即并入更新表；若同一区块重复出现，则保留枚举值更大的稳定状态。</para>
        /// </summary>
        public void HandleChunkLoadRequests(ChunkLoadRequestTable requestTable)
        {
            LoadRequestProcessor.HandleRequestTable(requestTable);
        }


        /*******************************
                  状态驱动
        ********************************/

        /// <summary>
        /// 驱动当前更新表与现有区块集合对应的一轮状态推进。
        /// <para>流程如下：</para>
        /// <para>1. 先遍历现有区块：若更新表中存在该区块，则按更新表目标推进；否则按 <see cref="ChunkStateNode.Exit"/> 推进；</para>
        /// <para>2. 每处理完一个现有区块，都从更新表中移除它；</para>
        /// <para>3. 再遍历更新表中剩余项：这些区块当前尚不存在，因此先创建、再设定目标稳定节点、再推进一次状态；</para>
        /// <para>4. 所有当前状态已到达 <see cref="ChunkStateNode.Exit"/> 的区块统一在本轮末尾移除。</para>
        /// </summary>
        public void Update()
        {
            // 当更新表为空且当前也没有任何区块时，本轮没有可推进对象。
            if (LoadRequestProcessor.UpdateTable.IsEmpty && _chunks.Count == 0)
            {
                return;
            }

            // 延迟删除列表：避免遍历过程中直接改动 _chunks。
            List<ChunkPosition> toRemove = [];

            // 先处理当前已经存在于 _chunks 中的区块。
            foreach (Chunk chunk in new List<Chunk>(_chunks.Values))
            {
                // 若更新表里已经存在该区块，则按更新表目标推进；否则本轮目标默认为 Exit。
                ChunkStateNode targetStableNode = LoadRequestProcessor.UpdateTable.TryGetTargetStableNode(
                    chunk.CPosition,
                    out ChunkStateNode requestedStableNode)
                    ? requestedStableNode
                    : ChunkStateNode.Exit;
                
                // 获取现有区块的新状态后，把其对应更新项从更新表中移除。
                LoadRequestProcessor.UpdateTable.RemoveChunk(chunk.CPosition);
                
                // 现有区块统一显式刷新最终目标稳定节点。
                if (chunk.State.FinalStableNode != targetStableNode)
                    chunk.State.SetFinalStableNode(targetStableNode);
                // 若当前节点并非终点节点，则推进状态
                if (chunk.State.CurrentNode != chunk.State.FinalStableNode)
                    UpdateChunkState(chunk);
                // 若推进后到达 Exit，则登记到延迟删除列表。
                if (chunk.State.CurrentNode == ChunkStateNode.Exit)
                {
                    toRemove.Add(chunk.CPosition);
                }
            }

            // 再处理更新表里剩下的区块：这些区块当前一定还不存在，因此需要先创建。
            List<KeyValuePair<ChunkPosition, ChunkStateNode>> pendingCreateEntries = [.. LoadRequestProcessor.UpdateTable];
            foreach (KeyValuePair<ChunkPosition, ChunkStateNode> updateEntry in pendingCreateEntries)
            {
                // 先创建缺失区块，再按更新表写入最终目标稳定节点。
                CreateChunk(updateEntry.Key);
                Chunk createdChunk = GetChunk(updateEntry.Key);
                
                // 创建失败时本轮直接跳过，统一在循环结束后清空更新表。
                if (createdChunk == null)
                {
                    continue;
                }

                // 新建区块同样显式刷新最终目标稳定节点。
                if (createdChunk.State.FinalStableNode != updateEntry.Value)
                {
                    createdChunk.State.SetFinalStableNode(updateEntry.Value);
                }

                // 对新建区块执行一轮状态推进；若推进后到达 Exit，则登记到延迟删除列表。
                ChunkStateNode updatedStateNode = UpdateChunkState(createdChunk);
                if (updatedStateNode == ChunkStateNode.Exit)
                {
                    toRemove.Add(createdChunk.CPosition);
                }
            }

            // 剩余更新项处理完成后，统一清空更新表。
            LoadRequestProcessor.UpdateTable.Clear();

            // 统一在遍历结束后做删除，避免集合修改异常。
            foreach (ChunkPosition chunkPosition in toRemove)
            {
                RemoveChunk(chunkPosition);
            }
        }

        /// <summary>
        /// 对指定区块执行一轮状态推进。
        /// <para>该方法会直接驱动 <see cref="Chunk.State"/> 选择目标节点、执行处理器、提交状态，并返回推进后的当前状态节点。</para>
        /// </summary>
        private ChunkStateNode UpdateChunkState(Chunk chunk)
        {
            // 缺少状态对象或当前状态已经是 Exit 的区块无需再推进。
            if (chunk?.State == null || chunk.State.CurrentNode == ChunkStateNode.Exit)
            {
                return ChunkStateNode.Exit;
            }

            ChunkState state = chunk.State;

            // 若当前尚未准备好下一跳结果，则先向状态对象询问“下一步该去哪个节点”。
            // 这里由 Manager 直接驱动 State，不再经过 Chunk 的桥接包装方法。
            if (state.TargetNode == null && !state.SelectTargetNode())
            {
                return state.CurrentNode;
            }

            // 选路后依旧没有可推进的目标节点，说明本轮没有实际推进需求。
            if (state.TargetNode == null)
            {
                return state.CurrentNode;
            }

            // 处理器按“当前节点”获取；若节点未配置 handler，则使用空处理器让状态直接推进。
            // 这样可以保持“无副作用节点”也遵循统一的成功推进流程。
            StateHandler handler = ChunkStateMachine.GetHandler(state.CurrentNode) ?? EmptyStateHandler.INSTANCE;

            // 然后由 Manager 统一执行当前节点的状态处理器。
            StateExecutionResult executionResult = ExecuteStateHandler(chunk, handler);
            switch (executionResult)
            {
                case StateExecutionResult.Success:
                {
                    // 处理器成功后，才允许提交状态推进结果。
                    ChunkStateUpdateResult updateResult = state.UpdateToTargetNode();
                    if (updateResult == null)
                    {
                        return state.CurrentNode;
                    }

                    // 统一广播“节点推进成功”事件。
                    OnChunkStateUpdated(new ChunkStateUpdatedEventArgs(
                        chunk.CPosition,
                        updateResult.PreviousNode,
                        updateResult.NewNode,
                        updateResult.IsNewNodeStable));

                    // 若推进后进入了新稳定节点，再额外广播稳定节点事件。
                    if (updateResult.IsNewNodeStable &&
                        updateResult.PreviousStableNode.HasValue &&
                        updateResult.NewStableNode.HasValue &&
                        updateResult.PreviousStableNode.Value != updateResult.NewStableNode.Value)
                    {
                        OnChunkStateStableReached(new ChunkStateStableReachedEventArgs(
                            chunk.CPosition,
                            updateResult.PreviousStableNode.Value,
                            updateResult.NewStableNode.Value));
                    }

                    // 返回推进后的最新节点，由上层决定是否登记删除。
                    return updateResult.NewNode;
                }

                case StateExecutionResult.PermanentFailure:
                case StateExecutionResult.RetryLater:
                    // 失败分支交给 ChunkState 内部策略处理（回退/阻塞/等待重试）。
                    state.HandleExecutionFailure(executionResult);
                    return state.CurrentNode;
            }

            // 兜底返回当前节点，保证所有路径都有明确结果。
            return state.CurrentNode;
        }

        /// <summary>
        /// 执行区块当前节点对应的状态处理器。
        /// <para>若当前节点没有 handler，则视为“无需副作用即可直接推进”，返回 Success。</para>
        /// </summary>
        private StateExecutionResult ExecuteStateHandler(Chunk chunk, StateHandler handler)
        {
            // 这里的 handler 必非 null：为空需求会在上层提前 continue。
            return handler.Execute(this, chunk);
        }


        // ================================================================================
        //                                  Tile 查询
        // ================================================================================

        /// <summary>
        /// 检查指定全局 Tile 是否已加载。
        /// </summary>
        public bool IsTileLoaded(Vector2I globalTilePosition)
        {
            // 先把全局 tile 定位到所属 chunk，再判断该 chunk.Data 是否存在。
            ChunkPosition chunkPos = GlobalTilePositionConverter.ToChunkPosition(globalTilePosition, OwnerLayer.ChunkSize);
            long key = chunkPos.ToKey();

            if (_chunks.TryGetValue(key, out Chunk chunk))
            {
                return chunk.Data != null;
            }

            return false;
        }

        /// <summary>
        /// 获取指定全局 Tile 的信息；若未加载则返回 null。
        /// </summary>
        public int? GetTileInfo(Vector2I globalTilePosition)
        {
            // 全局 -> (chunk, local) 双坐标拆解。
            Vector2I localTilePosition =
                GlobalTilePositionConverter.ToLocalTilePosition(globalTilePosition, OwnerLayer.ChunkSize, out ChunkPosition chunkPosition);
            long key = chunkPosition.ToKey();
            if (!_chunks.TryGetValue(key, out Chunk chunk))
            {
                return null;
            }

            if (chunk.Data == null)
            {
                return null;
            }

            // 最终通过局部索引读取 tile 值。
            return chunk.Data.Tiles[LocalTilePositionConverter.ToTileIndex(localTilePosition, OwnerLayer.ChunkSize)];
        }


        /*******************************
                  事件转发
        ********************************/

        /// <summary>
        /// 触发 Tile 变化事件，供上层调用。
        /// </summary>
        internal void OnTilesChanged(TilesChangedEventArgs args)
        {
            // 事件只做转发，不在这里叠加业务逻辑。
            TilesChanged?.Invoke(this, args);
        }

        /// <summary>
        /// 触发 Chunk 状态更新事件。
        /// </summary>
        internal void OnChunkStateUpdated(ChunkStateUpdatedEventArgs args)
        {
            // 状态推进事件统一从 Manager 对外发出，保持单一出口。
            ChunkStateUpdated?.Invoke(this, args);
        }

        /// <summary>
        /// 触发 Chunk 状态稳定变化事件。
        /// </summary>
        internal void OnChunkStateStableReached(ChunkStateStableReachedEventArgs args)
        {
            // 稳定节点事件同样由 Manager 统一转发。
            ChunkStateStableReached?.Invoke(this, args);
        }
    }
}
