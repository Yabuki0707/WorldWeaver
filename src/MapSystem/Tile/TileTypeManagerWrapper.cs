using Godot;
using rasu.Map.Tile;

// 禁用命名规则警告：该类对外提供 snake_case API 以匹配 Godot 约定
#pragma warning disable IDE1006


	/// <summary>
	/// TileTypeManager 的访问包装类，提供对 TileTypeManager 静态方法的全局入口
	/// </summary>
	[GlobalClass]
	public partial class TileTypeManagerWrapper : Node
	{


		/*******************************
				初始化
		********************************/

		/// <summary>
		/// 初始化函数，读取tiles下所有的json文件
		/// </summary>
		/// <param name="print_tile_types">是否打印每个TileType的信息</param>
		public static void initialize(bool print_tile_types = false)
		{
			TileTypeManager.Initialize(print_tile_types);
		}


		/*******************************
				状态访问
		********************************/

		/// <summary>
		/// 获取初始化状态
		/// </summary>
		/// <returns>是否已初始化</returns>
		public static bool is_initialized()
		{
			return TileTypeManager.Initialized;
		}

		/// <summary>
		/// 获取TileType种类数量
		/// </summary>
		/// <returns>TileType的总数</returns>
		public static int type_count()
		{
			return TileTypeManager.TypeCount;
		}

		/// <summary>
		/// 获取最小可用运行ID
		/// </summary>
		/// <returns>下一个可分配的运行ID</returns>
		public static int min_available_run_id()
		{
			return TileTypeManager.MinAvailableRunId;
		}


		/*******************************
				TileType操作
		********************************/

		/// <summary>
		/// 添加新的TileType种类
		/// </summary>
		/// <param name="tile_type">TileType数据</param>
		/// <returns>分配的运行ID，若未初始化或添加失败则返回-1</returns>
		public static int add_type(TileType tile_type)
		{
			return TileTypeManager.AddType(tile_type);
		}


		/*******************************
				TileType数据访问
		********************************/

		/// <summary>
		/// 根据运行ID获取TileType数据
		/// </summary>
		/// <param name="run_id">TileType的运行ID</param>
		/// <returns>对应的TileType数据，若未初始化或不存在则返回null</returns>
		public static TileType get_type_by_run_id(int run_id)
		{
			return TileTypeManager.GetTypeByRunId(run_id);
		}


		/// <summary>
		/// 根据名称获取TileType数据
		/// </summary>
		/// <param name="name">TileType的名称</param>
		/// <returns>对应的TileType数据，若未初始化或不存在则返回null</returns>
		public static TileType get_type_by_name(string name)
		{
			return TileTypeManager.GetTypeByName(name);
		}


		/// <summary>
		/// 根据名称获取TileType的运行ID
		/// </summary>
		/// <param name="name">TileType的名称</param>
		/// <returns>对应的运行ID，若未初始化或不存在则返回-1</returns>
		public static int get_run_id_by_name(string name)
		{
			return TileTypeManager.GetRunIdByName(name);
		}


		/// <summary>
		/// 根据运行ID获取TileType的名称
		/// </summary>
		/// <param name="run_id">TileType的运行ID</param>
		/// <returns>对应的名称，若未初始化或不存在则返回null</returns>
		public static string get_name_by_run_id(int run_id)
		{
			return TileTypeManager.GetNameByRunId(run_id);
		}


		/// <summary>
		/// 检查指定名称的TileType是否存在
		/// </summary>
		/// <param name="name">TileType的名称</param>
		/// <returns>若存在则返回true，若未初始化或不存在则返回false</returns>
		public static bool contains_type_by_name(string name)
		{
			return TileTypeManager.ContainsType(name);
		}


		/// <summary>
		/// 检查指定运行ID的TileType是否存在
		/// </summary>
		/// <param name="run_id">TileType的运行ID</param>
		/// <returns>若存在则返回true，若未初始化或不存在则返回false</returns>
		public static bool contains_type_by_run_id(int run_id)
		{
			return TileTypeManager.ContainsType(run_id);
		}
	}


#pragma warning restore IDE1006
