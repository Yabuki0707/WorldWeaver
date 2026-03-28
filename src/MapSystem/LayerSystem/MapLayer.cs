using Godot;
using System.Collections.Generic;
using WorldWeaver.CounterSystem;
using WorldWeaver.MapSystem.ChunkSystem;
using WorldWeaver.MapSystem.GridSystem;

namespace WorldWeaver.MapSystem.LayerSystem
{
    /// <summary>
    /// 地图层。
    /// <para>负责维护区块与网格的尺寸配置，并持有对应的管理器实例。</para>
    /// </summary>
    [GlobalClass]
    public partial class MapLayer : Node
    {
        /*******************************
                  ID 系统
        ********************************/

        /// <summary>
        /// MapLayer 实例 ID 分配计数器。
        /// </summary>
        private static readonly Counter _idCounter = new("MapLayerIdCounter");

        /// <summary>
        /// 已创建的 MapLayer 实例映射。
        /// </summary>
        private static readonly Dictionary<int, MapLayer> _layerInstances = [];

        /// <summary>
        /// 图层实例 ID。
        /// </summary>
        public int LayerId { get; private set; }

        /// <summary>
        /// 是否已完成初始化。
        /// </summary>
        private bool _isInitialized;

        /// <summary>
        /// 根据 ID 获取 MapLayer 实例。
        /// </summary>
        public static MapLayer GetById(int id)
        {
            return _layerInstances.GetValueOrDefault(id, null);
        }


        /*******************************
                  基础属性
        ********************************/

        /// <summary>
        /// 所属世界实例。
        /// </summary>
        [Export]
        public World OwnerWorld { get; set; }

        /// <summary>
        /// 所属世界 ID。
        /// </summary>
        public int WorldId => OwnerWorld.WorldId;


        /*******************************
                  区块尺寸
        ********************************/

        /// <summary>
        /// 默认区块大小指数。
        /// </summary>
        public static readonly Vector2I DefaultChunkSizeExp = new(4, 4);

        /// <summary>
        /// 区块大小指数配置。
        /// </summary>
        private Vector2I _chunkSizeExp = DefaultChunkSizeExp;

        /// <summary>
        /// 区块大小指数。
        /// </summary>
        [Export]
        public Vector2I ChunkSizeExp
        {
            get => _chunkSizeExp;
            set
            {
                if (_isInitialized)
                {
                    GD.PushError($"MapLayer: 图层已初始化，禁止修改 ChunkSizeExp。当前值：{_chunkSizeExp}，尝试设置为：{value}");
                    return;
                }

                _chunkSizeExp = value;
            }
        }

        /// <summary>
        /// 区块尺寸聚合信息。
        /// </summary>
        public MapElementSize ChunkElementSize { get; private set; }

        /// <summary>
        /// 区块实际尺寸。
        /// </summary>
        public Vector2I ChunkSize { get; private set; }

        /// <summary>
        /// 区块尺寸掩码。
        /// </summary>
        public Vector2I ChunkSizeMask { get; private set; }

        /// <summary>
        /// 区块管理器实例。
        /// </summary>
        public ChunkManager TheChunkManager { get; private set; }


        /*******************************
                  网格尺寸
        ********************************/

        /// <summary>
        /// 默认网格大小指数。
        /// </summary>
        public static readonly Vector2I DefaultGridSizeExp = new(4, 4);

        /// <summary>
        /// 网格大小指数配置。
        /// </summary>
        private Vector2I _gridSizeExp = DefaultGridSizeExp;

        /// <summary>
        /// 网格大小指数。
        /// </summary>
        [Export]
        public Vector2I GridSizeExp
        {
            get => _gridSizeExp;
            set
            {
                if (_isInitialized)
                {
                    GD.PushError($"MapLayer: 图层已初始化，禁止修改 GridSizeExp。当前值：{_gridSizeExp}，尝试设置为：{value}");
                    return;
                }

                _gridSizeExp = value;
            }
        }

        /// <summary>
        /// 网格尺寸聚合信息。
        /// </summary>
        public MapElementSize GridElementSize { get; private set; }

        /// <summary>
        /// 网格实际尺寸。
        /// </summary>
        public Vector2I GridSize { get; private set; }

        /// <summary>
        /// 网格管理器实例。
        /// </summary>
        public MapGridManager TheGridManager { get; private set; }


        /*******************************
                  生命周期
        ********************************/

        /// <summary>
        /// 供 Godot 构造使用。
        /// </summary>
        public MapLayer()
        {
        }

        /// <summary>
        /// 节点进入场景树时分配图层 ID 并注册实例。
        /// </summary>
        public override void _EnterTree()
        {
            LayerId = (int)_idCounter.GetAndIncrement();
            _layerInstances[LayerId] = this;

            if (OwnerWorld == null)
            {
                GD.PushError("MapLayer: OwnerWorld 未赋值，请在编辑器中设置或通过代码初始化。");
            }
        }

        /// <summary>
        /// 节点准备完成后执行一次初始化。
        /// </summary>
        public override void _Ready()
        {
            if (OwnerWorld == null)
            {
                OwnerWorld = GetParent<World>();
                if (OwnerWorld == null)
                {
                    GD.PushError("MapLayer: 无法找到所属的 World 实例。");
                    return;
                }
            }

            if (!_isInitialized)
            {
                InitializeLayer();
                _isInitialized = true;
            }
        }

        /// <summary>
        /// 初始化图层核心数据。
        /// </summary>
        private void InitializeLayer()
        {
            if (!Chunk.ValidateChunkSizeExp(_chunkSizeExp))
            {
                _chunkSizeExp = DefaultChunkSizeExp;
                GD.PushError($"区块指数大小无效：{_chunkSizeExp}，已自动调整为默认大小指数 {DefaultChunkSizeExp}");
            }

            ChunkElementSize = new MapElementSize(_chunkSizeExp);
            ChunkSize = ChunkElementSize.Size;
            ChunkSizeMask = ChunkElementSize.Mask;

            if (!MapGrid.ValidateGridSizeExp(_gridSizeExp))
            {
                _gridSizeExp = DefaultGridSizeExp;
                GD.PushError($"网格指数大小无效：{_gridSizeExp}，已自动调整为默认大小指数 {DefaultGridSizeExp}");
            }

            GridElementSize = new MapElementSize(_gridSizeExp);
            GridSize = GridElementSize.Size;

            TheChunkManager = new ChunkManager(this);
            TheGridManager = new MapGridManager(this);
        }

        /// <summary>
        /// 帧更新。
        /// </summary>
        public override void _Process(double delta)
        {
            TheChunkManager?.Update();
        }

        /// <summary>
        /// 节点退出场景树时注销实例。
        /// </summary>
        public override void _ExitTree()
        {
            _layerInstances.Remove(LayerId);
        }
    }
}
