using Godot;
using System;
using System.Collections.Generic;
using WorldWeaver.MapSystem.ChunkSystem.State;
using WorldWeaver.MapSystem.LayerSystem;
using WorldWeaver.MapSystem.TileSystem;
using WorldWeaver.PixelShapeSystem;

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
    /// </summary>
    public class ChunkManager
    {
        /*******************************
                  数据存储
        ********************************/

        /// <summary>
        /// 活跃区块字典。[Key(long) -> Chunk]
        /// </summary>
        private readonly Dictionary<Int128, Chunk> _chunks;

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
            OwnerLayer = owner;
            _chunks = new(128);
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
            long key = chunkPosition.ToKey();
            return _chunks.ContainsKey(key);
        }

        /// <summary>
        /// 获取指定区块；若不存在则返回 null。
        /// </summary>
        public Chunk GetChunk(ChunkPosition chunkPosition)
        {
            long key = chunkPosition.ToKey();
            return _chunks.TryGetValue(key, out Chunk chunk) ? chunk : null;
        }

        /// <summary>
        /// 创建并注册区块。
        /// </summary>
        public bool CreateChunk(ChunkPosition chunkPosition)
        {
            long key = chunkPosition.ToKey();
            if (_chunks.ContainsKey(key))
            {
                return false;
            }

            Chunk createdChunk = new(this, chunkPosition);
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
            long key = chunkPosition.ToKey();
            if (!_chunks.TryGetValue(key, out Chunk chunk))
            {
                return false;
            }

            _chunks.Remove(key);
            _updatingChunks.Remove(chunk);
            ChunkRemoved?.Invoke(chunk.CPosition);
            return true;
        }


        /*******************************
                  状态驱动
        ********************************/

        /// <summary>
        /// 驱动所有活跃区块的状态机。
        /// </summary>
        public void Update()
        {
            if (_updatingChunks.Count == 0)
            {
                return;
            }

            List<Chunk> toRemove = [];
            foreach (Chunk chunk in _updatingChunks)
            {
                chunk.State.Update();

                if (chunk.State.CurrentNode == ChunkStateNode.Exit)
                {
                    toRemove.Add(chunk);
                }
            }

            foreach (Chunk chunk in toRemove)
            {
                RemoveChunk(chunk.CPosition);
            }
        }


        // ================================================================================
        //                                  Tile 查询
        // ================================================================================

        /// <summary>
        /// 检查指定全局 Tile 是否已加载。
        /// </summary>
        public bool IsTileLoaded(Vector2I globalTilePosition)
        {
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
            TilesChanged?.Invoke(this, args);
        }

        /// <summary>
        /// 触发 Chunk 状态稳定变化事件，供 Chunk 调用。
        /// </summary>
        internal void OnChunkStateStableReached(ChunkStateStableReachedEventArgs args)
        {
            ChunkStateStableReached?.Invoke(this, args);
        }
    }
}
