namespace WorldWeaver.MapCore.TileCore.Manger
{
    /// <summary>
    /// TileTypeManager 的 Type 索引器外观，用于获取 TileType。
    /// <para>可通过名称（<c>string</c>）或运行 ID（<c>int</c>）查询。</para>
    /// <para>示例：<c>TileTypeManager.Type["grass"]</c> 或 <c>TileTypeManager.Type[1]</c>。</para>
    /// </summary>
    public sealed class TileTypeTypeIndexer
    {
        internal TileTypeTypeIndexer()
        {
        }

        public TileType this[string name] => TileTypeManager.GetTypeByName(name);

        public TileType this[int runId] => TileTypeManager.GetTypeByRunId(runId);
    }

    /// <summary>
    /// TileTypeManager 的 Name 索引器外观，用于获取 TileType 名称。
    /// <para>可通过运行 ID（<c>int</c>）或 TileType 对象查询。</para>
    /// <para>示例：<c>TileTypeManager.Name[1]</c> 或 <c>TileTypeManager.Name[someTileType]</c>。</para>
    /// </summary>
    public sealed class TileTypeNameIndexer
    {
        internal TileTypeNameIndexer()
        {
        }

        public string this[int runId] => TileTypeManager.GetNameByRunId(runId);

        public string this[TileType type] => type?.TileTypeName;
    }

    /// <summary>
    /// TileTypeManager 的 RunId 索引器外观，用于获取运行 ID。
    /// <para>可通过名称（<c>string</c>）或 TileType 对象查询。</para>
    /// <para>示例：<c>TileTypeManager.RunId["grass"]</c> 或 <c>TileTypeManager.RunId[someTileType]</c>。</para>
    /// </summary>
    public sealed class TileTypeRunIdIndexer
    {
        internal TileTypeRunIdIndexer()
        {
        }

        public int this[string name] => TileTypeManager.GetRunIdByName(name);

        public int this[TileType type] => type?.TileTypeRunId ?? 0;
    }
}
