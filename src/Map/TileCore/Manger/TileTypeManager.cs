using System;
using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Godot;

namespace WorldWeaver.Map.TileCore.Manger
{
    /// <summary>
    /// TileType 种类管理器。
    /// <para>运行 ID 从 startAvailableRunId 开始分配给普通 TileType，0 到 startAvailableRunId - 1 为 empty 保留位。</para>
    /// <para>初始化读取逻辑委托给 <see cref="Initializer"/>。</para>
    /// </summary>
    public static class TileTypeManager
    {
        // ================================================================================
        //                                  私有字段
        // ================================================================================

        private static TileType[] tileTypes;
        private static FrozenDictionary<string, int> tileTypeNameToRunId;

        // ================================================================================
        //                                  公开属性
        // ================================================================================

        /// <summary>
        /// TileType 初始化器。
        /// <para>在 <see cref="Initialize"/> 前可通过 <c>+=</c> 追加输入路径。</para>
        /// </summary>
        public static TileTypeInitializer Initializer { get; } = new TileTypeInitializer()
            + ProjectSettings.GlobalizePath("res://assets/tiles");

        /// <summary>
        /// TileTypeManager 是否已经完成初始化。
        /// </summary>
        public static bool Initialized { get; private set; }

        /// <summary>
        /// runId:typeName 映射的 SHA256 哈希值。
        /// </summary>
        public static string MappingHash { get; private set; }

        /// <summary>
        /// 运行 ID 到 TileType 的只读数组视图。
        /// </summary>
        public static ReadOnlySpan<TileType> TileTypes => tileTypes;

        /// <summary>
        /// TileType 名称到运行 ID 的 FrozenDictionary 视图。
        /// </summary>
        public static FrozenDictionary<string, int> TileNameToRunIdTable => tileTypeNameToRunId;

        /// <summary>
        /// Type 索引器外观。
        /// </summary>
        public static TileTypeTypeIndexer Type { get; } = new();

        /// <summary>
        /// Name 索引器外观。
        /// </summary>
        public static TileTypeNameIndexer Name { get; } = new();

        /// <summary>
        /// RunId 索引器外观。
        /// </summary>
        public static TileTypeRunIdIndexer RunId { get; } = new();

        // ================================================================================
        //                                  初始化
        // ================================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool EnsureInitialized()
        {
            if (Initialized)
            {
                return true;
            }

            GD.PushError("TileTypeManager 尚未初始化，请先调用 Initialize() 方法");
            return false;
        }

        /// <summary>
        /// 初始化 TileTypeManager。
        /// </summary>
        /// <param name="printTileTypes">是否打印加载结果。</param>
        /// <param name="startAvailableRunId">起始可用运行 ID，0 到 startAvailableRunId - 1 为 empty 保留位。</param>
        public static void Initialize(bool printTileTypes = false, int startAvailableRunId = 1)
        {
            if (Initialized)
            {
                GD.PushError("TileTypeManager 已经初始化，禁止重复调用 Initialize() 方法");
                return;
            }

            var (loadedTileTypes, nameToRunId, mappingHash) = Initializer.Initialize(printTileTypes, startAvailableRunId);
            if (loadedTileTypes == null)
            {
                return;
            }

            int maxRunId = loadedTileTypes[^1].TileTypeRunId;
            tileTypes = new TileType[maxRunId + 1];
            for (int i = 0; i < loadedTileTypes.Length; i++)
            {
                tileTypes[loadedTileTypes[i].TileTypeRunId] = loadedTileTypes[i];
            }

            tileTypeNameToRunId = nameToRunId;
            MappingHash = mappingHash;
            Initialized = true;
        }

        // ================================================================================
        //                                  公开查询方法
        // ================================================================================

        public static TileType GetTypeByRunId(int runId)
        {
            if (!EnsureInitialized())
            {
                return null;
            }

            return GetTypeByRunIdUnchecked(runId);
        }

        public static TileType GetTypeByName(string name)
        {
            if (!EnsureInitialized())
            {
                return null;
            }

            return GetTypeByNameUnchecked(name);
        }

        public static int GetRunIdByName(string name)
        {
            if (!EnsureInitialized())
            {
                return 0;
            }

            return GetRunIdByNameUnchecked(name);
        }

        public static string GetNameByRunId(int runId)
        {
            if (!EnsureInitialized())
            {
                return null;
            }

            return GetTypeByRunIdUnchecked(runId)?.TileTypeName;
        }

        public static bool ContainsType(string name)
        {
            if (!EnsureInitialized())
            {
                return false;
            }

            return name != null && tileTypeNameToRunId.ContainsKey(name);
        }

        public static bool ContainsType(int runId)
        {
            if (!EnsureInitialized())
            {
                return false;
            }

            return runId >= 0 && runId < tileTypes.Length;
        }

        public static TileType GetTypeByNameViaRunId(string name)
        {
            if (!EnsureInitialized())
            {
                return null;
            }

            return GetTypeByNameUnchecked(name);
        }

        // ================================================================================
        //                                  内部查询方法
        // ================================================================================

        private static TileType GetTypeByNameUnchecked(string name)
        {
            int runId = GetRunIdByNameUnchecked(name);
            return GetTypeByRunIdUnchecked(runId);
        }

        private static int GetRunIdByNameUnchecked(string name)
        {
            if (name == null)
            {
                return 0;
            }

            return tileTypeNameToRunId.TryGetValue(name, out int runId) ? runId : 0;
        }

        private static TileType GetTypeByRunIdUnchecked(int runId)
        {
            if (runId >= 0 && runId < tileTypes.Length)
            {
                return tileTypes[runId];
            }

            return null;
        }
    }
}
