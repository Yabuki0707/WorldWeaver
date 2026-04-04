using Godot;
using System;
using System.Collections.Generic;

namespace WorldWeaver.CounterSystem
{
    /// <summary>
    /// 计数器类，提供线程安全的计数功能。
    ///
    /// 设计思想：
    /// 计数器采用双标识符设计：名称（CounterName）作为恒定不变的唯一标识符用于持久化，
    /// 而 UID 仅作为运行时唯一标识符用于实例查询。每个计数器可关联一个历史数据管理器实例，
    /// 初始化时自动向管理器注册以留存历史数据。若存在历史数据则采用，否则使用 long.MinValue + 1 初始化。
    /// 特别地，long.MinValue 不作为有效序号，仅表示无历史数据或错误状态。
    /// 正常操作是获取后再自增（GetAndIncrement），因此 CountValue 中的值是未被使用的下一个序号。
    /// </summary>
    public class Counter : IDisposable
    {
        // ================================================================================
        //                                  常量
        // ================================================================================

        /// <summary>
        /// 最大自增幅度。
        /// 大于等于 3.5 万亿时：设为 1。
        /// </summary>
        private const long MAX_INCREMENT_STEP = 3500000000000L;


        // ================================================================================
        //                                  实例字段与属性
        // ================================================================================

        /// <summary>
        /// 关联的历史数据管理器实例。
        /// 每个计数器必须关联一个管理器实例。
        /// </summary>
        private readonly CounterHistoryManager _manager;

        /// <summary>
        /// 获取关联的历史数据管理器实例。
        /// </summary>
        public CounterHistoryManager Manager => _manager;


        /// <summary>
        /// 计数器的运行时唯一标识符。
        /// 仅用于运行时实例查询，不参与持久化。
        /// </summary>
        public readonly Guid CounterUid;


        /// <summary>
        /// 获取计数器的名称。
        /// 恒定不变的唯一标识符，用于持久化。
        /// </summary>
        public string CounterName { get; }


        /// <summary>
        /// 自增幅度。
        /// </summary>
        private long _incrementStep;

        /// <summary>
        /// 获取或设置自增幅度。
        /// 小于 0 时：若为 long.MinValue 则设为 1，否则取绝对值。
        /// 大于等于 3.5 万亿时：设为 1。
        /// </summary>
        public long IncrementStep
        {
            get => _incrementStep;
            set
            {
                _incrementStep = NormalizeIncrementStep(value, CounterName);
            }
        }


        /// <summary>
        /// 计数器当前计数值。
        /// </summary>
        private long _value;

        /// <summary>
        /// 获取计数器当前计数值。
        /// [不推荐使用] 计数器的主要用途是自增获取唯一ID，直接读取值通常无意义。
        /// </summary>
        public long CountValue
        {
            get
            {
                lock (_lock)
                {
                    return _value;
                }
            }
        }


        /// <summary>
        /// 是否已注册到历史数据管理器。
        /// </summary>
        public bool IsRegistered { get; private set; }


        /// <summary>
        /// 用于线程安全的锁对象。
        /// </summary>
        private readonly object _lock = new();


        // ================================================================================
        //                                  静态字段
        // ================================================================================

        /// <summary>
        /// 计数器实例映射表，以 UID 为键存储所有计数器实例。
        /// UID 仅是运行时的唯一标识符。
        /// </summary>
        private static readonly Dictionary<Guid, Counter> _counterInstances = [];

        /// <summary>
        /// 用于实例映射表的线程安全锁。
        /// </summary>
        private static readonly object _instancesLock = new();


        // ================================================================================
        //                                  构造
        // ================================================================================

        /// <summary>
        /// 创建计数器实例。
        /// 向历史数据管理器注册，代表需要留存历史数据。
        /// 若存在历史数据则采用，否则使用 long.MinValue + 1 初始化。
        /// 注意：long.MinValue 不作为有效序号，仅表示无数据或错误状态。
        /// </summary>
        /// <param name="counterName">计数器名称（恒定不变的唯一标识符，用于持久化）。</param>
        /// <param name="manager">历史数据管理器实例（可选，为 null 时不进行持久化）。</param>
        /// <param name="incrementStep">自增幅度，必须大于 0 且小于 3.5 万亿，默认为 1。</param>
        public Counter(string counterName, CounterHistoryManager manager = null, long incrementStep = 1)
        {
            _manager = manager;
            CounterName = counterName;
            CounterUid = Guid.NewGuid();
            _incrementStep = NormalizeIncrementStep(incrementStep, CounterName);

            if (_manager != null)
            {
                // 从管理器获取历史值。
                long historyValue = _manager.GetHistoryValue(CounterName);
                _value = historyValue == long.MinValue ? long.MinValue + 1 : historyValue;

                if (_manager.Register(this))
                {
                    IsRegistered = true;
                }
                else
                {
                    // 用于日志显示的计数器名称。
                    string counterDisplayName = string.IsNullOrWhiteSpace(CounterName) ? "<未命名计数器>" : CounterName;
                    GD.PushWarning($"[CounterSystem]:Counter {counterDisplayName} 在注册到历史数据管理器时发生警告，CounterName={counterDisplayName}，ManagerType={nameof(CounterHistoryManager)}，注册失败。");
                }
            }
            else
            {
                _value = long.MinValue + 1;
            }

            lock (_instancesLock)
            {
                _counterInstances[CounterUid] = this;
            }
        }


        // ================================================================================
        //                                  计数操作
        // ================================================================================

        /// <summary>
        /// 执行自增操作（不返回值）。
        /// </summary>
        public void Increment()
        {
            lock (_lock)
            {
                _value += IncrementStep;
            }
        }

        /// <summary>
        /// 返回当前值并执行自增操作。
        /// </summary>
        /// <returns>自增前的值。</returns>
        public long GetAndIncrement()
        {
            lock (_lock)
            {
                // 记录自增前的值。
                long beforeValue = _value;
                _value += IncrementStep;
                return beforeValue;
            }
        }

        /// <summary>
        /// 执行普通自增操作（自增幅度固定为 1，不返回值）。
        /// </summary>
        public void IncrementOne()
        {
            lock (_lock)
            {
                _value++;
            }
        }

        /// <summary>
        /// 返回当前值并执行普通自增操作（自增幅度固定为 1）。
        /// </summary>
        /// <returns>自增前的值。</returns>
        public long GetAndIncrementOne()
        {
            lock (_lock)
            {
                // 记录自增前的值。
                long beforeValue = _value;
                _value++;
                return beforeValue;
            }
        }

        /// <summary>
        /// 获取计数器当前计数值。
        /// [不推荐使用] 计数器的主要用途是自增获取唯一ID，直接读取值通常无意义。
        /// </summary>
        /// <returns>计数器当前计数值。</returns>
        public long GetValue()
        {
            lock (_lock)
            {
                return _value;
            }
        }

        /// <summary>
        /// 设置计数器的计数值。
        /// [不推荐使用] 会破坏计数器的自增语义，可能导致ID重复。
        /// </summary>
        /// <param name="value">目标计数值。</param>
        public void SetValue(long value)
        {
            // 用于日志显示的计数器名称。
            string counterDisplayName = string.IsNullOrWhiteSpace(CounterName) ? "<未命名计数器>" : CounterName;
            GD.PushWarning($"[CounterSystem]:Counter {counterDisplayName} 在手动设置计数值时发生警告，CounterName={counterDisplayName}，value={value}，这可能破坏自增语义并导致ID重复。");

            lock (_lock)
            {
                _value = value;
            }
        }


        // ================================================================================
        //                                  生命周期
        // ================================================================================

        /// <summary>
        /// 释放资源。
        /// 从历史数据管理器和实例映射表中注销。
        /// </summary>
        public void Dispose()
        {
            if (IsRegistered)
            {
                _manager.Unregister(CounterName);
                IsRegistered = false;
            }

            lock (_instancesLock)
            {
                _counterInstances.Remove(CounterUid);
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 析构函数。
        /// </summary>
        ~Counter()
        {
            Dispose();
        }


        // ================================================================================
        //                                  实例查询
        // ================================================================================

        /// <summary>
        /// 根据 UID 查询计数器实例。
        /// UID 仅是运行时的唯一标识符。
        /// </summary>
        /// <param name="uid">计数器的运行时唯一标识符。</param>
        /// <returns>若存在则返回对应计数器实例，否则返回 null。</returns>
        public static Counter GetCounterByUid(Guid uid)
        {
            lock (_instancesLock)
            {
                return _counterInstances.GetValueOrDefault(uid, null);
            }
        }

        /// <summary>
        /// 根据 UID 查询计数器实例。
        /// UID 仅是运行时的唯一标识符。
        /// </summary>
        /// <param name="uid">计数器的运行时唯一标识符字符串。</param>
        /// <returns>若存在则返回对应计数器实例，否则返回 null。</returns>
        public static Counter GetCounterByUid(string uid)
        {
            if (Guid.TryParse(uid, out Guid guid))
            {
                return GetCounterByUid(guid);
            }

            return null;
        }


        // ================================================================================
        //                                  私有工具
        // ================================================================================

        /// <summary>
        /// 将输入的自增幅度转换为有效值，并在非法时输出错误日志。
        /// </summary>
        /// <param name="incrementStep">原始自增幅度。</param>
        /// <param name="counterName">计数器名称，用于日志输出。</param>
        /// <returns>转换后的有效自增幅度。</returns>
        private static long NormalizeIncrementStep(long incrementStep, string counterName)
        {
            // 用于日志显示的计数器名称。
            string counterDisplayName = string.IsNullOrWhiteSpace(counterName) ? "<未命名计数器>" : counterName;

            if (incrementStep < 0)
            {
                // 修正后的自增幅度。
                long adjustedStep = incrementStep == long.MinValue ? 1 : -incrementStep;
                GD.PushError($"[CounterSystem]:Counter {counterDisplayName} 在修正自增幅度时发生错误，CounterName={counterDisplayName}，incrementStep={incrementStep} 非法，已自动调整为 {adjustedStep}。");
                return adjustedStep;
            }

            if (incrementStep == 0 || incrementStep >= MAX_INCREMENT_STEP)
            {
                GD.PushError($"[CounterSystem]:Counter {counterDisplayName} 在修正自增幅度时发生错误，CounterName={counterDisplayName}，incrementStep={incrementStep} 非法，已自动调整为 1。");
                return 1;
            }

            return incrementStep;
        }
    }
}
