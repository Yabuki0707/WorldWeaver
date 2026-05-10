using System.Collections.Generic;
using WorldWeaver.ModSystem;

namespace WorldWeaver
{
    /// <summary>
    /// Mod 管理器。负责扫描 mods/ 目录、加载模组 DLL/PCK 与依赖排序。
    /// </summary>
    public class ModManager : IModManager
    {
        /// <summary>
        /// 已加载的模组列表。
        /// </summary>
        private readonly List<IMod> _loadedMods = new();

        /// <summary>
        /// 已加载的模组数量。
        /// </summary>
        public int LoadedModCount => _loadedMods.Count;
    }
}
