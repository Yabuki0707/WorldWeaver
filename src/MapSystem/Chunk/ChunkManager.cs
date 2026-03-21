using Godot;
using System;
using System.Collections.Generic;
using rasu.Map.Chunk.State;
using rasu.Map.Chunk.State.Handlers;
using rasu.Map.Layer;
using rasu.Map.Tile;

namespace rasu.Map.Chunk
{

    /// <summary>
    /// Tile 变化事件参数
    /// </summary>
    public class TileChangedEventArgs(ChunkPosition chunkPosition, Vector2I localPosition, int? oldTileId, int? newTileId, TileChangeType changeType) : EventArgs
    {
        /// <summary>区块坐标</summary>
        public ChunkPosition ChunkPosition { get; } = chunkPosition;
        /// <summary>区块内局部坐标</summary>
        public Vector2I LocalPosition { get; } = localPosition;
        /// <summary>变化前的Tile类型ID</summary>
        public int? OldTileId { get; } = oldTileId;
        /// <summary>变化后的Tile类型ID</summary>
        public int? NewTileId { get; } = newTileId;
        /// <summary>变化类型</summary>
        public TileChangeType ChangeType { get; } = changeType;
    }

    /// <summary>
    /// Chunk 状态稳定变化事件参数
    /// </summary>
    public class ChunkStateStableReachedEventArgs(ChunkPosition chunkPosition, ChunkStateNode previousNode, ChunkStateNode newNode) : EventArgs
    {
        /// <summary>区块坐标</summary>
        public ChunkPosition ChunkPosition { get; } = chunkPosition;
        /// <summary>变化前的稳定节点</summary>
        public ChunkStateNode PreviousNode { get; } = previousNode;
        /// <summary>变化后的稳定节点</summary>
        public ChunkStateNode NewNode { get; } = newNode;
    }

    /// <summary>
    /// 区块管理器，负责管理区块的存在、移除与创建。
    /// <para>每个 MapLayer 拥有一个独立的 ChunkManager 实例。</para>
    /// <para>使用 long Key (x, y) 索引区块，避免哈希冲突。</para>
    /// <para>拥有存储 Chunk 引用的数据结构。</para>
    /// </summary>
    public class ChunkManager
    {
        /*******************************
                  数据存储
        ********************************/

        /// <summary>
        /// 活跃区块字典 [Key(long) -> Chunk]
        /// </summary>
        private readonly Dictionary<Int128, Chunk> _chunks = [];

        /// <summary>
        /// 待更新的区块列表（避免每帧遍历整个字典）
        /// </summary>
        private readonly HashSet<Chunk> _updatingChunks = [];

        /// <summary>
        /// 区块增加事件
        /// </summary>
        public event Action<ChunkPosition> ChunkCreated;

        /// <summary>
        /// 区块移除事件
        /// </summary>
        public event Action<ChunkPosition> ChunkRemoved;

        /// <summary>
        /// Tile 变化事件
        /// </summary>
        public event EventHandler<TileChangedEventArgs> TileChanged;

        /// <summary>
        /// Chunk 状态到达稳定节点事件
        /// </summary>
        public event EventHandler<ChunkStateStableReachedEventArgs> ChunkStateStableReached;


        /*******************************
                  属性
        ********************************/

        /// <summary>
        /// 所属的 MapLayer 实例
        /// </summary>
        public readonly MapLayer OwnerLayer;


        /*******************************
                  构造与初始化
        ********************************/

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="owner">所属 MapLayer</param>
        public ChunkManager(MapLayer owner)
        {
            //设置基本属性
            OwnerLayer = owner;
            // 初始化字典容量，避免后续动态扩容
            _chunks= new(128);
        }


        /*******************************
                  核心：区块索引与管理
        ********************************/

        /// <summary>
        /// 检查是否存在指定位置的区块
        /// </summary>
        /// <param name="chunkPosition">要检查的区块位置</param>
        /// <returns>如果存在则返回 true，否则返回 false</returns>
        public bool HasChunk(ChunkPosition chunkPosition)
        {
            long key = chunkPosition.ToKey();
            return _chunks.ContainsKey(key);
        }


        /// <summary>
        /// 获取区块（如果存在）
        /// </summary>
        /// <param name="chunkPosition">要获取的区块的位置</param>
        /// <returns>如果存在则返回区块实例，否则返回 null</returns>
        public Chunk GetChunk(ChunkPosition chunkPosition)
        {
            long key = chunkPosition.ToKey();
            return _chunks.TryGetValue(key, out Chunk chunk) ? chunk : null;
        }

        /// <summary>
        /// 添加新区块并注册
        /// </summary>
        /// <param name="chunkPosition">要创建的区块的位置</param>
        /// <returns>创建成功返回 true，失败返回 false</returns>
        public bool CreateChunk(ChunkPosition chunkPosition)
        {
            // 检测区块若已存在则返回 false避免重复冲突
            long key = chunkPosition.ToKey();
            if (_chunks.ContainsKey(key))
            {
                return false;
            }
            // 加入区块并触发事件
            Chunk createdChunk = new(this,chunkPosition);
            _chunks[key] = createdChunk;
            _updatingChunks.Add(createdChunk);
            ChunkCreated?.Invoke(chunkPosition);
            
            return true;
        }


        /// <summary>
        /// 移除区块
        /// </summary>
        /// <param name="chunkPosition">要移除的区块的位置</param>
        /// <returns>移除成功返回 true，失败返回 false</returns>
        public bool RemoveChunk(ChunkPosition chunkPosition)
        {
            long key = chunkPosition.ToKey();
            // 该区块存在才进行移除
            if (_chunks.TryGetValue(key, out Chunk chunk)==true)
            {
                _chunks.Remove(key);
                _updatingChunks.Remove(chunk);
                ChunkRemoved?.Invoke(chunk.CPosition);
                return true;
            }
            return false;
        }


        /*******************************
                  核心：统一状态驱动
        ********************************/


        /// <summary>
        /// 驱动所有活跃区块的状态机（应在 MapLayer 的 Update 中调用）
        /// </summary>
        public void Update()
        {
            if (_updatingChunks.Count == 0) return;
            // 记录待移除的区块
            List<Chunk> toRemove = [];
            // 遍历所有活跃区块
            foreach (Chunk chunk in _updatingChunks)
            {
                // 统一调用 ChunkState 的 Update
                chunk.State.Update();

                // 若生命周期为离开,则记录待移除
                if (chunk.State.CurrentNode == ChunkStateNode.Exit)
                {
                    toRemove.Add(chunk);
                }
            }
            // 移除所有状态为离开的区块
            foreach (Chunk chunk in toRemove)
            {
                RemoveChunk(chunk.CPosition);
            }
        }


        /*******************************
                  Tile 查询接口
        ********************************/

        /// <summary>
        /// 检查指定 Tile 是否已加载（在内存中或游戏中）
        /// </summary>
        public bool IsTileLoaded(GlobalTilePosition globalTilePosition)
        {
            ChunkPosition chunkPos = globalTilePosition.ToChunkPosition(OwnerLayer.ChunkSizeExp);
            long key = chunkPos.ToKey();
            
            if (_chunks.TryGetValue(key, out Chunk chunk))
            {
                if (chunk.Data == null) return false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取指定 Tile 的信息（如果未加载则返回 null）
        /// </summary>
        public int? GetTileInfo(GlobalTilePosition globalTilePosition)
        {
            // 转换为局部坐标并获取区块位置
            LocalTilePosition localTilePosition = globalTilePosition.ToLocalTilePosition(OwnerLayer.ChunkSizeExp, out ChunkPosition chunkPosition);
            // 检查区块是否存在
            long key = chunkPosition.ToKey(); 
            if (_chunks.TryGetValue(key, out Chunk chunk)==false)
                return null;
            // 确保区块数据已加载
            if (chunk.Data == null)
                return null;
            return chunk.Data.Tiles[localTilePosition.ToTileIndex(OwnerLayer.ChunkSizeExp)];
        }


        /*******************************
                  事件触发方法（供 Chunk 调用）
        ********************************/

        /// <summary>
        /// 触发 Tile 变化事件（供 Chunk 调用）
        /// </summary>
        internal void OnTileChanged(TileChangedEventArgs args)
        {
            TileChanged?.Invoke(this, args);
        }

        /// <summary>
        /// 触发 Chunk 状态稳定变化事件（供 Chunk 调用）
        /// </summary>
        internal void OnChunkStateStableReached(ChunkStateStableReachedEventArgs args)
        {
            ChunkStateStableReached?.Invoke(this, args);
        }


    }
}
