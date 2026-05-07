namespace WorldWeaver.Config
{
    /// <summary>
    /// 存档级 System 接口。
    /// <para>存档级 System 由 Save.Systems（SaveSystemsGroup）持有，在存档加载时通过事件广播收集注册，按依赖拓扑排序后初始化。</para>
    /// <para>模组与香草均可通过监听存档加载事件来注册自身的 ISaveSystem。</para>
    /// </summary>
    public interface ISaveSystem : IGameSystem
    {
        /// <summary>
        /// 存档加载时调用，前置 System 均已初始化完毕。
        /// </summary>
        /// <param name="save">当前存档上下文。</param>
        void OnSaveLoad(ISaveContext save);

        /// <summary>
        /// 存档卸载时调用，应清理所有存档相关的运行时状态。
        /// </summary>
        void OnSaveUnload();
    }
}
