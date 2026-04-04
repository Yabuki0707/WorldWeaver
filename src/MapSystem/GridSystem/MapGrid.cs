using Godot;

namespace WorldWeaver.MapSystem.GridSystem
{
    /// <summary>
    /// 地图网格运行时实体，表示一个网格位置。
    /// </summary>
    public class MapGrid
    {
        /*******************************
                  核心属性
        ********************************/

        /// <summary>
        /// 网格位置。
        /// </summary>
        public MapGridPosition GPosition { get; private set; }

        /// <summary>
        /// 所属的 MapGridManager。
        /// </summary>
        public MapGridManager OwnerManager { get; private set; }

        /// <summary>
        /// 网格中的区块数量。
        /// </summary>
        public int ChunkCount { get; set; } = 0;


        /*******************************
                  构造
        ********************************/

        /// <summary>
        /// 创建网格实例。
        /// </summary>
        public MapGrid(MapGridManager ownerManager, MapGridPosition position)
        {
            GPosition = position;
            OwnerManager = ownerManager;
        }
    }
}
