using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 计数器类，提供线程安全的计数功能
/// 
/// 设计思想：
/// 计数器采用双标识符设计：名称（CounterName）作为恒定不变的唯一标识符用于持久化，
/// 而 UID 仅作为运行时唯一标识符用于实例查询。每个计数器可关联一个历史数据管理器实例，
/// 初始化时自动向管理器注册以留存历史数据。若存在历史数据则采用，否则使用 long.MinValue + 1 初始化。
/// 特别地，long.MinValue 不作为有效序号，仅表示无历史数据或错误状态。
/// 正常操作是获取后再自增（GetAndIncrement），因此 CountValue 中的值是未被使用的下一个序号。
/// </summary>
public class Counter : Object, IDisposable
{
    /// <summary>
    /// 关联的历史数据管理器实例
    /// 每个计数器必须关联一个管理器实例
    /// </summary>
    private readonly CounterHistoryManager _manager;

    /// <summary>
    /// 获取关联的历史数据管理器实例
    /// </summary>
    public CounterHistoryManager Manager => _manager;


    /// <summary>
    /// 计数器的运行时唯一标识符
    /// 仅用于运行时实例查询，不参与持久化
    /// </summary>
    private readonly Guid _counterUID = Guid.Empty;

    /// <summary>
    /// 获取计数器的运行时唯一标识符
    /// </summary>
    public Guid CounterUID => _counterUID;

    /// <summary>
    /// 计数器的名称
    /// 恒定不变的唯一标识符，用于持久化
    /// </summary>
    private readonly string _counterName;

    /// <summary>
    /// 获取计数器的名称
    /// </summary>
    public string CounterName => _counterName;

    /// <summary>
    /// 最大自增幅度
    /// 大于等于 3.5 万亿时：设为 1
    /// </summary>
    private const long MaxIncrementStep = 3500000000000L;

    /// <summary>
    /// 自增幅度
    /// </summary>
    private long _incrementStep = 1;

    /// <summary>
    /// 获取或设置自增幅度
    /// 小于 0 时：若为 long.MinValue 则设为 1，否则取绝对值
    /// 大于等于 3.5 万亿时：设为 1
    /// </summary>
    public long IncrementStep
    {
        get => _incrementStep;
        set
        {
            if (value < 0)
            {
                _incrementStep = value == long.MinValue ? 1 : -value;
                GD.PushError($"自增幅度无效: {value}，已自动调整为 {_incrementStep}");
                throw new ArgumentOutOfRangeException(nameof(value), $"自增幅度不能为负数，已自动调整为 {_incrementStep}");
            }
            else if (value == 0 || value >= MaxIncrementStep)
            {
                _incrementStep = 1;
                GD.PushError($"自增幅度无效: {value}，已自动调整为 1");
                throw new ArgumentOutOfRangeException(nameof(value), "自增幅度不能大于等于 3.5 万亿，已自动调整为 1");
            }
            else
            {
                _incrementStep = value;
            }
        }
    }

    /// <summary>
    /// 计数器当前计数值
    /// </summary>
    private long _value = long.MinValue;

    /// <summary>
    /// 获取计数器当前计数值
    /// [不推荐使用] 计数器的主要用途是自增获取唯一ID，直接读取值通常无意义
    /// </summary>
    public long CountValue
    {
        get
        {
            lock (_lock)
            {
                return _value; // 返回当前计数值
            }
        }
    }

    /// <summary>
    /// 是否已注册到历史数据管理器
    /// </summary>
    private bool _isRegistered = false;

    /// <summary>
    /// 获取是否已注册到历史数据管理器
    /// </summary>
    public bool IsRegistered => _isRegistered;

    /// <summary>
    /// 用于线程安全的锁对象
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// 构造函数
    /// 向历史数据管理器注册，代表需要留存历史数据
    /// 若存在历史数据则采用，否则使用 long.MinValue + 1 初始化
    /// 注意：long.MinValue 不作为有效序号，仅表示无数据或错误状态
    /// </summary>
    /// <param name="counterName">计数器名称（恒定不变的唯一标识符，用于持久化）</param>
    /// <param name="manager">历史数据管理器实例（可选，为 null 时不进行持久化）</param>
    /// <param name="incrementStep">自增幅度，必须大于 0 且小于 3.5 万亿，默认为 1</param>
    /// <exception cref="ArgumentOutOfRangeException">自增幅度无效时抛出</exception>
    public Counter(string counterName, CounterHistoryManager manager = null, long incrementStep = 1)
    {
        //自增幅度的有效修正值
        long adjustedStep = incrementStep;
        //自增幅度报错提醒
        bool invalidValue = false;
        if (incrementStep < 0)
        {
            adjustedStep = incrementStep == long.MinValue ? 1 : -incrementStep;
            invalidValue = true;
        }
        else if (incrementStep == 0 ||incrementStep >= MaxIncrementStep)
        {
            adjustedStep = 1;
            invalidValue = true;
        }
        
        if (invalidValue)
        {
            GD.PushError($"自增幅度无效: {incrementStep}，已自动调整为 {adjustedStep}");
        }

        this._manager = manager; // 保存管理器引用（可为 null）
        this._counterName = counterName; // 保存计数器名称
        this._counterUID = Guid.NewGuid(); // 生成新的 UID
        this._incrementStep = adjustedStep;// 设置自增幅度(有效修正值)

        // 若管理器存在，则尝试获取历史数据并注册
        if (_manager != null)
        {
            long historyValue = _manager.GetHistoryValue(CounterName); // 从管理器获取历史值
            _value = historyValue == long.MinValue ? long.MinValue + 1 : historyValue; // 无历史数据时使用 MinValue + 1

            if (_manager.Register(this))
            {
                _isRegistered = true; // 注册成功
            }
            else
            {
                GD.PushError($"计数器 '{counterName}' 注册失败"); // 注册失败输出错误
            }
        }
        else
        {
            _value = long.MinValue + 1;
        }

        lock (_instancesLock)
        {
            _counterInstances[CounterUID] = this; // 添加到实例映射表
        }
    }

    /// <summary>
    /// 执行自增操作（不返回值）
    /// </summary>
    public void Increment()
    {
        lock (_lock)
        {
            _value += IncrementStep;
        }
    }

    /// <summary>
    /// 返回当前值并执行自增操作
    /// </summary>
    /// <returns>自增前的值</returns>
    public long GetAndIncrement()
    {
        lock (_lock)
        {
            long beforeValue = _value;
            _value += IncrementStep;
            return beforeValue;
        }
    }

    /// <summary>
    /// 执行普通自增操作（自增幅度固定为 1，不返回值）
    /// </summary>
    public void IncrementOne()
    {
        lock (_lock)
        {
            _value++;
        }
    }

    /// <summary>
    /// 返回当前值并执行普通自增操作（自增幅度固定为 1）
    /// </summary>
    /// <returns>自增前的值</returns>
    public long GetAndIncrementOne()
    {
        lock (_lock)
        {
            long beforeValue = _value;
            _value++;
            return beforeValue;
        }
    }

    /// <summary>
    /// 获取计数器当前计数值
    /// [不推荐使用] 计数器的主要用途是自增获取唯一ID，直接读取值通常无意义
    /// </summary>
    /// <returns>计数器当前计数值</returns>
    public long GetValue()
    {
        lock (_lock)
        {
            return _value;
        }
    }

    /// <summary>
    /// 设置计数器的计数值
    /// [不推荐使用] 会破坏计数器的自增语义，可能导致ID重复
    /// </summary>
    /// <param name="value">目标计数值</param>
    public void SetValue(long value)
    {
        GD.PushWarning($"计数器 '{CounterName}' 被手动设置值，这可能破坏自增语义并导致 ID 重复");
        
        lock (_lock)
        {
            _value = value;
        }
    }

    /// <summary>
    /// 释放资源
    /// 从历史数据管理器和实例映射表中注销
    /// </summary>
    public void Dispose()
    {
        if (_isRegistered)
        {
            _manager.Unregister(CounterName);
            _isRegistered = false;
        }
        lock (_instancesLock)
        {
            _counterInstances.Remove(CounterUID);
        }
        
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 析构函数
    /// </summary>
    ~Counter()
    {
        Dispose();
    }

    /// <summary>
    /// 计数器实例映射表，以 UID 为键存储所有计数器实例
    /// UID仅是运行时的唯一标识符
    /// </summary>
    private static readonly Dictionary<Guid, Counter> _counterInstances = [];

    /// <summary>
    /// 用于实例映射表的线程安全锁
    /// </summary>
    private static readonly object _instancesLock = new();

    /// <summary>
    /// 根据 UID 查询计数器实例
    /// UID仅是运行时的唯一标识符
    /// </summary>
    /// <param name="uid">计数器的运行时唯一标识符</param>
    /// <returns>若存在则返回对应计数器实例，否则返回 null</returns>
    public static Counter GetCounterByUID(Guid uid)
    {
        lock (_instancesLock)
        {
            if (_counterInstances.TryGetValue(uid, out Counter counter))
                return counter;
            return null;
        }
    }

    /// <summary>
    /// 根据 UID 查询计数器实例
    /// UID仅是运行时的唯一标识符
    /// </summary>
    /// <param name="uid">计数器的运行时唯一标识符字符串</param>
    /// <returns>若存在则返回对应计数器实例，否则返回 null</returns>
    public static Counter GetCounterByUID(string uid)
    {
        if (Guid.TryParse(uid, out Guid guid))
            return GetCounterByUID(guid);
        return null;
    }
}
