namespace WorldWeaver.Systems
{
    /// <summary>
    /// 系统注册器只读接口。供 Initialize 期间查询已就位的前置 System。
    /// </summary>
    /// <typeparam name="TSystem">容器所管理的 System 类型。</typeparam>
    public interface ISystemRegistrar<TSystem> where TSystem : IGameSystem
    {
        /// <summary>
        /// 当前已注册的 System 数量。
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 检查指定名称的 System 是否已注册。
        /// </summary>
        bool IsRegistered(string systemName);

        /// <summary>
        /// 尝试按名称获取已注册的 System。
        /// </summary>
        bool TryGetRegistration(string systemName, out TSystem system);

        /// <summary>
        /// 按名称获取已注册的 System，不存在则抛出 KeyNotFoundException。
        /// </summary>
        TSystem this[string systemName] { get; }
    }
}
