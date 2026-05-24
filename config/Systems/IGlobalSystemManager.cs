using System;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 全局 System 容器接口。继承 ISystemContainer 获取查询与声明表能力，
    /// 继承 IGameSystem 使容器自身成为可查询的 System 身份。
    /// </summary>
    public interface IGlobalSystemManager : ISystemContainer<IGlobalSystem>, IGameSystem
    {
        /// <summary>
        /// 全局 System 注册事件。订阅方通过 <see cref="ISystemContainer{IGlobalSystem}.Declared"/> 添加 System。
        /// </summary>
        event Action<IGlobalSystemManager> GlobalSystemRegistering;

        /// <summary>
        /// 全部全局 System 初始化完毕后触发。
        /// </summary>
        event Action GlobalSystemsInitialized;
    }
}
