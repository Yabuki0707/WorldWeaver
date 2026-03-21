using System;
using Godot;

namespace rasu.Map.Tile
{
    /// <summary>
    /// TileType数据结构，表示一种瓦片类型
    /// 有效的TileTypeRunId从1开始分配,0为无效TileTypeRunId
    /// </summary>
    public class TileType : Object
    {
        /// <summary>
        /// TileType名称
        /// </summary>
        public string TileTypeName { get; set; }

        /// <summary>
        /// TileType纹理ID
        /// </summary>
        public int TileTypeTextureId { get; set; }

        /// <summary>
        /// 是否可通行
        /// </summary>
        public bool IsPassable { get; set; }

        /// <summary>
        /// TileType的运行ID（在初始化过程中分配，未分配时为0）
        /// </summary>
        public int TileTypeRunId { get; set; } = 0;

        /// <summary>
        /// 将TileType数据转换为字符串表示
        /// </summary>
        /// <returns>TileType数据的字符串表示</returns>
        public override string ToString()
        {
            return $"TileType [RunId: {TileTypeRunId}, Name: {TileTypeName}, TextureId: {TileTypeTextureId}, IsPassable: {IsPassable}]";
        }
    }
}
