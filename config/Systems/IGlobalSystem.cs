using System.Collections.Generic;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 全局 System 接口。携带与自身类型匹配的声明注册表与初始化注册表。
    /// </summary>
    public interface IGlobalSystem : IGameSystem
    {
        /// <summary>
        /// 一次性声明前置依赖。传入当前已声明的全局 System 表，返回依赖名称。
        /// </summary>
        string[] GetGlobalSystemPrerequisites(Dictionary<string, IGlobalSystem> declaredSystems);

        /// <summary>
        /// 初始化本 System。传入的注册表完整包含所有前置依赖。
        /// </summary>
        void Initialize(Dictionary<string, IGlobalSystem> registry);
    }
}
