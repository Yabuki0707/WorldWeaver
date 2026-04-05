using Godot;
using System;
using System.Collections.Generic;
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
    public class TilesChangedEventArgs(TileValueShape tileValueShape, TileChangeType changeType) : EventArgs
    {
        /// <summary>
        /// 变化点形状与变化后的 TileRunId。
        /// <para>其中坐标使用全局坐标。</para>
        /// </summary>
        public TileValueShape TileValueShape { get; } = tileValueShape;

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
        /// 待更新区块集合，避免每帧遍历全部区块。
        /// </summary>
        private readonly HashSet<Chunk> _updatingChunks = [];

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

            // 同步写入索引表和更新集合，确保下一帧会参与状态驱动。
            _chunks[key] = createdChunk;
            _updatingChunks.Add(createdChunk);
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
            // 先从两处容器移除，再发事件，保证事件时刻状态一致。
            _updatingChunks.Remove(chunk);
            ChunkRemoved?.Invoke(chunk.CPosition);
            return true;
        }


        /*******************************
                  状态驱动
        ********************************/

        /// <summary>
        /// 驱动所有活跃区块的状态推进。
        /// <para>流程如下：</para>
        /// <para>1. 让 Chunk/State 先给出本轮是否存在目标节点与对应 handler；</para>
        /// <para>2. 由 Manager 执行 handler；</para>
        /// <para>3. 执行成功后，调用 Chunk.StateUpdate() 推进状态；</para>
        /// <para>4. 根据推进结果统一广播状态更新事件。</para>
        /// </summary>
        public void Update()
        {
            // 无活跃区块时直接退出，减少空帧开销。
            if (_updatingChunks.Count == 0)
            {
                return;
            }

            // 延迟删除列表：避免遍历中直接改动 _updatingChunks。
            List<Chunk> toRemove = [];
            foreach (Chunk chunk in _updatingChunks)
            {
                // 第一步：让 Chunk/State 给出“是否需要推进以及用哪个处理器”。
                StateHandler handler = chunk.GetStateUpdateHandler();

                // 约定：handler 为 null 时，表示本轮无更新需求。
                if (handler == null)
                {
                    continue;
                }

                // 第二步：由 Manager 执行处理器，不让 Chunk/State 自行执行业务副作用。
                StateExecutionResult executionResult = ExecuteStateHandler(chunk, handler);
                switch (executionResult)
                {
                    case StateExecutionResult.Success:
                        // 第三步：副作用成功后才提交状态推进。
                        ChunkStateUpdateResult updateResult = chunk.StateUpdate();
                        if (updateResult == null)
                        {
                            continue;
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

                        if (chunk.State.CurrentNode == ChunkStateNode.Exit)
                        {
                            // Exit 节点统一延迟到循环后批量移除。
                            toRemove.Add(chunk);
                        }

                        break;

                    case StateExecutionResult.PermanentFailure:
                    case StateExecutionResult.RetryLater:
                        // 失败分支交给 ChunkState 内部策略处理（回退/阻塞/等待重试）。
                        chunk.HandleStateExecutionFailure(executionResult);
                        break;
                }
            }

            // 统一在遍历结束后做删除，避免集合修改异常。
            foreach (Chunk chunk in toRemove)
            {
                RemoveChunk(chunk.CPosition);
            }
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
