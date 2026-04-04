using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WorldWeaver.CounterSystem
{
    /// <summary>
    /// 计数器历史数据管理器。
    ///
    /// 设计思想：
    /// 采用实例化管理模式，每个管理器实例独立管理一组计数器的历史数据，创建时需绑定文件路径用于持久化存储。
    /// 内部维护两张表：历史数据表存储持久化的计数器数据，仅在实例化时从文件加载；
    /// 注册表存储当前运行中的计数器实例，代表其需要留存历史数据。
    /// 保存时将注册表内所有计数器的当前值同步到历史数据表并写入文件。
    /// 计数器的名称是恒定不变的唯一标识符用于持久化，UID 仅是运行时标识符。
    /// long.MinValue 不作为有效序号，仅表示无历史数据。
    /// </summary>
    public class CounterHistoryManager : Object
    {
        // ================================================================================
        //                                  实例字段
        // ================================================================================

        /// <summary>
        /// 历史数据表：存储读取到的历史计数器数据。
        /// 键为计数器名称（恒定不变的唯一标识符），值为历史数据。
        /// </summary>
        private readonly Dictionary<string, long> _historyData = [];

        /// <summary>
        /// 注册表：存储当前运行中的计数器实例。
        /// 键为计数器名称，值为计数器实例引用。
        /// 注册代表该计数器需要留存历史数据。
        /// </summary>
        private readonly Dictionary<string, Counter> _registeredCounters = [];

        /// <summary>
        /// 用于线程安全的锁对象。
        /// </summary>
        private readonly object _lock = new();

        /// <summary>
        /// 计数器数据文件路径。
        /// 每个管理器实例有独立的文件路径。
        /// </summary>
        private readonly string _filePath;


        // ================================================================================
        //                                  静态字段
        // ================================================================================

        /// <summary>
        /// JSON 序列化选项（缓存实例）。
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };


        // ================================================================================
        //                                  构造
        // ================================================================================

        /// <summary>
        /// 创建管理器并指定文件路径。
        /// 自动从文件加载历史数据到历史数据表。
        /// </summary>
        /// <param name="filePath">计数器数据文件的完整路径。</param>
        /// <exception cref="ArgumentNullException">文件路径为空时抛出。</exception>
        public CounterHistoryManager(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                GD.PushError($"[CounterSystem]:CounterHistoryManager <未初始化> 在创建时发生错误，filePath={filePath ?? "<null>"} 非法。");
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
            }

            _filePath = filePath;

            // 初始化阶段的历史数据加载结果。
            bool loadResult = LoadHistoryData();
            if (loadResult == false)
            {
                GD.PushWarning($"[CounterSystem]:CounterHistoryManager {_filePath} 在初始化时发生警告，filePath={_filePath} 的历史数据未成功加载，具体原因请参考前序加载日志。");
            }
        }


        // ================================================================================
        //                                  持久化
        // ================================================================================

        /// <summary>
        /// 从文件加载历史数据到历史数据表。
        /// </summary>
        /// <returns>若加载成功返回 true。</returns>
        private bool LoadHistoryData()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    GD.PushWarning($"[CounterSystem]:CounterHistoryManager {_filePath} 在加载历史数据时发生警告，filePath={_filePath} 对应的历史数据文件不存在。");
                    return false;
                }

                // 读取历史数据文件内容。
                string json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    GD.PushWarning($"[CounterSystem]:CounterHistoryManager {_filePath} 在加载历史数据时发生警告，filePath={_filePath} 对应的历史数据文件内容为空。");
                    return true;
                }

                // 反序列化后的历史数据。
                Dictionary<string, long> data = JsonSerializer.Deserialize<Dictionary<string, long>>(json);
                if (data == null)
                {
                    GD.PushWarning($"[CounterSystem]:CounterHistoryManager {_filePath} 在加载历史数据时发生警告，filePath={_filePath} 的反序列化结果为 null。");
                    return false;
                }

                foreach (KeyValuePair<string, long> kvp in data)
                {
                    _historyData[kvp.Key] = kvp.Value;
                }

                return true;
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[CounterSystem]:CounterHistoryManager {_filePath} 在加载历史数据时发生警告，filePath={_filePath}，exception={ex.Message}。");
                return false;
            }
        }

        /// <summary>
        /// 将注册表内所有计数器的当前值写入文件。
        /// </summary>
        /// <returns>若保存成功返回 true。</returns>
        public bool Save()
        {
            lock (_lock)
            {
                try
                {
                    // 待保存的历史数据副本。
                    Dictionary<string, long> dataToSave = new();
                    foreach (KeyValuePair<string, Counter> kvp in _registeredCounters)
                    {
                        // 当前遍历到的计数器实例。
                        Counter counter = kvp.Value;
                        if (counter != null && counter.CountValue != long.MinValue)
                        {
                            dataToSave[kvp.Key] = counter.CountValue;
                        }
                    }

                    // 序列化后的 JSON 文本。
                    string json = JsonSerializer.Serialize(dataToSave, _jsonOptions);

                    // 目标文件所在目录。
                    string directory = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(_filePath, json);
                    return true;
                }
                catch (Exception ex)
                {
                    GD.PushError($"[CounterSystem]:CounterHistoryManager {_filePath} 在保存历史数据时发生错误，filePath={_filePath}，exception={ex.Message}。");
                    return false;
                }
            }
        }


        // ================================================================================
        //                                  注册管理
        // ================================================================================

        /// <summary>
        /// 注册计数器：代表该计数器需要留存历史数据。
        /// 计数器的名称是恒定不变的唯一标识符，用于持久化。
        /// </summary>
        /// <param name="counter">计数器实例。</param>
        /// <returns>若注册成功返回 true，若已存在返回 false。</returns>
        public bool Register(Counter counter)
        {
            if (counter == null)
            {
                GD.PushWarning($"[CounterSystem]:CounterHistoryManager {_filePath} 在注册计数器时发生警告，filePath={_filePath}，counter=null。");
                return false;
            }

            lock (_lock)
            {
                if (_registeredCounters.TryAdd(counter.CounterName, counter))
                {
                    return true;
                }

                // 用于日志显示的计数器名称。
                string counterDisplayName = string.IsNullOrWhiteSpace(counter.CounterName) ? "<未命名计数器>" : counter.CounterName;
                GD.PushWarning($"[CounterSystem]:CounterHistoryManager {_filePath} 在注册计数器时发生警告，filePath={_filePath}，CounterName={counterDisplayName} 已存在于注册表中。");
                return false;
            }
        }

        /// <summary>
        /// 注销计数器：从注册表中移除。
        /// </summary>
        /// <param name="counterName">计数器名称（恒定不变的唯一标识符）。</param>
        /// <returns>若注销成功返回 true。</returns>
        public bool Unregister(string counterName)
        {
            lock (_lock)
            {
                // 是否成功移除计数器。
                bool removed = _registeredCounters.Remove(counterName);
                if (removed == false)
                {
                    // 用于日志显示的计数器名称。
                    string counterDisplayName = string.IsNullOrWhiteSpace(counterName) ? "<未命名计数器>" : counterName;
                    GD.PushWarning($"[CounterSystem]:CounterHistoryManager {_filePath} 在注销计数器时发生警告，filePath={_filePath}，CounterName={counterDisplayName} 不存在于注册表中。");
                }

                return removed;
            }
        }


        // ================================================================================
        //                                  查询
        // ================================================================================

        /// <summary>
        /// 检查是否存在历史数据。
        /// </summary>
        /// <param name="counterName">计数器名称（恒定不变的唯一标识符）。</param>
        /// <returns>若存在返回 true。</returns>
        public bool HasHistoryData(string counterName)
        {
            lock (_lock)
            {
                return _historyData.ContainsKey(counterName);
            }
        }

        /// <summary>
        /// 查询历史数据：供计数器初始化时查询其历史数据。
        /// 若存在历史数据则返回，计数器应采用该值。
        /// 注意：long.MinValue 不作为有效序号，仅表示无历史数据。
        /// </summary>
        /// <param name="counterName">计数器名称（恒定不变的唯一标识符）。</param>
        /// <returns>历史值，若无历史数据则返回 long.MinValue。</returns>
        public long GetHistoryValue(string counterName)
        {
            lock (_lock)
            {
                return _historyData.GetValueOrDefault(counterName, long.MinValue);
            }
        }


        // ================================================================================
        //                                  维护
        // ================================================================================

        /// <summary>
        /// 清空所有数据（历史数据表和注册表）。
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _historyData.Clear();
                _registeredCounters.Clear();
            }
        }


        // ================================================================================
        //                                  统计属性
        // ================================================================================

        /// <summary>
        /// 获取已注册的计数器数量。
        /// </summary>
        public int RegisteredCount
        {
            get
            {
                lock (_lock)
                {
                    return _registeredCounters.Count;
                }
            }
        }

        /// <summary>
        /// 获取历史数据条目数量。
        /// </summary>
        public int HistoryDataCount
        {
            get
            {
                lock (_lock)
                {
                    return _historyData.Count;
                }
            }
        }
    }
}
