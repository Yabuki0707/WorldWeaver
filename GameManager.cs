using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using WorldWeaver.ModCore;
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
		public IGlobalSystemManager Systems { get; private set; }

		/// <summary>
		/// Mod 管理器。
		/// </summary>
		public IModManager ModManager { get; private set; }

		/// <summary>
		/// 游戏关闭前触发。
		/// </summary>
		public event Action GameShuttingDown;

		/// <summary>
		/// 构造时即收编入静态单例。若已有实例存在则报错。
		/// </summary>
		public GameManager()
		{
			if (Instance != null)
			{
				throw new InvalidOperationException(
                    "[GameManager]:已存在一个 GameManager 实例，不允许重复创建。"
				);
			}

			Instance = this;
			IGameManager.Instance = this;
		}

		/// <summary>
		/// 引擎入口。初始化容器 → 反射收集 [VanillaGlobalSystem] → 注册 → 启动。
		/// </summary>
		public override void _Ready()
		{
			GlobalSystemManager systemsManager = new();
			Systems = systemsManager;
			ModManager = new ModManager();

			IReadOnlyList<IGlobalSystem> vanillaSystems = GetVanillaGlobalSystemInstances();
			foreach (IGlobalSystem system in vanillaSystems)
			{
				systemsManager.GlobalSystemRegistering += manager => manager.Register(system);
			}

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
		/// 反射扫描当前程序集中标记了 <see cref="VanillaGlobalSystemAttribute"/> 的 IGlobalSystem 实现，
		/// 实例化并返回。
		/// </summary>
		private static IReadOnlyList<IGlobalSystem> GetVanillaGlobalSystemInstances()
		{
			List<IGlobalSystem> systems = new();

			foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
			{
				if (type.IsAbstract)
				{
					continue;
				}

				if (!typeof(IGlobalSystem).IsAssignableFrom(type))
				{
					continue;
				}

				if (type.GetCustomAttribute<VanillaGlobalSystemAttribute>() == null)
				{
					continue;
				}

				if (Activator.CreateInstance(type) is IGlobalSystem instance)
				{
					systems.Add(instance);
				}
			}

			return systems;
		}
	}
}
