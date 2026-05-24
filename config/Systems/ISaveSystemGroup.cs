using System;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 存档级 System 容器接口。继承 ISystemContainer 获取通用查询与声明表能力。
    /// </summary>
    public interface ISaveSystemGroup : ISystemContainer<ISaveSystem>
    {
        /// <summary>
        /// 存档级 System 注册事件。订阅方通过 <see cref="ISystemContainer{ISaveSystem}.Declared"/> 添加 System。
        /// </summary>
        event Action<ISaveSystemGroup> SaveSystemRegistering;

        /// <summary>
        /// 全部存档级 System 初始化完毕后触发。
        /// </summary>
        event Action SaveSystemsInitialized;
    }
}
