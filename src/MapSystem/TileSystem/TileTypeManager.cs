using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;

namespace WorldWeaver.MapSystem.TileSystem
{
    /// <summary>
    /// TileType种类管理器单例类，用于管理TileType种类信息
    /// TileTypeRunId从1开始分配,0为无效TileTypeRunId
    /// </summary>
    public static class TileTypeManager
    {
        /*******************************
                资源路径与初始化
        ********************************/

        /// <summary>
        /// 资源目录常量，存储tiles所在的位置
        /// </summary>
        private static readonly string _resourceDir = ProjectSettings.GlobalizePath("res://assets/tiles");


        /// <summary>
        /// 初始化状态
        /// </summary>
        public static bool Initialized { get; private set; } = false;


        /*******************************
                TileType数据存储
        ********************************/

        /// <summary>
        /// TileType种类列表，索引=运行ID
        /// </summary>
        private static readonly List<TileType> _tileTypes = [];

        /// <summary>
        /// TileType名称与运行ID对应的字典
        /// </summary>
        private static readonly Dictionary<string, int> _tileTypeNameToRunId = [];

        /// <summary>
        /// 最小可用运行ID（内部计数器，存储下一个可分配的RunId；RunId为连续分配的只增ID，不支持删除）
        /// </summary>
        private static int _minAvailableRunId = 1;

        /// <summary>
        /// 获取最小可用运行ID（RunId为连续分配的只增ID，不支持删除）
        /// </summary>
        public static int MinAvailableRunId => _minAvailableRunId;

        /// <summary>
        /// 获取TileType种类总数，总数等于最小可分配id-1
        /// </summary>
        public static int TypeCount => _minAvailableRunId - 1;


        /// <summary>
        /// 确保TileTypeManager已初始化，若未初始化则报错并返回false
        /// </summary>
        /// <returns>若已初始化则返回true，否则返回false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool EnsureInitialized()
        {
            if (Initialized)
                return true;

            GD.PushError("TileTypeManager尚未初始化，请先调用Initialize()方法");
            return false;
        }


        /*******************************
                初始化
        ********************************/

        /// <summary>
        /// 初始化函数，读取tiles下所有的json文件
        /// </summary>
        /// <param name="printTileTypes">是否打印每个TileType的信息</param>
        public static void Initialize(bool printTileTypes = false)
        {
            if (Initialized)
            {
                GD.PushError("TileTypeManager已经初始化，禁止重复调用Initialize()方法");
                return;
            }

            if (!Directory.Exists(_resourceDir))
            {
                GD.PushError($"无法打开目录: {_resourceDir}");
                return;
            }

            // 在索引0处添加空的TileType
            _tileTypes.Add(new TileType
            {
                TileTypeName = "empty",
                TileTypeRunId = 0
            });
            
            // 读取文件并排序
            string[] files = Directory.GetFiles(_resourceDir, "*.json");
            Array.Sort(files, StringComparer.Ordinal); 
            // 对文件进行遍历
            foreach (string filePath in files)
            {
                // 提取文件名（无后缀）作为tileTypeName
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
                {
                    GD.PushError($"文件名为空或无效: '{filePath}'");
                    continue;
                }

                try
                {
                    // 使用C#内置方法读取JSON文件内容
                    string jsonContent = File.ReadAllText(filePath);
                    TileType tileType = JsonConvert.DeserializeObject<TileType>(jsonContent);
                    if (tileType == null)
                    {
                        GD.PushError($"TileType反序列化失败: 文件 '{fileNameWithoutExtension}'");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(tileType.TileTypeName))
                    {
                        GD.PushError($"tileTypeName为空或无效: 文件 '{fileNameWithoutExtension}'");
                        continue;
                    }

                    // 验证文件名（无后缀）是否与tileTypeName一致（卫语句）
                    if (tileType.TileTypeName != fileNameWithoutExtension)
                    {
                        GD.PushError($"文件名与tileTypeName不匹配: 文件 '{fileNameWithoutExtension}' 中的tileTypeName为 '{tileType.TileTypeName}'");
                        continue;
                    }

                    // 检测重复的TileTypeName（卫语句）
                    if (_tileTypeNameToRunId.ContainsKey(tileType.TileTypeName))
                    {
                        GD.PushError($"跳过重复的TileTypeName: '{tileType.TileTypeName}'");
                        continue;
                    }

                    // 使用最小可用运行ID分配编号
                    tileType.TileTypeRunId = _minAvailableRunId;
                    _minAvailableRunId++;
                    
                    // 加入到列表中（索引=运行ID）
                    _tileTypes.Add(tileType);
                    
                    // 加入到tileTypeName与运行ID对应的字典
                    _tileTypeNameToRunId[tileType.TileTypeName] = tileType.TileTypeRunId;
                }
                catch (Exception ex)
                {
                    GD.PushError($"读取文件失败 {filePath}: {ex.Message}");
                }
            }

            // 打印TileType信息（如果需要）
            if (printTileTypes)
            {
                GD.Print($"已加载 {_tileTypes.Count} 个TileType:");
                foreach (var tileType in _tileTypes)
                {
                    GD.Print(tileType.ToString());
                }
            }

            // 设置初始化状态为true
            Initialized = true;
        }


        /// <summary>
        /// 添加新的TileType种类（仅允许在主线程调用）
        /// </summary>
        /// <param name="tileType">TileType数据</param>
        /// <returns>分配的运行ID，若未初始化或添加失败则返回0</returns>
        public static int AddType(TileType tileType)
        {
            if (!EnsureInitialized())
                return 0;

            if (tileType == null)
            {
                GD.PushError("无法添加空的TileType数据");
                return 0;
            }

            if (string.IsNullOrWhiteSpace(tileType.TileTypeName))
            {
                GD.PushError("TileTypeName不能为空");
                return 0;
            }

            if (_tileTypeNameToRunId.ContainsKey(tileType.TileTypeName))
            {
                GD.PushError($"TileType名称 '{tileType.TileTypeName}' 已存在，无法重复添加");
                return 0;
            }

            // 使用最小可用运行ID分配编号
            tileType.TileTypeRunId = _minAvailableRunId;
            _minAvailableRunId++;
            
            // 添加到列表
            _tileTypes.Add(tileType);
            
            // 添加到字典
            _tileTypeNameToRunId[tileType.TileTypeName] = tileType.TileTypeRunId;

            return tileType.TileTypeRunId;
        }


        /*******************************
                TileType数据访问
        ********************************/

        /// <summary>
        /// 根据运行ID获取TileType数据
        /// </summary>
        /// <param name="runId">TileType的运行ID</param>
        /// <returns>对应的TileType数据，若未初始化或不存在则返回null</returns>
        public static TileType GetTypeByRunId(int runId)
        {
            if (!EnsureInitialized())
                return null;

            if (runId >= 0 && runId < _tileTypes.Count)
            {
                return _tileTypes[runId];
            }
            return null;
        }


        /// <summary>
        /// 根据名称获取TileType数据
        /// </summary>
        /// <param name="name">TileType的名称</param>
        /// <returns>对应的TileType数据，若未初始化或不存在则返回null</returns>
        public static TileType GetTypeByName(string name)
        {
            if (!EnsureInitialized())
                return null;

            if (name == null) // 防御性检查
                return null;
            if (_tileTypeNameToRunId.TryGetValue(name, out int runId))// 检查是否存在对应的运行ID
            {
                return _tileTypes[runId];// 返回对应的TileType数据
            }
            return null;
        }


        /// <summary>
        /// 根据名称获取TileType的运行ID
        /// </summary>
        /// <param name="name">TileType的名称</param>
        /// <returns>对应的运行ID，若未初始化或不存在则返回0</returns>
        public static int GetRunIdByName(string name)
        {
            if (!EnsureInitialized())
                return 0;

            if (name == null) // 防御性检查
                return 0;
            if (_tileTypeNameToRunId.TryGetValue(name, out int runId))// 检查是否存在对应的运行ID
            {
                return runId;// 返回对应的运行ID
            }
            return 0;
        }


        /// <summary>
        /// 根据运行ID获取TileType的名称
        /// </summary>
        /// <param name="runId">TileType的运行ID</param>
        /// <returns>对应的名称，若未初始化或不存在则返回null</returns>
        public static string GetNameByRunId(int runId)
        {
            if (!EnsureInitialized())
                return null;

            if (runId >= 0 && runId < _tileTypes.Count)
            {
                return _tileTypes[runId].TileTypeName;
            }
            return null;
        }


        /// <summary>
        /// 检查指定名称的TileType是否存在
        /// </summary>
        /// <param name="name">TileType的名称</param>
        /// <returns>若存在则返回true，若未初始化或不存在则返回false</returns>
        public static bool ContainsType(string name)
        {
            if (!EnsureInitialized())
                return false;

            if (name == null) // 防御性检查
                return false;
            return _tileTypeNameToRunId.ContainsKey(name);
        }


        /// <summary>
        /// 检查指定运行ID的TileType是否存在
        /// </summary>
        /// <param name="runId">TileType的运行ID</param>
        /// <returns>若存在则返回true，若未初始化或不存在则返回false</returns>
        public static bool ContainsType(int runId)
        {
            if (!EnsureInitialized())
                return false;

            return runId >= 0 && runId < _tileTypes.Count;
        }


    }
}
