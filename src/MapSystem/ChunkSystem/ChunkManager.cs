using Godot;
using System;
using System.Collections.Generic;
using WorldWeaver.MapSystem.ChunkSystem.State;
using WorldWeaver.MapSystem.LayerSystem;
using WorldWeaver.MapSystem.TileSystem;

namespace WorldWeaver.MapSystem.ChunkSystem
{
    /// <summary>
    /// 单个 Tile 变化事件参数。
    /// </summary>
    public class TileChangedEventArgs(ChunkPosition chunkPosition, Vector2I localPosition, int newTileId, TileChangeType changeType) : EventArgs
    {
        /// <summary>区块坐标。</summary>
        public ChunkPosition ChunkPosition { get; } = chunkPosition;

        /// <summary>区块内局部坐标。</summary>
        public Vector2I LocalPosition { get; } = localPosition;

        /// <summary>变化后的 Tile 类型 ID。</summary>
        public int NewTileId { get; } = newTileId;

        /// <summary>变化类型。</summary>
        public TileChangeType ChangeType { get; } = changeType;
    }

    /// <summary>
    /// 批量 Tile 变化事件参数。
    /// </summary>
    public class TilesBatchChangedEventArgs(ChunkPosition chunkPosition, Vector2I[] localPositions, int[] newTileIds, TileChangeType changeType) : EventArgs
    {
        /// <summary>区块坐标。</summary>
        public ChunkPosition ChunkPosition { get; } = chunkPosition;

        /// <summary>发生变化的局部坐标集合。</summary>
        public Vector2I[] LocalPositions { get; } = localPositions;

        /// <summary>变化后的 Tile 类型 ID 集合。</summary>
        public int[] NewTileIds { get; } = newTileIds;

        /// <summary>变化类型。</summary>
        public TileChangeType ChangeType { get; } = changeType;
    }

    /// <summary>
    /// Chunk 状态到达稳定节点时的事件参数。
    /// </summary>
    public class ChunkStateStableReachedEventArgs(ChunkPosition chunkPosition, ChunkStateNode previousNode, ChunkStateNode newNode) : EventArgs
    {
        /// <summary>区块坐标。</summary>
        public ChunkPosition ChunkPosition { get; } = chunkPosition;

        /// <summary>变化前的稳定节点。</summary>
        public ChunkStateNode PreviousNode { get; } = previousNode;

        /// <summary>变化后的稳定节点。</summary>
        public ChunkStateNode NewNode { get; } = newNode;
    }

    /// <summary>
    /// 区块管理器，负责区块的创建、索引、移除与状态驱动。
    /// 每个 MapLayer 拥有一个独立的 ChunkManager 实例。
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
        /// 单个 Tile 变化事件。
        /// </summary>
        public event EventHandler<TileChangedEventArgs> TileChanged;

        /// <summary>
        /// 批量 Tile 变化事件。
        /// </summary>
        public event EventHandler<TilesBatchChangedEventArgs> TilesBatchChanged;

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
                return false;

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
            if (_chunks.TryGetValue(key, out Chunk chunk) == false)
                return false;

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
                return;

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


        /*******************************
                  Tile 查询
        ********************************/

        /// <summary>
        /// 检查指定全局 Tile 是否已加载。
        /// </summary>
        public bool IsTileLoaded(GlobalTilePosition globalTilePosition)
        {
            ChunkPosition chunkPos = globalTilePosition.ToChunkPosition(OwnerLayer.ChunkSizeExp);
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
        public int? GetTileInfo(GlobalTilePosition globalTilePosition)
        {
            LocalTilePosition localTilePosition = globalTilePosition.ToLocalTilePosition(OwnerLayer.ChunkSizeExp, out ChunkPosition chunkPosition);
            long key = chunkPosition.ToKey();
            if (_chunks.TryGetValue(key, out Chunk chunk) == false)
                return null;

            if (chunk.Data == null)
                return null;

            return chunk.Data.Tiles[localTilePosition.ToTileIndex(OwnerLayer.ChunkSizeExp)];
        }


        /*******************************
                  事件转发
        ********************************/

        /// <summary>
        /// 触发单个 Tile 变化事件，供 Chunk 调用。
        /// </summary>
        internal void OnTileChanged(TileChangedEventArgs args)
        {
            TileChanged?.Invoke(this, args);
        }

        /// <summary>
        /// 触发批量 Tile 变化事件，供 Chunk 调用。
        /// </summary>
        internal void OnTilesBatchChanged(TilesBatchChangedEventArgs args)
        {
            TilesBatchChanged?.Invoke(this, args);
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
