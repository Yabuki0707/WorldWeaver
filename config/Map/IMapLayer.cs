namespace WorldWeaver.Map
{
    /// <summary>
    /// 地图图层接口。
    /// </summary>
    public interface IMapLayer
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
