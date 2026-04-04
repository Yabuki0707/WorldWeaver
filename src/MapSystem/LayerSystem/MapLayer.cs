using System;
using WorldWeaver.MapSystem.ChunkSystem;
using WorldWeaver.MapSystem.GridSystem;

namespace WorldWeaver.MapSystem.LayerSystem
{
    /// <summary>
    /// 地图层。
    /// <para>持有图层级配置（存储路径、Chunk/Grid 尺寸）以及对应的管理器实例。</para>
    /// </summary>
    public class MapLayer
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


        // ================================================================================
        //                                  基础属性
        // ================================================================================

        /// <summary>
        /// 世界 ID。
        /// </summary>
        public int WorldId { get; }

        /// <summary>
        /// 图层 ID。
        /// </summary>
        public int LayerId { get; }

        /// <summary>
        /// 存储文件路径（根目录）。
        /// </summary>
        public string StorageFilePath { get; private set; }

        /// <summary>
        /// Chunk 大小配置。
        /// </summary>
        public MapElementSize ChunkSize { get; private set; }

        /// <summary>
        /// Grid 大小配置（单位：Chunk）。
        /// </summary>
        public MapElementSize GridSize { get; private set; }

        /// <summary>
        /// Chunk 管理器。
        /// </summary>
        public ChunkManager TheChunkManager { get; }

        /// <summary>
        /// Grid 管理器。
        /// </summary>
        public MapGridManager TheGridManager { get; }


        // ================================================================================
        //                                  构造与配置
        // ================================================================================

        /// <summary>
        /// 创建地图层。
        /// </summary>
        public MapLayer(
            int worldId,
            int layerId,
            string storageFilePath,
            MapElementSize chunkSize = null,
            MapElementSize gridSize = null)
        {
            if (string.IsNullOrWhiteSpace(storageFilePath))
            {
                throw new ArgumentException("storageFilePath 不能为空。", nameof(storageFilePath));
            }

            WorldId = worldId;
            LayerId = layerId;
            StorageFilePath = storageFilePath;
            ChunkSize = chunkSize ?? DEFAULT_CHUNK_SIZE;
            GridSize = gridSize ?? DEFAULT_GRID_SIZE;

            TheChunkManager = new ChunkManager(this);
            TheGridManager = new MapGridManager(this);
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
