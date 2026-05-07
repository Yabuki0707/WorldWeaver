using System.Collections.Generic;

namespace WorldWeaver.Config
{
    /// <summary>
    /// 存档上下文接口，提供存档元信息与图层访问入口。
    /// </summary>
    public interface ISaveContext
    {
        /// <summary>
        /// 存档唯一标识。
        /// </summary>
        string SaveId { get; }

        /// <summary>
        /// 存档存储根路径。
        /// </summary>
        string StorageRootPath { get; }

        /// <summary>
        /// 当前存档包含的图层上下文列表（只读）。
        /// </summary>
        IReadOnlyList<IMapLayerContext> Layers { get; }
    }

    /// <summary>
    /// 地图图层上下文接口。
    /// </summary>
    public interface IMapLayerContext
    {
        /// <summary>
        /// 图层 ID。
        /// </summary>
        int LayerId { get; }

        /// <summary>
        /// 图层数据存储文件路径。
        /// </summary>
        string StorageFilePath { get; }
    }
}
