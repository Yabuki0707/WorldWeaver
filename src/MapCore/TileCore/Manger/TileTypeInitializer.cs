using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Godot;
using Newtonsoft.Json;

namespace WorldWeaver.MapCore.TileCore.Manger
{
    /// <summary>
    /// TileTypeManager 初始化模块。
    /// <para>负责从路径列表（目录或文件）并行读取 TileType JSON、校验、分配运行 ID、去重，并生成 FrozenDictionary 与映射哈希。</para>
    /// <para>通过 <c>+=</c> / <c>-=</c> 语法糖管理输入路径。</para>
    /// </summary>
    public sealed class TileTypeInitializer
    {
        private readonly TileTypePathCollection _pathsCollection = new();

        // ================================================================================
        //                                  公开属性
        // ================================================================================

        /// <summary>
        /// 路径列表是否仍保持在初始化后的状态。
        /// <para>任何影响路径的操作（<c>+=</c>、<c>-=</c>、<see cref="ClearPaths"/>）均置为 false，仅 <see cref="Initialize"/> 成功时置为 true。</para>
        /// </summary>
        public bool IsPathListUnmodified { get; private set; }

        // ================================================================================
        //                                  运算符重载
        // ================================================================================

        /// <summary>
        /// 通过 <c>+=</c> 添加目录或单个 JSON 文件路径。
        /// </summary>
        public static TileTypeInitializer operator +(TileTypeInitializer initializer, string path)
        {
            if (initializer != null && !string.IsNullOrWhiteSpace(path))
            {
                initializer._pathsCollection.Add(path);
                initializer.IsPathListUnmodified = false;
            }

            return initializer;
        }

        /// <summary>
        /// 通过 <c>-=</c> 移除路径，并标记路径列表已被修改。
        /// </summary>
        public static TileTypeInitializer operator -(TileTypeInitializer initializer, string path)
        {
            if (initializer != null && !string.IsNullOrWhiteSpace(path))
            {
                initializer._pathsCollection.Remove(path);
                initializer.IsPathListUnmodified = false;
            }

            return initializer;
        }

        // ================================================================================
        //                                  路径管理
        // ================================================================================

        /// <summary>
        /// 清空所有已配置的路径，并标记路径列表已被修改。
        /// </summary>
        public void ClearPaths()
        {
            _pathsCollection.Clear();
            IsPathListUnmodified = false;
        }

        // ================================================================================
        //                                  初始化入口
        // ================================================================================

        /// <summary>
        /// 消费已配置的所有路径，并行扫描并加载 TileType 数据。
        /// <para>路径列表在并行读取前会先排序，外层按文件或父目录作为单位分配数组槽位。</para>
        /// <para>根据 <paramref name="startAvailableRunId"/> 的数量分配 empty 类型占据 0 到 startAvailableRunId - 1 号位。</para>
        /// <para>在融合阶段完成去重：重复名称选取前者。</para>
        /// </summary>
        /// <param name="printTileTypes">是否打印加载结果。</param>
        /// <param name="startAvailableRunId">起始可用运行 ID，0 到 startAvailableRunId - 1 为 empty 保留位。</param>
        /// <returns>包含 TileType 数组、FrozenDictionary 名称→运行ID 映射、映射哈希的三元组。</returns>
        public (TileType[] tileTypes, FrozenDictionary<string, int> nameToRunId, string mappingHash) Initialize(bool printTileTypes, int startAvailableRunId = 1)
        {
            if (_pathsCollection.Count == 0)
            {
                GD.PushError("TileTypeInitializer 未配置任何输入路径，请通过 += 添加目录或文件");
                return (null, null, null);
            }

            // 排序路径列表
            string[] sortedPaths = _pathsCollection.ToArray();
            Array.Sort(sortedPaths, StringComparer.Ordinal);

            // 外层按路径数为单位分配槽位
            TileType[][] tileTypeChunks = new TileType[sortedPaths.Length][];

            // 并行读取
            Parallel.For(0, sortedPaths.Length, i =>
            {
                tileTypeChunks[i] = LoadFromPath(i, sortedPaths[i]);
            });

            // 主线程融合、去重、分配 runId
            List<TileType> tileTypeList = new();
            Dictionary<string, int> nameToRunIdBuilder = new();

            // empty 类型占据 0 到 startAvailableRunId - 1
            for (int i = 0; i < startAvailableRunId; i++)
            {
                tileTypeList.Add(new TileType
                {
                    TileTypeName = "empty",
                    TileTypeRunId = i
                });
            }

            // 首个 empty 入库
            if (startAvailableRunId > 0)
            {
                nameToRunIdBuilder["empty"] = 0;
            }

            // 从 startAvailableRunId 开始为普通 TileType 分配 runId
            TileTypeRunIdAllocator runIdAllocator = new(startAvailableRunId);
            for (int i = 0; i < tileTypeChunks.Length; i++)
            {
                TileType[] chunk = tileTypeChunks[i];
                if (chunk == null || chunk.Length == 0)
                {
                    continue;
                }

                for (int j = 0; j < chunk.Length; j++)
                {
                    TileType tileType = chunk[j];

                    // 去重：名称已存在则跳过，选取前者
                    if (nameToRunIdBuilder.ContainsKey(tileType.TileTypeName))
                    {
                        continue;
                    }

                    tileType.TileTypeRunId = runIdAllocator++;
                    tileTypeList.Add(tileType);
                    nameToRunIdBuilder[tileType.TileTypeName] = tileType.TileTypeRunId;
                }
            }

            string mappingHash = BuildMappingHash(tileTypeList);

            if (printTileTypes)
            {
                PrintLoadedTypes(tileTypeList);
            }

            IsPathListUnmodified = true;
            return (tileTypeList.ToArray(), nameToRunIdBuilder.ToFrozenDictionary(), mappingHash);
        }

        // ================================================================================
        //                                  并行读取
        // ================================================================================

        /// <summary>
        /// 从单个路径加载 TileType 数组。
        /// <para>目录则扫描 *.json 并按文件名排序，文件则直接加载。</para>
        /// </summary>
        private static TileType[] LoadFromPath(int index, string path)
        {
            if (Directory.Exists(path))
            {
                string[] files = Directory.GetFiles(path, "*.json");
                Array.Sort(files, StringComparer.Ordinal);

                List<TileType> results = new(files.Length);
                for (int i = 0; i < files.Length; i++)
                {
                    TileType tileType = LoadTileType(files[i]);
                    if (tileType != null)
                    {
                        results.Add(tileType);
                    }
                }

                return results.ToArray();
            }

            if (File.Exists(path))
            {
                if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    GD.PushError($"文件不是 JSON 格式，已跳过: '{path}'");
                    return Array.Empty<TileType>();
                }

                TileType tileType = LoadTileType(path);
                return tileType != null ? new[] { tileType } : Array.Empty<TileType>();
            }

            GD.PushError($"路径不存在，已跳过: '{path}'");
            return Array.Empty<TileType>();
        }

        /// <summary>
        /// 读取并校验单个 TileType JSON 文件。
        /// </summary>
        private static TileType LoadTileType(string filePath)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                GD.PushError($"文件名为空或无效: '{filePath}'");
                return null;
            }

            try
            {
                string jsonContent = File.ReadAllText(filePath);
                TileType tileType = JsonConvert.DeserializeObject<TileType>(jsonContent);
                if (tileType == null)
                {
                    GD.PushError($"TileType 反序列化失败: 文件 '{fileNameWithoutExtension}'");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(tileType.TileTypeName))
                {
                    GD.PushError($"TileTypeName 为空或无效: 文件 '{fileNameWithoutExtension}'");
                    return null;
                }

                if (tileType.TileTypeName != fileNameWithoutExtension)
                {
                    GD.PushError($"文件名与 TileTypeName 不匹配: 文件 '{fileNameWithoutExtension}' 中的 TileTypeName 为 '{tileType.TileTypeName}'");
                    return null;
                }

                return tileType;
            }
            catch (Exception ex)
            {
                GD.PushError($"读取文件失败 {filePath}: {ex.Message}");
                return null;
            }
        }

        // ================================================================================
        //                                  哈希构建
        // ================================================================================

        /// <summary>
        /// 构建 runId:typeName 映射的 SHA256 哈希。
        /// </summary>
        private static string BuildMappingHash(IReadOnlyList<TileType> tileTypes)
        {
            StringBuilder sb = new();
            for (int i = 0; i < tileTypes.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append('|');
                }

                sb.Append(tileTypes[i].TileTypeRunId);
                sb.Append(':');
                sb.Append(tileTypes[i].TileTypeName);
            }

            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
            StringBuilder hashSb = new(hashBytes.Length * 2);
            for (int i = 0; i < hashBytes.Length; i++)
            {
                hashSb.Append(hashBytes[i].ToString("x2"));
            }

            return hashSb.ToString();
        }

        // ================================================================================
        //                                  调试输出
        // ================================================================================

        private static void PrintLoadedTypes(IReadOnlyList<TileType> tileTypeList)
        {
            StringBuilder typesText = new();
            for (int i = 0; i < tileTypeList.Count; i++)
            {
                typesText.Append(tileTypeList[i].ToString() + ",");
            }

            GD.Print($"已加载 {tileTypeList.Count} 个 TileType:" + typesText);
        }

        // ================================================================================
        //                                  路径收集器
        // ================================================================================

        /// <summary>
        /// 轻量的路径字符串列表。
        /// </summary>
        private sealed class TileTypePathCollection : IEnumerable<string>
        {
            private readonly List<string> _paths = new();

            public int Count => _paths.Count;

            public void Add(string path)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    _paths.Add(path);
                }
            }

            public void Remove(string path)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    _paths.Remove(path);
                }
            }

            public void Clear()
            {
                _paths.Clear();
            }

            public string[] ToArray()
            {
                return _paths.ToArray();
            }

            public IEnumerator<string> GetEnumerator()
            {
                return _paths.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return _paths.GetEnumerator();
            }
            
        }
    }
}
