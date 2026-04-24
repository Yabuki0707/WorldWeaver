using System.Collections.Generic;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem;
using WorldWeaver.MapSystem.ChunkSystem.State;
using WorldWeaver.MapSystem.LayerSystem;
using WorldWeaver.MapSystem.TileSystem;
using WorldWeaver.PixelShapeSystem.PointsShape;
using WorldWeaver.PixelShapeSystem.QuadrangleShape;
using WorldWeaver.PixelShapeSystem.ValueShape;


/// <summary>
/// 场景中的玩家测试节点。
/// <para>该节点保留原有的 WASD 移动行为，并在每个物理帧向地图层提交一片 5x5 区块的加载申请。</para>
/// <para>申请范围以当前节点所在区块为中心，目标稳定节点固定为 <see cref="ChunkStateNode.LoadedInGame"/>。</para>
/// </summary>
public partial class Sber : Node2D
{
	// ================================================================================
	//                                  常量
	// ================================================================================

	/// <summary>
	/// 单个 Tile 的像素尺寸。
	/// <para>当前场景中的 TileSet 使用 128x128。</para>
	/// </summary>
	private const int TILE_PIXEL_SIZE = 128;

	/// <summary>
	/// 请求矩形的半径。
	/// <para>当前固定为 2，对应总范围 5x5 区块。</para>
	/// </summary>
	private const int REQUEST_HALF_EXTENT = 10;

	/// <summary>
	/// 鼠标右键改地形时的 tile 半径。
	/// <para>当前固定为 2，对应总范围 5x5 tile。</para>
	/// </summary>
	private const int RIGHT_CLICK_TILE_HALF_EXTENT = 2;

	/// <summary>
	/// 右键写入使用的 TileType 名称。
	/// </summary>
	private const string DEEP_SEA_TILE_TYPE_NAME = "deep_sea";


	// ================================================================================
	//                                  运行时字段
	// ================================================================================

	/// <summary>
	/// 父节点。
	/// <para>按当前场景结构，Sber 直接挂在 <see cref="MapLayer"/> 下。</para>
	/// </summary>
	private Node _parentNode;

	/// <summary>
	/// 所属地图层。
	/// </summary>
	private MapLayer _ownerMapLayer;

	/// <summary>
	/// 地图渲染层。
	/// <para>用于按区块读取当前 TileMapLayer 中已经渲染出来的 atlas 坐标。</para>
	/// </summary>
	private MapVisualLayer _mapVisualLayer;

	/// <summary>
	/// 区块标记脚本资源。
	/// <para>运行时按需创建新的 <see cref="Sprite2D"/>，再挂载 <c>icon_3.gd</c>。</para>
	/// </summary>
	private Script _chunkMarkerScript;

	/// <summary>
	/// 每物理帧复用的请求区块坐标列表。
	/// </summary>
	private readonly List<Vector2I> _requestedChunkPositions = new(25);

	/// <summary>
	/// 每物理帧复用的请求状态列表。
	/// </summary>
	private readonly List<ChunkStateNode> _requestedChunkStates = new(25);

	/// <summary>
	/// 上一帧 E 键是否处于按下状态。
	/// <para>用于将持续按住 E 的输入压成单次打印。</para>
	/// </summary>
	private bool _wasEPressed;

	/// <summary>
	/// deep_sea 的运行时 TileRunId 缓存。
	/// <para>为 0 表示当前未解析成功。</para>
	/// </summary>
	private int _deepSeaTileRunId;


	// ================================================================================
	//                                  生命周期方法
	// ================================================================================

	/// <summary>
	/// Ready 阶段获取父节点，并从父节点下解析同级的地图层。
	/// </summary>
	public override void _Ready()
	{
		// 先记录父节点，后续解析 MapLayer 时以当前场景结构为准。
		_parentNode = GetParent();
		if (_parentNode == null)
		{
			GD.PushError("[Sber] 无法初始化：父节点不存在。");
			return;
		}
		
		_ownerMapLayer = _parentNode as MapLayer;
		if (_ownerMapLayer == null)
		{
			GD.PushError("[Sber] 无法初始化：父节点不是 MapLayer。");
		}
		else
		{

			// 运行时直接加载 icon_3.gd，避免复用场景里的现有节点导致其本体被隐藏或污染。
			_chunkMarkerScript = ResourceLoader.Load("res://icon_3.gd") as Script;
			if (_chunkMarkerScript == null)
			{
				GD.PushWarning("[Sber] 无法加载 icon_3.gd，鼠标点击时无法生成区块标记。");
			}
		}

		TileTypeManager.Initialize();
		_deepSeaTileRunId = TileTypeManager.GetRunIdByName(DEEP_SEA_TILE_TYPE_NAME);
		if (_deepSeaTileRunId <= 0)
		{
			GD.PushWarning($"[Sber] 未找到 TileType '{DEEP_SEA_TILE_TYPE_NAME}'，右键改深海功能将不可用。");
		}
	}

	/// <summary>
	/// 每帧处理测试移动输入。
	/// </summary>
	public override void _Process(double delta)
	{
		// 保留原有的测试移动逻辑。
		if (Input.IsKeyPressed(Key.W))
		{
			Position = new Vector2(Position.X, Position.Y - 75.0f);
		}

		if (Input.IsKeyPressed(Key.S))
		{
			Position = new Vector2(Position.X, Position.Y + 75.0f);
		}

		if (Input.IsKeyPressed(Key.A))
		{
			Position = new Vector2(Position.X - 75.0f, Position.Y);
		}

		if (Input.IsKeyPressed(Key.D))
		{
			Position = new Vector2(Position.X + 75.0f, Position.Y);
		}
		
	}

	/// <summary>
	/// 处理鼠标点击输入。
	/// <para>左键点击时，读取鼠标所在世界坐标，换算为区块坐标并生成一个区块标记。</para>
	/// <para>右键点击时，以鼠标所在 tile 为中心，将 5x5 范围内的 tile 直接改为 deep_sea。</para>
	/// </summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouseButtonEvent)
		{
			return;
		}

		if (!mouseButtonEvent.Pressed)
		{
			return;
		}

		if (mouseButtonEvent.ButtonIndex == MouseButton.Left)
		{
			SpawnChunkMarkerAtMousePosition();
			return;
		}

		if (mouseButtonEvent.ButtonIndex == MouseButton.Right)
		{
			PaintDeepSeaAtMousePosition();
		}
	}

	/// <summary>
	/// 每个物理帧都提交一份 5x5 区块范围的加载申请。
	/// </summary>
	public override void _PhysicsProcess(double delta)
	{
		if (_ownerMapLayer?.TheChunkManager == null)
		{
			return;
		}

		SubmitChunkLoadRequests();
	}


	// ================================================================================
	//                                  区块请求方法
	// ================================================================================

	/// <summary>
	/// 以当前节点所在区块为中心，提交一份 5x5 的 LoadedInGame 申请表。
	/// </summary>
	private void SubmitChunkLoadRequests()
	{
		// 先把当前节点位置解释为全局 Tile 坐标，再换算到所属区块。
		Vector2I currentGlobalTilePosition = new(
			Mathf.RoundToInt(GlobalPosition.X / TILE_PIXEL_SIZE),
			Mathf.RoundToInt(GlobalPosition.Y / TILE_PIXEL_SIZE));
		ChunkPosition centerChunkPosition =
			GlobalTilePositionConverter.ToChunkPosition(currentGlobalTilePosition, _ownerMapLayer.ChunkSize);

		// 复用列表前先清空，避免每帧重新分配。
		_requestedChunkPositions.Clear();
		_requestedChunkStates.Clear();

		for (int offsetY = -REQUEST_HALF_EXTENT; offsetY <= REQUEST_HALF_EXTENT; offsetY++)
		{
			for (int offsetX = -REQUEST_HALF_EXTENT; offsetX <= REQUEST_HALF_EXTENT; offsetX++)
			{
				// 生成以中心区块为基准的请求区块坐标。
				Vector2I requestedChunkPosition = new(
					centerChunkPosition.X + offsetX,
					centerChunkPosition.Y + offsetY);
				_requestedChunkPositions.Add(requestedChunkPosition);
				_requestedChunkStates.Add(ChunkStateNode.LoadedInGame);
			}
		}

		// 使用静态点列表与目标稳定状态列表构造完整快照式请求表。
		ChunkLoadRequestTable requestTable = ChunkLoadRequestTable.Create(
			new PixelValuesListShape<ChunkStateNode>(
				new PointListShape(_requestedChunkPositions),
				_requestedChunkStates));
		if (requestTable == null)
		{
			GD.PushError("[Sber] 构造区块加载请求表失败。");
			return;
		}

		_ownerMapLayer.TheChunkManager.HandleChunkLoadRequests(requestTable);
	}

	/// <summary>
	/// 在鼠标当前所在区块生成一个标记节点，并输出该区块的状态快照。
	/// </summary>
	private void SpawnChunkMarkerAtMousePosition()
	{
		if (_ownerMapLayer == null)
		{
			GD.PushWarning("[Sber] 当前未绑定 MapLayer，无法生成区块标记。");
			return;
		}

		if (_chunkMarkerScript == null)
		{
			GD.PushWarning("[Sber] 当前未加载 icon_3.gd，无法生成区块标记。");
			return;
		}

		Vector2 mouseGlobalPosition = GetGlobalMousePosition();
		Vector2I mouseGlobalTilePosition = ConvertGlobalPixelPositionToGlobalTilePosition(mouseGlobalPosition);
		ChunkPosition chunkPosition =
			GlobalTilePositionConverter.ToChunkPosition(mouseGlobalTilePosition, _ownerMapLayer.ChunkSize);

		Sprite2D chunkMarkerInstance = new();
		chunkMarkerInstance.SetScript(_chunkMarkerScript);
		chunkMarkerInstance.Set("chunk_position", chunkPosition.ToVector2I());
		_ownerMapLayer.AddChild(chunkMarkerInstance);
		PrintChunkStateSnapshot(chunkPosition);
	}

	/// <summary>
	/// 以鼠标当前所在 tile 为中心，将 5x5 范围内的 tile 直接改为 deep_sea。
	/// </summary>
	private void PaintDeepSeaAtMousePosition()
	{
		if (_ownerMapLayer?.TheChunkManager?.DataOperator == null)
		{
			GD.PushWarning("[Sber] 当前未绑定可用的 ChunkManager.DataOperator，无法执行右键改深海。");
			return;
		}

		if (_deepSeaTileRunId <= 0)
		{
			GD.PushWarning($"[Sber] 当前未解析到 TileType '{DEEP_SEA_TILE_TYPE_NAME}'，无法执行右键改深海。");
			return;
		}

		Vector2 mouseGlobalPosition = GetGlobalMousePosition();
		Vector2I mouseGlobalTilePosition = ConvertGlobalPixelPositionToGlobalTilePosition(mouseGlobalPosition);
		Vector2I rectangleOrigin = new(
			mouseGlobalTilePosition.X - RIGHT_CLICK_TILE_HALF_EXTENT,
			mouseGlobalTilePosition.Y - RIGHT_CLICK_TILE_HALF_EXTENT);
		RectangleShape rectangleShape = new(
			RIGHT_CLICK_TILE_HALF_EXTENT * 2 + 1,
			RIGHT_CLICK_TILE_HALF_EXTENT * 2 + 1,
			rectangleOrigin);

		_ownerMapLayer.TheChunkManager.DataOperator.SetTiles(new TileRegion(rectangleShape, _deepSeaTileRunId));
	}

	/// <summary>
	/// 将世界像素坐标转换为全局 Tile 坐标。
	/// <para>当前规则为每轴除以 128 并向下取整。</para>
	/// </summary>
	private static Vector2I ConvertGlobalPixelPositionToGlobalTilePosition(Vector2 globalPixelPosition)
	{
		return new Vector2I(
			Mathf.FloorToInt(globalPixelPosition.X / TILE_PIXEL_SIZE),
			Mathf.FloorToInt(globalPixelPosition.Y / TILE_PIXEL_SIZE));
	}

	/// <summary>
	/// 按指定格式输出区块状态快照。
	/// </summary>
	private void PrintChunkStateSnapshot(ChunkPosition chunkPosition)
	{
		Chunk chunk = _ownerMapLayer?.TheChunkManager?.GetChunk(chunkPosition);
		if (chunk?.State == null)
		{
			GD.Print($"{FormatChunkPosition(chunkPosition)}:N-None+None,T-None+None,F-None");
			return;
		}

		ChunkState state = chunk.State;
		GD.Print(
			$"{FormatChunkPosition(chunkPosition)}:N-{FormatNode(state.CurrentStableNode)}+{FormatNode(state.CurrentNode)},T-{FormatNode(state.TargetNode)},F-{FormatNode(state.FinalStableNode)}");
	}

	/// <summary>
	/// 格式化区块坐标文本。
	/// </summary>
	private static string FormatChunkPosition(ChunkPosition chunkPosition)
	{
		return $"({chunkPosition.X},{chunkPosition.Y})";
	}

	/// <summary>
	/// 格式化状态节点文本。
	/// </summary>
	private static string FormatNode(ChunkStateNode? stateNode)
	{
		return stateNode?.ToString() ?? "None";
	}




}
