using System;
using System.Collections.Generic;
using WorldWeaver.Systems;

namespace WorldWeaver.Map
{
	/// <summary>
	/// 存档接口。管理存档级 System 容器与图层。
	/// </summary>
	public interface ISave
	{
		/// <summary>
		/// 存档唯一标识。
		/// </summary>
		string SaveId { get; }

		/// <summary>
		/// 存档存储根路径。
		/// </summary>
		string StorageRootPath { get; }

		/// <summary>
		/// 存档级 System 容器。
		/// </summary>
		ISaveSystemGroup Systems { get; }

		/// <summary>
		/// 当前存档包含的图层列表。
		/// </summary>
		IReadOnlyList<IMapLayer> Layers { get; }

		/// <summary>
		/// 存档加载/创建完成后触发。此时 Systems 已初始化完毕，可安全查询。
		/// </summary>
		event Action SaveReady;

		/// <summary>
		/// 存档卸载前触发，用于清理。
		/// </summary>
		event Action SaveUnloading;
	}
}
