using System;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem;
using WorldWeaver.MapSystem.GridSystem;

namespace WorldWeaver.MapSystem.LayerSystem
{
    /// <summary>
    /// 地图层。
    /// <para>作为场景树节点持有图层级配置（存储路径、Chunk/Grid 尺寸）以及对应的管理器实例。</para>
    /// </summary>
    [GlobalClass]
    public partial class MapLayer : Node
    {
        // ================================================================================
        //                                  默认配置常量
        // ================================================================================

        /// <summary>
        /// 默认 Chunk 宽度指数。
        /// </summary>
        public const int DEFAULT_CHUNK_WIDTH_EXP = 4;

        /// <summary>
        /// 默认 Chunk 高度指数。
        /// </summary>
        public const int DEFAULT_CHUNK_HEIGHT_EXP = 4;

        /// <summary>
        /// 默认 Grid 宽度指数（单位：Chunk）。
        /// </summary>
        public const int DEFAULT_GRID_WIDTH_EXP = 4;

        /// <summary>
        /// 默认 Grid 高度指数（单位：Chunk）。
        /// </summary>
        public const int DEFAULT_GRID_HEIGHT_EXP = 4;

        /// <summary>
        /// 默认 Chunk 大小。
        /// </summary>
        public static readonly MapElementSize DEFAULT_CHUNK_SIZE =
            new(DEFAULT_CHUNK_WIDTH_EXP, DEFAULT_CHUNK_HEIGHT_EXP);

        /// <summary>
        /// 默认 Grid 大小（单位：Chunk）。
        /// </summary>
        public static readonly MapElementSize DEFAULT_GRID_SIZE =
            new(DEFAULT_GRID_WIDTH_EXP, DEFAULT_GRID_HEIGHT_EXP);

        /// <summary>
        /// 默认存储根路径。
        /// </summary>
        public const string DEFAULT_STORAGE_FILE_PATH = "user://world_0/layer_0";


        // ================================================================================
        //                                  导出配置
        // ================================================================================

        /// <summary>
        /// 世界 ID。
        /// </summary>
        [Export]
        public int WorldId { get; set; } = 0;

        /// <summary>
        /// 图层 ID。
        /// </summary>
        [Export]
        public int LayerId { get; set; } = 0;

        /// <summary>
        /// 存储文件路径（根目录）。
        /// </summary>
        [Export]
        public string StorageFilePath { get; private set; } = DEFAULT_STORAGE_FILE_PATH;

        /// <summary>
        /// Chunk 宽度指数。
        /// </summary>
        [Export(PropertyHint.Range, "1,29,1")]
        public int ChunkWidthExp { get; set; } = DEFAULT_CHUNK_WIDTH_EXP;

        /// <summary>
        /// Chunk 高度指数。
        /// </summary>
        [Export(PropertyHint.Range, "1,29,1")]
        public int ChunkHeightExp { get; set; } = DEFAULT_CHUNK_HEIGHT_EXP;

        /// <summary>
        /// Grid 宽度指数（单位：Chunk）。
        /// </summary>
        [Export(PropertyHint.Range, "1,29,1")]
        public int GridWidthExp { get; set; } = DEFAULT_GRID_WIDTH_EXP;

        /// <summary>
        /// Grid 高度指数（单位：Chunk）。
        /// </summary>
        [Export(PropertyHint.Range, "1,29,1")]
        public int GridHeightExp { get; set; } = DEFAULT_GRID_HEIGHT_EXP;


        // ================================================================================
        //                                  运行时属性
        // ================================================================================

        /// <summary>
        /// Chunk 大小配置。
        /// </summary>
        public MapElementSize ChunkSize { get; private set; } = DEFAULT_CHUNK_SIZE;

        /// <summary>
        /// Grid 大小配置（单位：Chunk）。
        /// </summary>
        public MapElementSize GridSize { get; private set; } = DEFAULT_GRID_SIZE;

        /// <summary>
        /// Chunk 管理器。
        /// </summary>
        public ChunkManager TheChunkManager { get; private set; }

        /// <summary>
        /// Grid 管理器。
        /// </summary>
        public MapGridManager TheGridManager { get; private set; }

        /// <summary>
        /// 是否已经完成运行时初始化。
        /// </summary>
        private bool _isInitialized;


        // ================================================================================
        //                                  生命周期与配置
        // ================================================================================

        /// <summary>
        /// 进入 Ready 阶段后初始化图层配置与管理器。
        /// </summary>
        public override void _Ready()
        {
            if (_isInitialized)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(StorageFilePath))
            {
                GD.PushWarning($"[MapLayer] StorageFilePath 为空，已回退为默认路径 {DEFAULT_STORAGE_FILE_PATH}。");
                StorageFilePath = DEFAULT_STORAGE_FILE_PATH;
            }

            // 节点进入场景树后再固化尺寸配置，确保导出属性已准备完毕。
            ChunkSize = new MapElementSize(ChunkWidthExp, ChunkHeightExp);
            GridSize = new MapElementSize(GridWidthExp, GridHeightExp);

            // Manager 生命周期与 Layer 节点绑定，只初始化一次。
            TheChunkManager = new ChunkManager(this);
            TheGridManager = new MapGridManager(this);
            _isInitialized = true;
        }

        /// <summary>
        /// 更新存储路径。
        /// </summary>
        public void SetStorageFilePath(string storageFilePath)
        {
            if (string.IsNullOrWhiteSpace(storageFilePath))
            {
                throw new ArgumentException("storageFilePath 不能为空。", nameof(storageFilePath));
            }

            StorageFilePath = storageFilePath;
        }
        
    }
}
