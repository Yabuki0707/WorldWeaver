namespace WorldWeaver.Systems
{
    /// <summary>
    /// System 基类。通过 GameManager.Instance 访问容器以查询同级 System。
    /// </summary>
    public abstract class GameSystem : IGameSystem
    {
        /// <summary>
        /// System 名称，子类必须实现。
        /// </summary>
        public abstract string SystemName { get; }

        /// <summary>
        /// 按名称查询同级 System。通过 GameManager 单例访问全局容器。
        /// </summary>
        public IGameSystem GetGlobalSystem(string systemName)
        {
            return GameManager.Instance.Systems.GetGlobalSystem(this, systemName);
        }

        /// <summary>
        /// 卸载，默认空实现。子类可重写以清理资源。
        /// </summary>
        public virtual void Uninstall()
        {
        }
    }
}
