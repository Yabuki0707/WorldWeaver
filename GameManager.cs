using Godot;
using WorldWeaver.ModSystem;
using WorldWeaver.Systems;

namespace WorldWeaver
{
	/// <summary>
	/// 游戏根节点。持有全局 System 容器与 Mod 管理器，所有架构能力的起点。
	/// </summary>
	[GlobalClass]
	public partial class GameManager : Node, IGameManager
	{
		/// <summary>
		/// 全局单例，供 System 与 Mod 访问。
		/// </summary>
		public static GameManager Instance { get; private set; }

		/// <summary>
		/// 全局 System 容器。
		/// </summary>
		public IGlobalSystemsManager Systems { get; private set; }

		/// <summary>
		/// Mod 管理器。
		/// </summary>
		public IModManager ModManager { get; private set; }

		/// <summary>
		/// 游戏关闭前触发。
		/// </summary>
		public event System.Action GameShuttingDown;

		/// <summary>
		/// 构造时即收编入静态单例。若已有实例存在则报错。
		/// </summary>
		public GameManager()
		{
			if (Instance != null)
			{
				throw new System.InvalidOperationException(
                    "[GameManager]:已存在一个 GameManager 实例，不允许重复创建。"
				);
			}

			Instance = this;
		}

		/// <summary>
		/// 引擎入口。初始化容器 → 注册香草 System → 启动。
		/// </summary>
		public override void _Ready()
		{
			GlobalSystemsManager systemsManager = new();
			Systems = systemsManager;
			ModManager = new ModManager();

			// 香草 System 注册
			systemsManager.GlobalSystemRegistering += RegisterVanillaSystems;

			// 模组加载与注册（待 ModManager 完善后在此处接入）

			systemsManager.Initialize();
		}

		/// <summary>
		/// 引擎退出前广播清理事件。
		/// </summary>
		public override void _ExitTree()
		{
			GameShuttingDown?.Invoke();
		}

		/// <summary>
		/// 香草 System 在此硬编码注册。
		/// </summary>
		private void RegisterVanillaSystems(IGlobalSystemsManager manager)
		{
			// manager.Register(new SaveManager());
			// manager.Register(new ...);
		}
	}
}
