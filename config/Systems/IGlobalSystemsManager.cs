using System;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 全局 System 容器接口。所有查询均返回 IGlobalSystem。
    /// </summary>
    public interface IGlobalSystemsManager
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
        /// 一切全局 System 查询的唯一枢纽。visitor 为请求方自身。
        /// </summary>
        IGlobalSystem GetGlobalSystem(IGameSystem visitor, string target);

        /// <summary>
        /// System 间查询索引器，直接委托 GetGlobalSystem。
        /// </summary>
        IGlobalSystem this[IGameSystem visitor, string target] { get; }

        /// <summary>
        /// 容器以自身为 visitor 查询——非 System 上下文访问全局 System 的唯一入口。
        /// </summary>
        IGlobalSystem ResolveSystem(string systemName);

        /// <summary>
        /// 全局 System 注册事件。订阅方直接通过容器注册 System。
        /// </summary>
        event Action<IGlobalSystemsManager> GlobalSystemRegistering;

        /// <summary>
        /// 全部全局 System 初始化完毕后触发。
        /// </summary>
        event Action GlobalSystemsInitialized;
    }
}
