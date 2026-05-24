using System.Collections.Generic;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 存档级 System 接口。携带与自身类型匹配的声明注册表与初始化注册表。
    /// </summary>
    public interface ISaveSystem : IGameSystem
    {
        /// <summary>
        /// 一次性生成前置依赖并填入 Prerequisites / PrerequisiteSet、置位 IsPrerequisitesGenerated。
        /// 若已生成或生成失败（如不满足特殊要求）则返回 false，不做任何操作。
        /// </summary>
        bool GenerateSaveSystemPrerequisites(ISystemDeclarationTable<ISaveSystem> declaredSystems);

        /// <summary>
        /// 初始化本 System。传入的注册器完整包含所有已就位的前置依赖。
        /// </summary>
        bool Initialize(ISystemRegistrar<ISaveSystem> registry);
    }
}
