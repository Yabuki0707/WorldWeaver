namespace WorldWeaver.Config
{
    /// <summary>
    /// 存档级 System 注册器，在存档加载事件中传递给订阅方，用于注册存档级 System。
    /// <para>重复注册同一 SystemName 的 System 将被忽略（先到先得）。</para>
    /// </summary>
    public interface ISaveSystemRegistrar
    {
        /// <summary>
        /// 注册一个存档级 System。
        /// </summary>
        void Register(ISaveSystem system);
    }
}
