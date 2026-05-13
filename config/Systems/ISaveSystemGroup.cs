using System;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 存档级 System 容器接口。所有查询均返回 ISaveSystem。
    /// </summary>
    public interface ISaveSystemGroup
    {
        /// <summary>
        /// 是否已完成初始化。
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
        /// 替 visitor 解析 target。
        /// </summary>
        ISaveSystem ResolveFor(IGameSystem visitor, string target);

        /// <summary>
        /// System 间查询索引器。
        /// </summary>
        ISaveSystem this[IGameSystem visitor, string target] { get; }

        /// <summary>
        /// 存档级 System 注册事件。
        /// </summary>
        event Action<ISaveSystemGroup> SaveSystemRegistering;

        /// <summary>
        /// 全部存档级 System 初始化完毕后触发。
        /// </summary>
        event Action SaveSystemsInitialized;
    }
}
