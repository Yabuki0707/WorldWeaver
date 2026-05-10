using System;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 全局 System 容器。自身是 IGameSystem 以充当访问者身份，但不入系统表。
    /// </summary>
    public class GlobalSystemsManager : IGameSystem, IGlobalSystemsManager
    {
        /// <summary>
        /// 声明表，Key 为 SystemName。TryAdd 保证先到先得。
        /// </summary>
        private readonly Dictionary<string, IGlobalSystem> _declared = new();

        /// <summary>
        /// 系统表，初始化完成后可供查询。Key 为 SystemName。
        /// </summary>
        private readonly Dictionary<string, IGlobalSystem> _systemTable = new();

        // ================================================================================
        //                           IGameSystem — 显式实现
        // ================================================================================

        /// <summary>
        /// 容器自身的 SystemName，仅用作 GetGlobalSystem 的 visitor 身份标识。
        /// </summary>
        public string SystemName => "GlobalSystemsManager";

        /// <summary>
        /// 容器卸载——与游戏进程同生命周期。
        /// </summary>
        void IGameSystem.Uninstall()
        {
        }

        // ================================================================================
        //                                   事件
        // ================================================================================

        /// <summary>
        /// 全局 System 注册事件。在 Initialize 开头触发。
        /// </summary>
        public event Action<IGlobalSystemsManager> GlobalSystemRegistering;

        /// <summary>
        /// 全部全局 System 初始化完毕后触发。
        /// </summary>
        public event Action GlobalSystemsInitialized;

        // ================================================================================
        //                                   属性
        // ================================================================================

        /// <summary>
        /// 是否已完成初始化。初始化后拒绝新的 System 注册。
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 系统表中已注册的 System 数量。
        /// </summary>
        public int Count => _systemTable.Count;

        /// <summary>
        /// 检查指定名称的 System 是否在系统表中。
        /// </summary>
        public bool ContainsKey(string systemName)
        {
            return _systemTable.ContainsKey(systemName);
        }

        // ================================================================================
        //                           System 间查询（枢纽）
        // ================================================================================

        /// <summary>
        /// 一切全局 System 查询的唯一枢纽。visitor 为请求方自身，仅 IGameSystem 可调用。
        /// </summary>
        public IGlobalSystem GetGlobalSystem(IGameSystem visitor, string target)
        {
            if (visitor == null)
            {
                GD.PushError($"[GlobalSystemsManager]:GetGlobalSystem 的 visitor 为 null，查询 {target} 被拒绝。");
                return null;
            }

            _systemTable.TryGetValue(target, out IGlobalSystem system);
            return system;
        }

        /// <summary>
        /// System 间查询索引器，直接委托 GetGlobalSystem。
        /// </summary>
        public IGlobalSystem this[IGameSystem visitor, string target] => GetGlobalSystem(visitor, target);

        // ================================================================================
        //                           容器代理查询（公开）
        // ================================================================================

        /// <summary>
        /// 容器以自身为 visitor 查询——非 System 上下文访问全局 System 的唯一入口。
        /// </summary>
        public IGlobalSystem ResolveSystem(string systemName)
        {
            return GetGlobalSystem(this, systemName);
        }

        // ================================================================================
        //                              注册与初始化
        // ================================================================================

        /// <summary>
        /// 注入一个全局 System 到声明表。
        /// 若已初始化则拒绝注入。同名的 System 先到先得。
        /// </summary>
        public void Register(IGlobalSystem system)
        {
            if (IsInitialized)
            {
                return;
            }

            _declared.TryAdd(system.SystemName, system);
        }

        /// <summary>
        /// 执行完整的初始化流程：
        /// <para>1. 广播 GlobalSystemRegistering 事件（收集声明表）</para>
        /// <para>2. 调用各 System 的 GetPrerequisites 构建注册表</para>
        /// <para>3. 拓扑排序</para>
        /// <para>4. 按序逐个调用 IGlobalSystem.Initialize，同时填充系统表</para>
        /// <para>5. 标记 IsInitialized，清空声明表</para>
        /// <para>6. 广播 GlobalSystemsInitialized</para>
        /// </summary>
        public void Initialize()
        {
            GlobalSystemRegistering?.Invoke(this);

            Dictionary<IGlobalSystem, string[]> registrationTable = BuildRegistrationTable();

            List<IGlobalSystem> sorted = TopologicalSort(registrationTable);

            foreach (IGlobalSystem entry in sorted)
            {
                entry.Initialize(_systemTable);
                _systemTable[entry.SystemName] = entry;
            }

            IsInitialized = true;
            _declared.Clear();

            GlobalSystemsInitialized?.Invoke();
        }

        /// <summary>
        /// 遍历声明表，调用各 System 的 GetPrerequisites 构建注册表。
        /// 声明表类型与 GetPrerequisites 入参一致，无需包装转换。
        /// </summary>
        private Dictionary<IGlobalSystem, string[]> BuildRegistrationTable()
        {
            Dictionary<IGlobalSystem, string[]> registrationTable = new();
            foreach (IGlobalSystem system in _declared.Values)
            {
                string[] prerequisites = system.GetPrerequisites(_declared);
                registrationTable[system] = prerequisites;
            }

            return registrationTable;
        }

        /// <summary>
        /// 对注册表进行拓扑排序，检测环依赖或缺失前置则报错。
        /// </summary>
        private List<IGlobalSystem> TopologicalSort(Dictionary<IGlobalSystem, string[]> registrationTable)
        {
            // TODO: 实现拓扑排序
            List<IGlobalSystem> sorted = new();
            foreach (KeyValuePair<IGlobalSystem, string[]> pair in registrationTable)
            {
                sorted.Add(pair.Key);
            }

            return sorted;
        }
    }
}
