using System;
using WorldWeaver.ModSystem;
using WorldWeaver.Systems;

namespace WorldWeaver
{
    /// <summary>
    /// GameManager 对外接口，Mod 获取一切架构能力的唯一入口。
    /// </summary>
    public interface IGameManager
    {
        /// <summary>
        /// 全局 System 容器，用于查询与订阅全局 System 生命周期。
        /// </summary>
        IGlobalSystemsManager Systems { get; }

        /// <summary>
        /// Mod 管理器，用于查询已加载模组与模组信息。
        /// </summary>
        IModManager ModManager { get; }

        /// <summary>
        /// 游戏关闭前触发，用于清理通知。
        /// </summary>
        event Action GameShuttingDown;
    }
}
