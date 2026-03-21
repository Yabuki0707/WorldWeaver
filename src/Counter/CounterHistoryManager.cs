using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

/// <summary>
/// 计数器历史数据管理器
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
    /// <summary>
    /// 历史数据表：存储读取到的历史计数器数据
    /// 键为计数器名称（恒定不变的唯一标识符），值为历史数据
    /// </summary>
    private readonly Dictionary<string, long> _historyData = [];

    /// <summary>
    /// 注册表：存储当前运行中的计数器实例
    /// 键为计数器名称，值为计数器实例引用
    /// 注册代表该计数器需要留存历史数据
    /// </summary>
    private readonly Dictionary<string, Counter> _registeredCounters = [];

    /// <summary>
    /// 用于线程安全的锁对象
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// JSON 序列化选项（缓存实例）
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// 计数器数据文件路径
    /// 每个管理器实例有独立的文件路径
    /// </summary>
    private string _filePath = "";

    /// <summary>
    /// 构造函数：创建管理器并指定文件路径
    /// 自动从文件加载历史数据到历史数据表
    /// </summary>
    /// <param name="filePath">计数器数据文件的完整路径</param>
    /// <exception cref="ArgumentNullException">文件路径为空时抛出</exception>
    public CounterHistoryManager(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            GD.PushError($"创建 CounterHistoryManager 失败: 文件路径为空");
            throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
        }

        this._filePath = filePath; // 保存文件路径
        LoadHistoryData(); // 加载历史数据
    }

    /// <summary>
    /// 从文件加载历史数据到历史数据表
    /// </summary>
    /// <returns>若加载成功返回 true</returns>
    private bool LoadHistoryData()
    {
        try
            {
                if (!File.Exists(_filePath))
                    return true; // 文件不存在则返回成功
                string json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                    return true; // 空文件则返回成功
                var data = JsonSerializer.Deserialize<Dictionary<string, long>>(json);
                if (data == null)
                {
                    GD.PushError("加载计数器历史数据失败: 反序列化结果为 null");
                    return false;
                }
                foreach (var kvp in data)
                {
                    _historyData[(string)kvp.Key] = (long)kvp.Value; // 加载到历史数据表
                }
                return true;
            }
        catch (Exception ex)
            {
                GD.PushError($"加载计数器历史数据失败: {ex.Message}");
                return false;
            }
    }

    /// <summary>
    /// 保存：将注册表内所有计数器的当前值写入文件
    /// </summary>
    /// <returns>若保存成功返回 true</returns>
    public bool Save()
    {
        lock (_lock)
            {
                try
                    {
                        var dataToSave = new Dictionary<string, long>();
                        foreach (var kvp in _registeredCounters)
                        {
                            Counter counter = kvp.Value;
                            // 检查计数器是否有效且计数值不是默认值
                            if (counter != null && counter.CountValue != long.MinValue)
                            {
                                dataToSave[kvp.Key] = counter.CountValue; // 获取计数器的当前计数值
                            }
                        }
                        string json = JsonSerializer.Serialize(dataToSave, _jsonOptions); // 序列化为 JSON
                        string directory = Path.GetDirectoryName(_filePath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory); // 创建目录（如果不存在）
                        }
                        File.WriteAllText(_filePath, json); // 写入文件
                        return true;
                    }
                catch (Exception ex)
                    {
                        GD.PushError($"保存计数器历史数据失败: {ex.Message}");
                        return false;
                    }
            }
    }

    /// <summary>
    /// 注册计数器：代表该计数器需要留存历史数据
    /// 计数器的名称是恒定不变的唯一标识符，用于持久化
    /// </summary>
    /// <param name="counter">计数器实例</param>
    /// <returns>若注册成功返回 true，若已存在返回 false</returns>
    public bool Register(Counter counter)
    {
        if (counter == null)
            return false;
        lock (_lock)
        {
            if (_registeredCounters.ContainsKey(counter.CounterName))
            {
                GD.PushWarning($"计数器 '{counter.CounterName}' 已存在，注册失败");
                return false;
            }
            _registeredCounters[counter.CounterName] = counter;
            return true;
        }
    }

    /// <summary>
    /// 注销计数器：从注册表中移除
    /// </summary>
    /// <param name="counterName">计数器名称（恒定不变的唯一标识符）</param>
    /// <returns>若注销成功返回 true</returns>
    public bool Unregister(string counterName)
    {
        lock (_lock)
            {
                bool removed = _registeredCounters.Remove(counterName); // 从注册表移除
                if (!removed)
                {
                    GD.PushWarning($"计数器 '{counterName}' 不存在于注册表中，注销失败");
                }
                return removed;
            }
    }

    /// <summary>
    /// 检查是否存在历史数据
    /// </summary>
    /// <param name="counterName">计数器名称（恒定不变的唯一标识符）</param>
    /// <returns>若存在返回 true</returns>
    public bool HasHistoryData(string counterName)
    {
        lock (_lock)
        {
            return _historyData.ContainsKey(counterName);
        }
    }

    /// <summary>
    /// 查询历史数据：供计数器初始化时查询其历史数据
    /// 若存在历史数据则返回，计数器应采用该值
    /// 注意：long.MinValue 不作为有效序号，仅表示无历史数据
    /// </summary>
    /// <param name="counterName">计数器名称（恒定不变的唯一标识符）</param>
    /// <returns>历史值，若无历史数据则返回 long.MinValue</returns>
    public long GetHistoryValue(string counterName)
    {
        lock (_lock)
            {
                if (_historyData.TryGetValue(counterName, out long historyValue))
                    return historyValue; // 返回历史值
                return long.MinValue; // 无历史数据则返回 MinValue
            }
    }

    /// <summary>
    /// 清空所有数据（历史数据表和注册表）
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _historyData.Clear();
            _registeredCounters.Clear();
        }
    }

    /// <summary>
    /// 获取已注册的计数器数量
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
    /// 获取历史数据条目数量
    /// </summary>
    public int HistoryDataCount
    {
        get
            {
                lock (_lock)
                    {
                        return _historyData.Count; // 返回历史数据条目数量
                    }
            }
    }
}
