namespace WorldWeaver.Config
{
    /// <summary>
    /// 全局 System 注册器，在游戏启动事件中传递给订阅方，用于注册全局 System。
    /// <para>重复注册同一 SystemName 的 System 将被忽略（先到先得）。</para>
    /// </summary>
    public interface IGlobalSystemRegistrar
    {
        /// <summary>
        /// 注册一个全局 System。
        /// </summary>
        void Register(IGlobalSystem system);
    }
}
