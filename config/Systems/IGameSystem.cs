namespace WorldWeaver.Systems
{
    /// <summary>
    /// System 基接口。定义所有 System 共有的身份标识、查询与卸载行为。
    /// </summary>
    public interface IGameSystem
    {
        /// <summary>
        /// System 名称，全局唯一，用于前置依赖引用与调试。
        /// </summary>
        string SystemName { get; }

        /// <summary>
        /// 按名称查询同级 System。委托至所属容器的双 Key 索引器。
        /// </summary>
        IGameSystem GetGlobalSystem(string systemName);

        /// <summary>
        /// 卸载本 System，清理所有运行时状态。
        /// </summary>
        void Uninstall();
    }
}
