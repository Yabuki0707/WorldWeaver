namespace WorldWeaver.Config
{
    /// <summary>
    /// Mod 入口接口。
    /// <para>社区模组的入口类实现此接口。ModManager 在加载阶段实例化入口类并调用 <see cref="OnLoad"/>。</para>
    /// <para>香草不实现此接口，其逻辑已硬编码于主 DLL，ModManager 遇到香草时直接走内置初始化流程。</para>
    /// <para>Mod 通过 <paramref name="gameManager"/> 的事件注册自己的 System。</para>
    /// </summary>
    public interface IMod
    {
        /// <summary>
        /// Mod 唯一名称，与 mod.json 中的 name 字段一致。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Mod 被加载时调用。
        /// <para>此时应订阅 <see cref="IGameManager.GlobalSystemRegistering"/> 等事件以注册 System。</para>
        /// </summary>
        /// <param name="gameManager">游戏管理器实例，Mod 获取一切架构能力的唯一入口。</param>
        void OnLoad(IGameManager gameManager);
    }
}
