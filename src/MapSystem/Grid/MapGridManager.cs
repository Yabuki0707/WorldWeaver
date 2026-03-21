using Godot;
using System;
using System.Collections.Generic;
using rasu.Map.Chunk;
using rasu.Map.Layer;

namespace rasu.Map.Grid
{
    /// <summary>
    /// 地图网格管理器，负责管理无限 2D 平面上的所有网格。
    /// <para>每个 MapLayer 拥有一个独立的 MapGridManager 实例。</para>
    /// <para>使用 long Key (x, y) 索引网格，避免哈希冲突。</para>
    /// </summary>
    public class MapGridManager
    {
        /*******************************
                  数据存储
        ********************************/

        /// <summary>
        /// 活跃网格字典 [Key(long) -> 区块数量]
        /// <para>值存储该网格中的区块数量</para>
        /// </summary>
        private readonly Dictionary<long, MapGrid> _grids = [];



        /// <summary>
        /// 网格添加事件
        /// </summary>
        public event Action<MapGridPosition> GridAdded;

        /// <summary>
        /// 网格移除事件
        /// </summary>
        public event Action<MapGridPosition> GridRemoved;


        /*******************************
                  属性
        ********************************/



        /// <summary>
        /// 所属的 MapLayer 实例
        /// </summary>
        public MapLayer OwnerLayer { get; private set; } = null;


        /*******************************
                  构造与初始化
        ********************************/

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="owner">所属 MapLayer</param>
        public MapGridManager(MapLayer owner)
        {
            OwnerLayer = owner;
            // 初始化字典容量，避免后续动态扩容
            _grids = new(32);
            
            // 订阅区块管理器事件
            SubscribeToChunkEvents();
        }


        /*******************************
                  核心：网格索引与管理
        ********************************/

        /// <summary>
        /// 检查是否存在指定位置的网格
        /// </summary>
        public bool HasGrid(MapGridPosition gPosition)
        {
            long key = gPosition.ToKey();
            return _grids.ContainsKey(key);
        }


        /// <summary>
        /// 获取网格位置（如果存在）
        /// </summary>
        public MapGridPosition? GetGrid(MapGridPosition gPosition)
        {
            long key = gPosition.ToKey();
            return _grids.ContainsKey(key) ? gPosition : null;
        }

        /// <summary>
        /// 添加新网格
        /// </summary>
        /// <param name="pos">要添加的网格位置</param>
        /// <returns>添加成功返回 true，失败返回 false</returns>
        private bool AddGrid(MapGridPosition gPosition)
        {
            long key = gPosition.ToKey();
            if (_grids.ContainsKey(key))
            {
                return false;
            }
            
            _grids[key] = new MapGrid(this, gPosition); // 初始计数为0
            GridAdded?.Invoke(gPosition);
            
            return true;
        }


        /// <summary>
        /// 移除网格
        /// </summary>
        private bool RemoveGrid(MapGridPosition gPosition)
        {
            long key = gPosition.ToKey();
            if (_grids.Remove(key) == true)
            {
                GridRemoved?.Invoke(gPosition);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 增加网格区块计数（严格检测）
        /// </summary>
        private bool IncrementGridChunkCount(MapGridPosition gPosition)
        {
            long key = gPosition.ToKey();
            if (_grids.TryGetValue(key, out MapGrid grid) == true)
            {
                grid.ChunkCount++;
                return true;
            }
            // 如果网格不存在，则返回失败
            return false;
        }

        /// <summary>
        /// 减少网格区块计数（严格检测）
        /// </summary>
        private bool DecrementGridChunkCount(MapGridPosition gPosition)
        {
            long key = gPosition.ToKey();
            if (_grids.TryGetValue(key, out MapGrid grid) == true)
            {
                grid.ChunkCount--;
                if (grid.ChunkCount <= 0)
                {
                    // 移除网格
                    RemoveGrid(gPosition);
                }
                return true;
            }
            // 如果键不存在，说明网格已被移除或从未创建，返回错误
            return false;
        }


        /*******************************
                  事件订阅与处理
        ********************************/

        /// <summary>
        /// 订阅区块管理器事件
        /// </summary>
        private void SubscribeToChunkEvents()
        {
            var chunkManager = OwnerLayer.TheChunkManager;
            if (chunkManager == null)
            {
                GD.PushError("[MapGridManager] 无法订阅区块事件：TheChunkManager 为 null");
                return;
            }

            chunkManager.ChunkCreated += OnChunkAdded;
            chunkManager.ChunkRemoved += OnChunkRemoved;
        }

        /// <summary>
        /// 处理区块添加事件
        /// </summary>
        private void OnChunkAdded(ChunkPosition cPosition)
        {
            // 计算区块所属的网格位置
            MapGridPosition gridPos = cPosition.ToGridPosition(OwnerLayer.GridSizeExp);
            // 尝试添加网格
            AddGrid(gridPos);
            // 增加网格区块计数
            if (IncrementGridChunkCount(gridPos)==false)
                GD.PushError($"[MapGridManager.OnChunkAdded] 网格不存在于位置 {gridPos}，区块坐标 {cPosition} 添加事件被忽略");
        }

        /// <summary>
        /// 处理区块移除事件
        /// </summary>
        private void OnChunkRemoved(ChunkPosition cPosition)
        {
            // 计算区块所属的网格位置
            MapGridPosition gridPos = cPosition.ToGridPosition(OwnerLayer.GridSize);
            
            // 如果网格存在，减少区块计数
            bool decrementResult = DecrementGridChunkCount(gridPos);
            // 如果网格不存在，记录错误（正常情况下不应该发生）
            if (decrementResult==false)
                GD.PushError($"[MapGridManager.OnChunkRemoved] 网格不存在于位置 {gridPos}，区块坐标 {cPosition} 移除事件被忽略");
        }


    }
}
