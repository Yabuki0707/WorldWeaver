using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 全局 System 容器。继承 SystemContainerBase 获得拓扑排序流水线，
    /// 实现 IGlobalSystemManager 暴露全局 System 特有事件。
    /// </summary>
    public class GlobalSystemManager : SystemContainerBase<IGlobalSystem>, IGlobalSystemManager
    {
        // ================================================================================
        //                           IGameSystem — 继承自 IGlobalSystemManager
        // ================================================================================

        /// <summary>
        /// 容器自身的 SystemName，用作 VisitSystem 及 ResolveFor 的 visitor 身份标识。
        /// </summary>
        public string SystemName => "GlobalSystemManager";

        /// <summary>
        /// 容器无前置依赖，恒为 true。
        /// </summary>
        public bool IsPrerequisitesGenerated => true;

        /// <summary>
        /// 容器无前置依赖，恒为空。
        /// </summary>
        public ReadOnlyMemory<string> Prerequisites => ReadOnlyMemory<string>.Empty;

        /// <summary>
        /// 容器无前置依赖，恒为空。
        /// </summary>
        public FrozenSet<string> PrerequisiteSet => FrozenSet<string>.Empty;

        /// <summary>
        /// 容器卸载——与游戏进程同生命周期，占位。
        /// </summary>
        public void Uninstall()
        {
        }

        // ================================================================================
        //                                   事件
        // ================================================================================

        /// <summary>
        /// 全局 System 注册事件。在 Initialize 开头触发。
        /// </summary>
        public event Action<IGlobalSystemManager> GlobalSystemRegistering;

        /// <summary>
        /// 全部全局 System 初始化完毕后触发。
        /// </summary>
        public event Action GlobalSystemsInitialized;

        // ================================================================================
        //                              初始化入口
        // ================================================================================

        /// <summary>
        /// 执行完整的初始化流程：广播 GlobalSystemRegistering → 声明表→注册表→拓扑排序→逐个 Initialize → 广播 GlobalSystemsInitialized。
        /// </summary>
        public void Initialize()
        {
            InitializeCore(
                () => GlobalSystemRegistering?.Invoke(this),
                () => GlobalSystemsInitialized?.Invoke());
        }

        // ================================================================================
        //                              子类钩子实现
        // ================================================================================


        /// <summary>
        /// 委托至 IGlobalSystem.GenerateGlobalSystemPrerequisites，传入当前声明表。
        /// </summary>
        protected override bool GenerateSystemPrerequisites(IGlobalSystem system)
        {
            return system.GenerateGlobalSystemPrerequisites(Declared);
        }

        /// <summary>
        /// 委托至 IGlobalSystem.Initialize，传入当前注册表，返回是否初始化成功。
        /// </summary>
        protected override bool InitializeSystem(IGlobalSystem system, ISystemRegistrationSequence<IGlobalSystem> registry)
        {
            return system.Initialize(registry);
        }
    }
}
