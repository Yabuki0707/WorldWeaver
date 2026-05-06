namespace WorldWeaver.Config
{
    /// <summary>
    /// 全局 System 接口。
    /// <para>全局 System 由 GameManager 持有，进入游戏即初始化，生命周期与游戏进程绑定，不随存档切换。</para>
    /// <para>注册方式与存档级 System 一致：通过事件广播 + 拓扑排序。</para>
    /// </summary>
    public interface IGlobalSystem : IGameSystem
    {
        /// <summary>
        /// 游戏启动时调用，前置 System 均已初始化完毕。
        /// </summary>
        void OnGameStart();

        /// <summary>
        /// 游戏关闭时调用。
        /// </summary>
        void OnGameShutdown();
    }
}
