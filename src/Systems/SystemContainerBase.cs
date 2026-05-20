using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// System 容器基类。提供声明表→注册表→拓扑排序→逐个初始化的完整流水线，
    /// 子类通过钩子注入特有事件与各 System 的前置声明/初始化调用。
    /// 初始化完成后系统表冻结为 <see cref="FrozenDictionary{TKey, TValue}"/>。
    /// </summary>
    /// <typeparam name="TSystem">容器所管理的 System 类型。</typeparam>
    public abstract class SystemContainerBase<TSystem> : ISystemContainer<TSystem> where TSystem : IGameSystem
    {
        /// <summary>
        /// 声明表，Key 为 SystemName。TryAdd 保证先到先得。
        /// <para>子类钩子 GenerateSystemPrerequisites 直接读取此表获取已声明 System。</para>
        /// </summary>
        protected readonly Dictionary<string, TSystem> Declared = new();

        /// <summary>
        /// 系统表，初始化完成后冻结为 FrozenDictionary 供高速只读查询。
        /// <para>初始化前为 null，初始化完成后不可变。</para>
        /// </summary>
        private FrozenDictionary<string, TSystem> _systemTable;

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
        /// 替 visitor 解析 target——一切 System 查询的唯一枢纽。visitor 仅 IGameSystem 可调用。
        /// </summary>
        public TSystem ResolveFor(IGameSystem visitor, string target)
        {
            if (visitor == null)
            {
                GD.PushError(
                    $"[SystemContainerBase]:ResolveFor 的 visitor 为 null，查询 {target} 被拒绝。");
                return default;
            }

            _systemTable.TryGetValue(target, out TSystem system);
            return system;
        }

        /// <summary>
        /// System 间查询索引器，直接委托 ResolveFor。
        /// </summary>
        public TSystem this[IGameSystem visitor, string target] => ResolveFor(visitor, target);

        // ================================================================================
        //                              注册与初始化
        // ================================================================================

        /// <summary>
        /// 注入一个 System 到声明表。
        /// 若已初始化则拒绝注入。同名的 System 先到先得。
        /// </summary>
        public void Register(TSystem system)
        {
            if (IsInitialized)
            {
                return;
            }

            Declared.TryAdd(system.SystemName, system);
        }

        /// <summary>
        /// 执行完整的初始化流水线：
        /// <para>1. 广播注册事件（子类注入）</para>
        /// <para>2. 调用各 System 的 GenerateXxxPrerequisites 筛选声明表</para>
        /// <para>3. 构建 RegistrationTable，饱和式扫描迭代器逐个产出前置就位的 System</para>
        /// <para>4. 逐个初始化，成功后 ++ 加入成员表</para>
        /// <para>5. 冻结系统表、标记 IsInitialized、清空声明表</para>
        /// <para>6. 广播初始化完毕事件（子类注入）</para>
        /// </summary>
        /// <param name="fireRegistrationEvent">广播注册事件的委托。</param>
        /// <param name="fireInitializedEvent">广播初始化完毕事件的委托。</param>
        protected void InitializeCore(Action fireRegistrationEvent, Action fireInitializedEvent)
        {
            // —————— 1. 广播注册事件，收集声明表 ——————
            fireRegistrationEvent();

            // —————— 2. 声明表筛选：生成前置失败或已生成过的排除 ——————
            Dictionary<string, TSystem> filteredDeclared = BuildRegistrationTable();

            // —————— 3~4. 构建注册表，饱和式扫描，逐个初始化 ——————
            SystemRegistrationSequence<TSystem> reg = new(filteredDeclared);
            foreach (TSystem entry in reg)
            {
                if (InitializeSystem(entry, reg))
                {
                    reg += entry;
                }
            }

            // —————— 异常报告 ——————
            IReadOnlyList<string> uninitialized = reg.GetUnregisteredSystemNames();
            if (uninitialized.Count > 0)
            {
                GD.PushError(
                    $"[SystemContainerBase] 初始化异常——以下 System 未能通过注册: {string.Join(", ", uninitialized)}");
            }

            // —————— 5. 冻结、标记完成、清空声明表 ——————
            _systemTable = reg.ToSystemTable();
            IsInitialized = true;
            Declared.Clear();

            // —————— 6. 广播就绪 ——————
            fireInitializedEvent();
        }

        // ================================================================================
        //                              子类钩子
        // ================================================================================

        /// <summary>
        /// 调用子类特有的 GenerateXxxPrerequisites，传入系统实例。
        /// 钩子实现应调用 system.GenerateXxxPrerequisites(<see cref="Declared"/>)。
        /// </summary>
        /// <param name="system">当前 System 实例。</param>
        /// <returns>生成是否成功。若已生成或生成失败则返回 false。</returns>
        protected abstract bool GenerateSystemPrerequisites(TSystem system);

        /// <summary>
        /// 调用子类特有的 Initialize 方法，返回是否初始化成功。
        /// </summary>
        /// <param name="system">当前 System 实例。</param>
        /// <param name="registry">当前注册表，完整包含所有已就位的前置依赖。</param>
        /// <returns>初始化是否成功。失败时调用方不应将 system 加入成员表。</returns>
        protected abstract bool InitializeSystem(TSystem system, ISystemRegistrationSequence<TSystem> registry);

        // ================================================================================
        //                              声明表筛选（私有）
        // ================================================================================

        /// <summary>
        /// 遍历声明表，对每个 System 调用 GenerateXxxPrerequisites。
        /// 已生成过前置或生成失败的 System 将被排除，不进入后续注册流程。
        /// </summary>
        /// <returns>筛选后的声明表，Key 为 SystemName，均成功生成前置依赖且首次生成。</returns>
        private Dictionary<string, TSystem> BuildRegistrationTable()
        {
            Dictionary<string, TSystem> filtered = new(Declared.Count, StringComparer.Ordinal);
            foreach (TSystem system in Declared.Values)
            {
                string name = system.SystemName;

                // 已生成过前置依赖则报错并排除。
                if (system.IsPrerequisitesGenerated)
                {
                    GD.PushError(
                        $"[SystemContainerBase] 声明表筛选: System '{name}' 的前置依赖已生成过，排除。");
                    continue;
                }

                // 生成前置依赖失败则报错并排除。
                if (!GenerateSystemPrerequisites(system))
                {
                    GD.PushError(
                        $"[SystemContainerBase] 声明表筛选: System '{name}' 的前置依赖生成失败，排除。");
                    continue;
                }

                filtered[name] = system;
            }

            return filtered;
        }
    }
}
