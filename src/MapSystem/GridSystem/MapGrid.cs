using System;
using Godot;

namespace WorldWeaver.MapSystem.GridSystem
{
    /// <summary>
    /// 地图网格运行时实体，表示一个网格位置
    /// </summary>
    public class MapGrid
    {
        /*******************************
                  核心属性
        ********************************/

        /// <summary>网格位置</summary>
        public MapGridPosition GPosition { get; private set; }

        /// <summary>所属的 MapGridManager</summary>
        public MapGridManager OwnerManager { get; private set; }

        /// <summary>区块数量</summary>
        public int ChunkCount { get; set; } = 0;


        /*******************************
                  构造与销毁
        ********************************/

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="position">网格位置</param>
        /// <param name="ownerManager">所属的 MapGridManager</param>
        public MapGrid(MapGridManager ownerManager, MapGridPosition position)
        {
            GPosition = position;
            OwnerManager = ownerManager;
        }


        /*******************************
                  静态验证方法
        ********************************/

        /// <summary>
        /// 验证网格大小指数的有效性
        /// </summary>
        /// <param name="gridSizeExp">待验证的网格大小指数</param>
        /// <returns>验证通过返回true，否则返回false</returns>
        public static bool ValidateGridSizeExp(Vector2I gridSizeExp)
        {
            return gridSizeExp.X > 0 && gridSizeExp.Y > 0;
        }
    }
}