using System.Collections.Generic;
using WorldWeaver.ModCore;

namespace WorldWeaver.ModSystem
{
    /// <summary>
    /// Mod 管理器。负责扫描 mods/ 目录、加载模组 DLL/PCK、依赖排序，以及反射发现 <c>[GlobalSystem]</c>。
    /// </summary>
    public class ModManager : IModManager
    {
        /// <summary>
        /// 已加载的模组列表（按依赖序）。
        /// </summary>
        private readonly List<IMod> _loadedMods = [];

        /// <summary>
        /// 已加载的模组数量。
        /// </summary>
        public int LoadedModCount => _loadedMods.Count;
    }
}
