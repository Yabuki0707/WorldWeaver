using System;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 存档级 System 容器接口。所有查询均返回 ISaveSystem。
    /// </summary>
    public interface ISaveSystemsManager
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
        /// 一切存档级 System 查询的唯一枢纽。
        /// </summary>
        ISaveSystem GetSaveSystem(IGameSystem visitor, string target);

        /// <summary>
        /// System 间查询索引器。
        /// </summary>
        ISaveSystem this[IGameSystem visitor, string target] { get; }

        /// <summary>
        /// 容器以自身为 visitor 查询——非 System 上下文访问存档 System 的唯一入口。
        /// </summary>
        ISaveSystem ResolveSystem(string systemName);

        /// <summary>
        /// 存档级 System 注册事件。
        /// </summary>
        event Action<ISaveSystemsManager> SaveSystemRegistering;

        /// <summary>
        /// 全部存档级 System 初始化完毕后触发。
        /// </summary>
        event Action SaveSystemsInitialized;
    }
}
