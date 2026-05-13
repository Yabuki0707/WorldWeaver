using System;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 全局 System 容器接口。继承 IGameSystem 使容器自身成为可查询的 System 身份。
    /// </summary>
    public interface IGlobalSystemManager : IGameSystem
    {
        /// <summary>
        /// 是否已完成初始化。初始化后拒绝新的 System 注册。
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 系统表中已注册的 System 数量。
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 检查指定名称的 System 是否在系统表中。
        /// </summary>
        bool ContainsKey(string systemName);

        /// <summary>
        /// 替 visitor 解析 target——一切 System 查询的唯一枢纽。
        /// </summary>
        IGlobalSystem ResolveFor(IGameSystem visitor, string target);

        /// <summary>
        /// System 间查询索引器，直接委托 ResolveFor。
        /// </summary>
        IGlobalSystem this[IGameSystem visitor, string target] { get; }

        /// <summary>
        /// 注入一个全局 System 到声明表。同名的 System 先到先得。
        /// </summary>
        void Register(IGlobalSystem system);

        /// <summary>
        /// 全局 System 注册事件。订阅方直接通过容器注册 System。
        /// </summary>
        event Action<IGlobalSystemManager> GlobalSystemRegistering;

        /// <summary>
        /// 全部全局 System 初始化完毕后触发。
        /// </summary>
        event Action GlobalSystemsInitialized;
    }
}
