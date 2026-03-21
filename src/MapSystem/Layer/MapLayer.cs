using Godot;
using System;
using System.Collections.Generic;
using rasu.Map.Chunk;
using rasu.Map.Grid;

namespace rasu.Map.Layer
{
    
    /// <summary>
    /// 地图层，负责存储区块的基本信息（区块大小）。
    /// <para>每个 MapLayer 拥有独立的 ChunkManager 实例管理区块。</para>
    /// </summary>
    [GlobalClass]
    public partial class MapLayer : Node
    {
        /*******************************
                  ID系统
        ********************************/
        
        /// <summary>
        /// MapLayer实例ID分配计数器
        /// </summary>
        private static readonly Counter _idCounter = new("MapLayerIdCounter", null);
        
        /// <summary>
        /// MapLayer实例映射表，以ID为键存储所有MapLayer实例
        /// </summary>
        private static readonly Dictionary<int, MapLayer> _layerInstances = [];

        /// <summary>
        /// MapLayer实例的唯一标识符
        /// </summary>
        public int LayerId { get; private set; }


        /// <summary>
        /// 根据ID获取MapLayer实例
        /// </summary>
        /// <param name="id">MapLayer的ID</param>
        /// <returns>对应的MapLayer实例，如果不存在则返回null</returns>
        public static MapLayer GetById(int id)
        {
            if (_layerInstances.TryGetValue(id, out MapLayer value))
                return value;
            return null;
        }


        /*******************************
                  属性
        ********************************/


        /// <summary>
        /// 所属世界实例
        /// </summary>
        public World OwnerWorld { get; private set; }

        /// <summary>
        /// 获取所属世界的ID
        /// </summary>
        public int WorldId => OwnerWorld.WorldId;


        /// <summary>
        /// 默认区块(Chunk)大小
        /// </summary>
        public static readonly Vector2I DefaultChunkSizeExp = new(4, 4);


        /// <summary>
        /// 区块大小(Tile的长宽数量)
        /// </summary>
        public Vector2I ChunkSize { get; private set; }

        /// <summary>
        /// 区块大小指数(2^Exp)
        /// </summary>
        public Vector2I ChunkSizeExp { get; private set; }

        /// <summary>
        /// 区块大小指数(2^Exp -1即size -1)
        /// </summary>
        public Vector2I ChunkSizeMark { get; private set; }

        /// <summary>
        /// 区块管理器实例
        /// </summary>
        public ChunkManager TheChunkManager { get; private set; }



        /// <summary>
        /// 默认网格(Grid)大小
        /// </summary>
        public static readonly Vector2I DefaultGridSizeExp = new(4, 4);

        /// <summary>
        /// 网格大小指数(2^Exp)
        /// </summary>
        public Vector2I GridSizeExp { get; private set; }

        /// <summary>
        /// 网格大小(即一个网格包含多少个区块)
        /// </summary>
        public Vector2I GridSize { get; private set; }

        /// <summary>
        /// 网格管理器实例
        /// </summary>
        public MapGridManager TheGridManager { get; private set; }


        /*******************************
                  构造与销毁
        ********************************/
        


        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="ownerWorld">所属世界实例</param>
        /// <param name="chunkSizeExp">区块大小指数（2^Exp），无效时自动修正为默认大小</param>
        /// <param name="gridSizeExp">网格大小指数（2^Exp），无效时自动修正为默认大小</param>
        public MapLayer(World ownerWorld, Vector2I chunkSizeExp, Vector2I gridSizeExp)
        {
            // 分配Layer中的ID
            LayerId = (int)_idCounter.GetAndIncrement();
            _layerInstances[LayerId] = this;
            
            // 设置所属世界
            OwnerWorld = ownerWorld;
            
            // 验证区块大小指数并设置区块大小
            if (Chunk.Chunk.ValidateChunkSizeExp(chunkSizeExp)==false)
            {
                chunkSizeExp = DefaultChunkSizeExp;
                GD.PushError($"区块指数大小无效: {chunkSizeExp}，已自动调整为默认大小指数 {DefaultChunkSizeExp}");
            }
            ChunkSize = new( 1 << chunkSizeExp.X, 1 << chunkSizeExp.Y);
            ChunkSizeExp = chunkSizeExp;
            ChunkSizeMark = ChunkSize - new Vector2I(1,1);
            
            // 验证网格大小指数并设置网格大小
            if (MapGrid.ValidateGridSizeExp(gridSizeExp)==false)
            {
                gridSizeExp = DefaultGridSizeExp;
                GD.PushError($"网格指数大小无效: {gridSizeExp}，已自动调整为默认大小指数 {DefaultGridSizeExp}");
            }
            GridSize = new( 1 << gridSizeExp.X, 1 << gridSizeExp.Y);

            // 初始化区块管理器
            TheChunkManager = new(this);
            
            // 初始化网格管理器
            TheGridManager = new MapGridManager(this);
        }

        /// <summary>
        /// 帧更新
        /// </summary>
        public override void _Process(double delta)
        {
            // 驱动区块管理器更新
            TheChunkManager?.Update();
        }

        /// <summary>
        /// 当MapLayer节点从场景树中移除时，自动清理实例映射
        /// </summary>
        public override void _ExitTree()
        {
            _layerInstances.Remove(LayerId);
        }
    }
}
