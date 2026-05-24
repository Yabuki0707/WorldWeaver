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
        /// 声明表，通过 + 运算符添加，同名先到先得。
        /// <para>子类钩子 GenerateSystemPrerequisites 直接遍历此表获取已声明 System。</para>
        /// </summary>
        public SystemDeclarationTable<TSystem> Declared { get; } = new();

        /// <summary>
        /// 显式接口实现，以 <see cref="ISystemDeclarationTable{TSystem}"/> 类型暴露给外部。
        /// </summary>
        ISystemDeclarationTable<TSystem> ISystemContainer<TSystem>.Declared => Declared;

        /// <summary>
        /// 系统表，初始化完成后冻结为 FrozenDictionary 供高速只读查询。
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
        public bool IsRegistered(string systemName)
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
                    $"[{GetType().Name}]:ResolveFor 的 visitor 为 null，查询 {target} 被拒绝。");
                return default;
            }

            _systemTable.TryGetValue(target, out TSystem system);
            return system;
        }

        /// <summary>
        /// System 间查询索引器，直接委托 <see cref="ResolveFor"/>。
        /// </summary>
        public TSystem this[IGameSystem visitor, string target] => ResolveFor(visitor, target);

        // ================================================================================
        //                              初始化
        // ================================================================================

        /// <summary>
        /// 执行完整的初始化流水线：
        /// <para>1. 广播注册事件（子类注入）</para>
        /// <para>2. 声明表筛选——BuildFilteredTable 校验并生成前置依赖</para>
        /// <para>3. 构建注册器，Kahn 拓扑顺序逐个产出就绪 System</para>
        /// <para>4. 逐个 Initialize，成功后 + 加入成员表</para>
        /// <para>5. 冻结系统表、标记 IsInitialized</para>
        /// <para>6. 广播初始化完毕事件（子类注入）</para>
        /// </summary>
        protected void InitializeCore(Action fireRegistrationEvent, Action fireInitializedEvent)
        {
            // —————— 1. 广播注册事件，收集声明表 ——————
            fireRegistrationEvent();

            // —————— 2. 声明表筛选 ——————
            SystemDeclarationTable<TSystem> filteredDeclared = Declared.BuildFilteredTable(GenerateSystemPrerequisites);

            // —————— 3~4. 构建注册器，Kahn 拓扑顺序，逐个初始化 ——————
            SystemRegistrar<TSystem> reg = new(filteredDeclared);
            foreach (TSystem entry in reg)
            {
                if (InitializeSystem(entry, reg))
                {
                    reg += entry;
                }
            }

            // —————— 异常报告 ——————
            (IReadOnlyList<string> initFailed, IReadOnlyList<string> prereqFailed) = reg.GetFailedSystemNames();
            if (initFailed.Count > 0)
            {
                GD.PushError(
                    $"[{GetType().Name}] 初始化异常——以下 System 的 Initialize 返回 false: {string.Join(", ", initFailed)}");
            }

            if (prereqFailed.Count > 0)
            {
                GD.PushError(
                    $"[{GetType().Name}] 前置条件异常——以下 System 的前置依赖从未被满足: {string.Join(", ", prereqFailed)}");
            }

            if (initFailed.Count + prereqFailed.Count > 0)
            {
                GD.PushError(
                    $"[{GetType().Name}] 以下 {initFailed.Count + prereqFailed.Count} 个 System 未被初始化: {string.Join(", ", initFailed)}, {string.Join(", ", prereqFailed)}");
            }

            // —————— 5. 冻结、标记完成 ——————
            _systemTable = reg.ToSystemTable();
            IsInitialized = true;

            // —————— 6. 广播就绪 ——————
            fireInitializedEvent();
        }

        // ================================================================================
        //                              子类钩子
        // ================================================================================

        /// <summary>
        /// 调用子类特有的 GenerateXxxPrerequisites。
        /// 钩子实现应调用 system.GenerateXxxPrerequisites(<see cref="Declared"/>)。
        /// </summary>
        protected abstract bool GenerateSystemPrerequisites(TSystem system);

        /// <summary>
        /// 调用子类特有的 Initialize 方法，返回是否初始化成功。
        /// </summary>
        protected abstract bool InitializeSystem(TSystem system, ISystemRegistrar<TSystem> registrar);
    }
}
