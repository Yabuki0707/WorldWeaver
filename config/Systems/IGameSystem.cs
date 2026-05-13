namespace WorldWeaver.Systems
{
    /// <summary>
    /// System 基接口。定义所有 System 共有的身份标识、查询入口与卸载行为。
    /// </summary>
    public interface IGameSystem
    {
        /// <summary>
        /// System 名称，全局唯一，用于前置依赖引用与调试。
        /// </summary>
        string SystemName { get; }

        /// <summary>
        /// 按名称查询全局 System。默认实现委托至 IGameManager.Instance.Systems.ResolveFor。
        /// </summary>
        IGameSystem VisitSystem(string systemName)
        {
            return IGameManager.Instance.Systems.ResolveFor(this, systemName);
        }

        /// <summary>
        /// 卸载本 System，清理所有运行时状态。
        /// </summary>
        void Uninstall();
    }
}
