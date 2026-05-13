namespace WorldWeaver.ModCore
{
    /// <summary>
    /// Mod 管理器接口。负责扫描 mods/ 目录、加载模组 DLL/PCK 与依赖排序。
    /// </summary>
    public interface IModManager
    {
        /// <summary>
        /// 已加载的模组数量。
        /// </summary>
        int LoadedModCount { get; }
    }
}
