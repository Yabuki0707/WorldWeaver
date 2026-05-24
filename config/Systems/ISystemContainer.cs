namespace WorldWeaver.Systems
{
    /// <summary>
    /// System 容器基接口。全局与存档级容器共享的查询、注册与拓扑排序契约。
    /// </summary>
    /// <typeparam name="TSystem">容器所管理的 System 类型，必须实现 IGameSystem。</typeparam>
    public interface ISystemContainer<TSystem> where TSystem : IGameSystem
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
        bool IsRegistered(string systemName);

        /// <summary>
        /// 声明表。外部通过 + 运算符向其中添加 System。
        /// </summary>
        ISystemDeclarationTable<TSystem> Declared { get; }

        /// <summary>
        /// 替 visitor 解析 target——一切 System 查询的唯一枢纽。
        /// </summary>
        TSystem ResolveFor(IGameSystem visitor, string target);

        /// <summary>
        /// System 间查询索引器，直接委托 ResolveFor。
        /// </summary>
        TSystem this[IGameSystem visitor, string target] { get; }
    }
}
