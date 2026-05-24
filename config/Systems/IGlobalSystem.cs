using System.Collections.Generic;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 全局 System 接口。携带与自身类型匹配的声明注册表与初始化注册表。
    /// </summary>
    public interface IGlobalSystem : IGameSystem
    {
        /// <summary>
        /// 一次性生成前置依赖并填入 Prerequisites / PrerequisiteSet、置位 IsPrerequisitesGenerated。
        /// 若已生成则返回 false 且不做任何操作。
        /// </summary>
        bool GenerateGlobalSystemPrerequisites(ISystemDeclarationTable<IGlobalSystem> declaredSystems);

        /// <summary>
        /// 初始化本 System。传入的注册器完整包含所有已就位的前置依赖。
        /// </summary>
        bool Initialize(ISystemRegistrar<IGlobalSystem> registry);
    }
}
