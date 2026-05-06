namespace WorldWeaver.Config
{
    /// <summary>
    /// System 基接口。
    /// <para>定义 System 的公共属性：名称与前置依赖。全局 System 与存档级 System 均继承自此接口。</para>
    /// </summary>
    public interface IGameSystem
    {
        /// <summary>
        /// System 名称，全局唯一，用于前置依赖引用与调试。
        /// </summary>
        string SystemName { get; }

        /// <summary>
        /// 前置依赖的 System 名称数组，这些 System 必须在本 System 之前完成初始化。
        /// </summary>
        string[] Prerequisites { get; }
    }
}
