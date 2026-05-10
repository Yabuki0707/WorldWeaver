using System.Collections.Generic;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 存档级 System 基类。继承 GameSystem，声明自身的前置依赖与初始化逻辑。
    /// </summary>
    public abstract class SaveSystem : GameSystem, ISaveSystem
    {
        /// <summary>
        /// 声明前置依赖。子类必须实现。
        /// </summary>
        public abstract string[] GetSaveSystemPrerequisites(Dictionary<string, ISaveSystem> declaredSystems);

        /// <summary>
        /// 初始化。子类必须实现。
        /// </summary>
        public abstract void Initialize(Dictionary<string, ISaveSystem> registry);
    }
}
