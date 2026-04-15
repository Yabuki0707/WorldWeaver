using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;
using WorldWeaver.MapSystem.GridSystem;
using WorldWeaver.MapSystem.LayerSystem;

namespace WorldWeaver.MapSystem.ChunkSystem
{
	/// <summary>
	/// 区块持久化器，负责区块数据的读写。
	/// <para>提供阻塞（带冷却）和非阻塞（带结果缓存与并发限制）两种方案。</para>
	/// <para>当前对外统一返回 <see cref="PersistenceRequestResult"/>，以便状态处理器准确区分“成功 / 稍后重试 / 永久失败”。</para>
	/// </summary>
	public static class ChunkPersistence
	{
		// ================================================================================
		//                                  公共配置
		// ================================================================================

		/// <summary>
		/// 最大并发任务数（非阻塞方案）。
		/// </summary>
		private const int MAX_CONCURRENT_TASKS = 12;

		/// <summary>
		/// 主线程阻塞式保存/加载冷却时间（毫秒）。
		/// </summary>
		private const ulong BLOCKING_COOLDOWN_MS = 700;

		/// <summary>
		/// 非阻塞方案结果缓存过期时间（毫秒）。
		/// </summary>
		private const ulong DEFAULT_RESULT_EXPIRATION_MS = 20000;

		/// <summary>
		/// 冷却期间最大阻塞式请求数。
		/// </summary>
		private const int MAX_BLOCKING_REQUESTS_IN_COOLDOWN = 128;


		// ================================================================================
		//                                  对外枚举
		// ================================================================================

		/// <summary>
		/// 持久化操作类型。
		/// </summary>
		public enum PersistenceOperationType
		{
			/// <summary>
			/// 加载操作。
			/// </summary>
			Load = 0,

			/// <summary>
			/// 保存操作。
			/// </summary>
			Save = 1
		}

		/// <summary>
		/// 持久化请求结果。
		/// </summary>
		public enum PersistenceRequestResult
		{
			/// <summary>
			/// 持久化操作成功。
			/// </summary>
			Success = 0,

			/// <summary>
			/// 当前无法完成，建议稍后重试。
			/// </summary>
			RetryLater = 1,

			/// <summary>
			/// 当前请求存在结构性错误，不应继续重试。
			/// </summary>
			PermanentFailure = 2
		}


		// ================================================================================
		//                                  阻塞方案状态
		// ================================================================================

		/// <summary>
		/// 下次允许阻塞式读写的时间戳。
		/// </summary>
		private static ulong nextAllowedBlockingTime = 0;

		/// <summary>
		/// 冷却期间累计收到的阻塞式请求数量。
		/// </summary>
		private static int blockingRequestCount = 0;


		// ================================================================================
		//                                  异步方案状态
		// ================================================================================

		/// <summary>
		/// 活跃任务表。
		/// <para>Key 使用 chunk uid；同一时刻同一区块只允许一个异步持久化任务存在。</para>
		/// </summary>
		private static readonly ConcurrentDictionary<string, Task> _ACTIVE_TASKS = new();

		/// <summary>
		/// 异步结果表。
		/// <para>Key 使用 <c>uid + operationType</c>，供后续轮询获取任务结果。</para>
		/// </summary>
		private static readonly ConcurrentDictionary<string, ResultEntry> _RESULT_TABLE = new();

		/// <summary>
		/// 用于限制异步任务并发数的信号量。
		/// </summary>
		private static readonly SemaphoreSlim _CONCURRENCY_SEMAPHORE = new(MAX_CONCURRENT_TASKS);


		// ================================================================================
		//                                  私有结果类型
		// ================================================================================

		/// <summary>
		/// 异步加载结果。
		/// </summary>
		private sealed class LoadTaskResult(PersistenceRequestResult requestResult, ChunkDataStorage storage)
		{
			/// <summary>
			/// 请求结果。
			/// </summary>
			public PersistenceRequestResult RequestResult { get; } = requestResult;

			/// <summary>
			/// 加载得到的区块储存对象。
			/// <para>文件不存在时允许为 <see langword="null"/>。</para>
			/// </summary>
			public ChunkDataStorage Storage { get; } = storage;
		}

		/// <summary>
		/// 结果表条目。
		/// </summary>
		private sealed class ResultEntry(object result, ulong timestamp, PersistenceOperationType type)
		{
			/// <summary>
			/// 结果对象。
			/// <para>加载操作对应 <see cref="LoadTaskResult"/>，保存操作对应 <see cref="PersistenceRequestResult"/>。</para>
			/// </summary>
			public object Result = result;

			/// <summary>
			/// 写入时间戳。
			/// </summary>
			public ulong Timestamp = timestamp;

			/// <summary>
			/// 操作类型。
			/// </summary>
			public PersistenceOperationType OperationType = type;
		}


		// ================================================================================
		//                                  阻塞方案
		// ================================================================================

		/// <summary>
		/// 阻塞式保存（带冷却限制）。
		/// </summary>
		public static PersistenceRequestResult SaveBlocking(MapLayer ownerLayer, Chunk chunk, string saveDir)
		{
			if (!ValidateCommonArguments(ownerLayer, chunk, nameof(SaveBlocking)))
			{
				return PersistenceRequestResult.PermanentFailure;
			}

			ulong currentTime = Time.GetTicksMsec();
			if (currentTime < nextAllowedBlockingTime)
			{
				// 冷却中直接要求稍后重试，同时保留超量请求诊断。
				if (blockingRequestCount == MAX_BLOCKING_REQUESTS_IN_COOLDOWN)
				{
					GD.PushError($"[ChunkPersistence] 阻塞式读写操作在冷却期间申请次数超过阈值 {MAX_BLOCKING_REQUESTS_IN_COOLDOWN}，当前时间={currentTime}，区块={chunk.Uid}。");
				}

				blockingRequestCount++;
				return PersistenceRequestResult.RetryLater;
			}

			// 当前请求已通过冷却窗口，重置冷却计数。
			blockingRequestCount = 0;

			// 无数据时视为无需持久化，直接成功。
			if (chunk.Data == null)
			{
				return PersistenceRequestResult.Success;
			}

			if (!TryCreateChunkDataStorage(chunk.Data, out ChunkDataStorage storage))
			{
				GD.PushError($"[ChunkPersistence] SaveBlocking: 区块 {chunk.Uid} 的 ChunkData 无法转换为有效储存对象。");
				return PersistenceRequestResult.PermanentFailure;
			}

			string path = GetChunkFilePath(ownerLayer, chunk, saveDir);
			try
			{
				// 统一将 ChunkData 写为压缩储存对象，确保同步/异步格式一致。
				File.WriteAllBytes(path, storage.ToCompressedBytes());
				nextAllowedBlockingTime = currentTime + BLOCKING_COOLDOWN_MS;
				return PersistenceRequestResult.Success;
			}
			catch (IOException e)
			{
				nextAllowedBlockingTime = currentTime + BLOCKING_COOLDOWN_MS;
				GD.PushError($"[ChunkPersistence] SaveBlocking: 保存区块 {chunk.Uid} 到 {path} 失败: {e.Message}");
				return PersistenceRequestResult.RetryLater;
			}
			catch (UnauthorizedAccessException e)
			{
				nextAllowedBlockingTime = currentTime + BLOCKING_COOLDOWN_MS;
				GD.PushError($"[ChunkPersistence] SaveBlocking: 保存区块 {chunk.Uid} 到 {path} 被拒绝: {e.Message}");
				return PersistenceRequestResult.RetryLater;
			}
			catch (Exception e)
			{
				nextAllowedBlockingTime = currentTime + BLOCKING_COOLDOWN_MS;
				GD.PushError($"[ChunkPersistence] SaveBlocking: 保存区块 {chunk.Uid} 时发生异常: {e.Message}");
				return PersistenceRequestResult.RetryLater;
			}
		}

		/// <summary>
		/// 阻塞式加载（带冷却限制）。
		/// </summary>
		public static PersistenceRequestResult LoadBlocking(MapLayer ownerLayer, Chunk chunk, string saveDir, out ChunkDataStorage storage)
		{
			storage = null;
			if (!ValidateCommonArguments(ownerLayer, chunk, nameof(LoadBlocking)))
			{
				return PersistenceRequestResult.PermanentFailure;
			}

			ulong currentTime = Time.GetTicksMsec();
			if (currentTime < nextAllowedBlockingTime)
			{
				if (blockingRequestCount == MAX_BLOCKING_REQUESTS_IN_COOLDOWN)
				{
					GD.PushError($"[ChunkPersistence] 阻塞式读写操作在冷却期间申请次数超过阈值 {MAX_BLOCKING_REQUESTS_IN_COOLDOWN}，当前时间={currentTime}，区块={chunk.Uid}。");
				}

				blockingRequestCount++;
				return PersistenceRequestResult.RetryLater;
			}

			blockingRequestCount = 0;
			string path = GetChunkFilePath(ownerLayer, chunk, saveDir);

			try
			{
				// 文件不存在是正常情况：表示当前区块尚无持久化数据。
				if (!File.Exists(path))
				{
					return PersistenceRequestResult.Success;
				}

				byte[] compressedBytes = File.ReadAllBytes(path);
				PersistenceRequestResult deserializeResult = TryDeserializeChunkDataStorage(compressedBytes, ownerLayer.ChunkSize, out storage);
				if (deserializeResult != PersistenceRequestResult.Success)
				{
					return deserializeResult;
				}

				nextAllowedBlockingTime = currentTime + BLOCKING_COOLDOWN_MS;
				return PersistenceRequestResult.Success;
			}
			catch (IOException e)
			{
				nextAllowedBlockingTime = currentTime + BLOCKING_COOLDOWN_MS;
				GD.PushError($"[ChunkPersistence] LoadBlocking: 从 {path} 加载区块 {chunk.Uid} 失败: {e.Message}");
				return PersistenceRequestResult.RetryLater;
			}
			catch (UnauthorizedAccessException e)
			{
				nextAllowedBlockingTime = currentTime + BLOCKING_COOLDOWN_MS;
				GD.PushError($"[ChunkPersistence] LoadBlocking: 读取 {path} 被拒绝: {e.Message}");
				return PersistenceRequestResult.RetryLater;
			}
			catch (Exception e)
			{
				nextAllowedBlockingTime = currentTime + BLOCKING_COOLDOWN_MS;
				GD.PushError($"[ChunkPersistence] LoadBlocking: 读取区块 {chunk.Uid} 时发生异常: {e.Message}");
				return PersistenceRequestResult.RetryLater;
			}
		}


		// ================================================================================
		//                                  异步方案
		// ================================================================================

		/// <summary>
		/// 尝试获取异步加载结果或启动任务。
		/// </summary>
		public static PersistenceRequestResult TryLoadAsync(MapLayer ownerLayer, Chunk chunk, string saveDir, out ChunkDataStorage storage)
		{
			storage = null;
			if (!ValidateCommonArguments(ownerLayer, chunk, nameof(TryLoadAsync)))
			{
				return PersistenceRequestResult.PermanentFailure;
			}

			string uid = chunk.Uid;
			string resultKey = GetResultKey(uid, PersistenceOperationType.Load);

			if (_RESULT_TABLE.TryRemove(resultKey, out ResultEntry entry))
			{
				if (entry.OperationType != PersistenceOperationType.Load)
				{
					GD.PushError($"[ChunkPersistence] TryLoadAsync: 结果表项 {resultKey} 的操作类型错误。"
					);
					return PersistenceRequestResult.PermanentFailure;
				}

				if (entry.Result is not LoadTaskResult loadResult)
				{
					GD.PushError($"[ChunkPersistence] TryLoadAsync: 结果表项 {resultKey} 的数据类型错误。"
					);
					return PersistenceRequestResult.PermanentFailure;
				}

				storage = loadResult.Storage;
				return loadResult.RequestResult;
			}

			// 若已有同区块异步任务在跑，则本轮仅返回稍后重试。
			if (_ACTIVE_TASKS.ContainsKey(uid))
			{
				return PersistenceRequestResult.RetryLater;
			}

			// 并发已满时，不启动新任务，保留后续轮询重试机会。
			if (_ACTIVE_TASKS.Count >= MAX_CONCURRENT_TASKS)
			{
				return PersistenceRequestResult.RetryLater;
			}

			CreateLoadTask(uid, GetChunkFilePath(ownerLayer, chunk, saveDir), ownerLayer.ChunkSize);
			return PersistenceRequestResult.RetryLater;
		}

		/// <summary>
		/// 尝试获取异步保存结果或启动任务。
		/// </summary>
		public static PersistenceRequestResult TrySaveAsync(MapLayer ownerLayer, Chunk chunk, string saveDir)
		{
			if (!ValidateCommonArguments(ownerLayer, chunk, nameof(TrySaveAsync)))
			{
				return PersistenceRequestResult.PermanentFailure;
			}

			string uid = chunk.Uid;
			string resultKey = GetResultKey(uid, PersistenceOperationType.Save);

			if (_RESULT_TABLE.TryRemove(resultKey, out ResultEntry entry))
			{
				if (entry.OperationType != PersistenceOperationType.Save)
				{
					GD.PushError($"[ChunkPersistence] TrySaveAsync: 结果表项 {resultKey} 的操作类型错误。"
					);
					return PersistenceRequestResult.PermanentFailure;
				}

				if (entry.Result is not PersistenceRequestResult requestResult)
				{
					GD.PushError($"[ChunkPersistence] TrySaveAsync: 结果表项 {resultKey} 的数据类型错误。"
					);
					return PersistenceRequestResult.PermanentFailure;
				}

				return requestResult;
			}

			if (_ACTIVE_TASKS.ContainsKey(uid))
			{
				return PersistenceRequestResult.RetryLater;
			}

			if (_ACTIVE_TASKS.Count >= MAX_CONCURRENT_TASKS)
			{
				return PersistenceRequestResult.RetryLater;
			}

			// 无数据时无需启动任务，直接视为保存成功。
			if (chunk.Data == null)
			{
				return PersistenceRequestResult.Success;
			}

			if (!TryCreateChunkDataStorage(chunk.Data, out ChunkDataStorage storage))
			{
				GD.PushError($"[ChunkPersistence] TrySaveAsync: 区块 {chunk.Uid} 的 ChunkData 无法转换为有效储存对象。");
				return PersistenceRequestResult.PermanentFailure;
			}

			CreateSaveTask(uid, GetChunkFilePath(ownerLayer, chunk, saveDir), storage);
			return PersistenceRequestResult.RetryLater;
		}


		// ================================================================================
		//                                  路径与任务方法
		// ================================================================================

		/// <summary>
		/// 获取区块存档路径。
		/// </summary>
		public static string GetChunkFilePath(MapLayer ownerLayer, Chunk chunk, string saveDir)
		{
			if (ownerLayer == null)
			{
				throw new ArgumentNullException(nameof(ownerLayer));
			}

			// 先将 Godot 的虚拟路径（如 user://）转换为操作系统可识别的绝对路径。
			string absoluteSaveDir = ProjectSettings.GlobalizePath(saveDir);

			// 1. 计算区块所属的 Grid 位置。
			MapGridPosition gridPos = chunk.CPosition.ToGridPosition(ownerLayer);
			string gridUid = gridPos.ToString();

			// 2. 拼接 Grid 目录路径。
			string gridDir = Path.Combine(absoluteSaveDir, gridUid);

			// 3. 确保目录存在。
			if (!DirAccess.DirExistsAbsolute(gridDir))
			{
				DirAccess.MakeDirRecursiveAbsolute(gridDir);
			}

			// 4. 返回完整文件路径。
			return Path.Combine(gridDir, $"chunk_{chunk.Uid}.json");
		}

		/// <summary>
		/// 创建异步加载任务。
		/// </summary>
		private static void CreateLoadTask(string uid, string path, MapElementSize expectedChunkSize)
		{
			string resultKey = GetResultKey(uid, PersistenceOperationType.Load);
			Task task = Task.Run(async () =>
			{
				await _CONCURRENCY_SEMAPHORE.WaitAsync();
				try
				{
					// 文件不存在视为成功，但没有持久化数据。
					if (!File.Exists(path))
					{
						_RESULT_TABLE[resultKey] = new ResultEntry(
							new LoadTaskResult(PersistenceRequestResult.Success, null),
							Time.GetTicksMsec(),
							PersistenceOperationType.Load);
						return;
					}

					byte[] compressedBytes = await File.ReadAllBytesAsync(path);
					PersistenceRequestResult requestResult = TryDeserializeChunkDataStorage(compressedBytes, expectedChunkSize, out ChunkDataStorage storage);
					_RESULT_TABLE[resultKey] = new ResultEntry(
						new LoadTaskResult(requestResult, storage),
						Time.GetTicksMsec(),
						PersistenceOperationType.Load);
				}
				catch (IOException e)
				{
					GD.PushError($"[ChunkPersistence] 异步加载区块({uid})失败: {e.Message}");
					_RESULT_TABLE[resultKey] = new ResultEntry(
						new LoadTaskResult(PersistenceRequestResult.RetryLater, null),
						Time.GetTicksMsec(),
						PersistenceOperationType.Load);
				}
				catch (UnauthorizedAccessException e)
				{
					GD.PushError($"[ChunkPersistence] 异步加载区块({uid})被拒绝: {e.Message}");
					_RESULT_TABLE[resultKey] = new ResultEntry(
						new LoadTaskResult(PersistenceRequestResult.RetryLater, null),
						Time.GetTicksMsec(),
						PersistenceOperationType.Load);
				}
				catch (Exception e)
				{
					GD.PushError($"[ChunkPersistence] 异步加载区块({uid})发生异常: {e.Message}");
					_RESULT_TABLE[resultKey] = new ResultEntry(
						new LoadTaskResult(PersistenceRequestResult.RetryLater, null),
						Time.GetTicksMsec(),
						PersistenceOperationType.Load);
				}
				finally
				{
					_CONCURRENCY_SEMAPHORE.Release();
					_ACTIVE_TASKS.TryRemove(uid, out _);
				}
			});

			_ACTIVE_TASKS[uid] = task;
		}

		/// <summary>
		/// 创建异步保存任务。
		/// </summary>
		private static void CreateSaveTask(string uid, string path, ChunkDataStorage storage)
		{
			string resultKey = GetResultKey(uid, PersistenceOperationType.Save);
			Task task = Task.Run(async () =>
			{
				await _CONCURRENCY_SEMAPHORE.WaitAsync();
				try
				{
					await File.WriteAllBytesAsync(path, storage.ToCompressedBytes());

					_RESULT_TABLE[resultKey] = new ResultEntry(
						PersistenceRequestResult.Success,
						Time.GetTicksMsec(),
						PersistenceOperationType.Save);
				}
				catch (IOException e)
				{
					GD.PushError($"[ChunkPersistence] 异步保存区块({uid})失败: {e.Message}");
					_RESULT_TABLE[resultKey] = new ResultEntry(
						PersistenceRequestResult.RetryLater,
						Time.GetTicksMsec(),
						PersistenceOperationType.Save);
				}
				catch (UnauthorizedAccessException e)
				{
					GD.PushError($"[ChunkPersistence] 异步保存区块({uid})被拒绝: {e.Message}");
					_RESULT_TABLE[resultKey] = new ResultEntry(
						PersistenceRequestResult.RetryLater,
						Time.GetTicksMsec(),
						PersistenceOperationType.Save);
				}
				catch (Exception e)
				{
					GD.PushError($"[ChunkPersistence] 异步保存区块({uid})发生异常: {e.Message}");
					_RESULT_TABLE[resultKey] = new ResultEntry(
						PersistenceRequestResult.RetryLater,
						Time.GetTicksMsec(),
						PersistenceOperationType.Save);
				}
				finally
				{
					_CONCURRENCY_SEMAPHORE.Release();
					_ACTIVE_TASKS.TryRemove(uid, out _);
				}
			});

			_ACTIVE_TASKS[uid] = task;
		}


		// ================================================================================
		//                                  工具方法
		// ================================================================================

		/// <summary>
		/// 清除过期的异步结果。
		/// </summary>
		public static void ClearExpiredResults(ulong maxAgeMs = DEFAULT_RESULT_EXPIRATION_MS)
		{
			ulong currentTime = Time.GetTicksMsec();
			List<string> toRemove = [];

			foreach (KeyValuePair<string, ResultEntry> pair in _RESULT_TABLE)
			{
				if (currentTime - pair.Value.Timestamp > maxAgeMs)
				{
					toRemove.Add(pair.Key);
				}
			}

			foreach (string key in toRemove)
			{
				_RESULT_TABLE.TryRemove(key, out _);
			}
		}

		/// <summary>
		/// 生成结果表键。
		/// </summary>
		private static string GetResultKey(string uid, PersistenceOperationType type)
		{
			return $"{uid}_{type}";
		}

		/// <summary>
		/// 验证阻塞/异步持久化共用参数。
		/// </summary>
		private static bool ValidateCommonArguments(MapLayer ownerLayer, Chunk chunk, string callerName)
		{
			if (ownerLayer == null)
			{
				GD.PushError($"[ChunkPersistence] {callerName}: ownerLayer 参数为 null。\n");
				return false;
			}

			if (chunk == null)
			{
				GD.PushError($"[ChunkPersistence] {callerName}: chunk 参数为 null。\n");
				return false;
			}

			if (chunk == Chunk.EMPTY)
			{
				GD.PushError($"[ChunkPersistence] {callerName}: chunk 参数为 Empty。\n");
				return false;
			}

			if (string.IsNullOrWhiteSpace(chunk.Uid))
			{
				GD.PushError($"[ChunkPersistence] {callerName}: chunk.Uid 为空。\n");
				return false;
			}

			return true;
		}

		/// <summary>
		/// 将 ChunkData 转换为可序列化储存对象。
		/// </summary>
		private static bool TryCreateChunkDataStorage(ChunkData data, out ChunkDataStorage storage)
		{
			storage = ChunkDataStorage.FromData(data);
			return storage != null;
		}

		/// <summary>
		/// 将压缩字节反序列化为 ChunkData 储存对象。
		/// </summary>
		private static PersistenceRequestResult TryDeserializeChunkDataStorage(byte[] compressedBytes, MapElementSize expectedChunkSize, out ChunkDataStorage storage)
		{
			storage = null;
			if (compressedBytes == null || compressedBytes.Length == 0)
			{
				GD.PushError("[ChunkPersistence] 反序列化 ChunkDataStorage 失败：压缩字节内容为空。\n");
				return PersistenceRequestResult.PermanentFailure;
			}

			storage = ChunkDataStorage.FromCompressedBytes(compressedBytes);
			if (storage == null)
			{
				GD.PushError("[ChunkPersistence] 反序列化 ChunkDataStorage 失败：压缩字节无法解压或 JSON 无法解析。\n");
				return PersistenceRequestResult.PermanentFailure;
			}

			// 持久化器只接受当前 ChunkDataStorage 结构，不做旧 JSON 格式兼容。
			if (!MapElementSize.IsValidExp(storage.WidthExp, storage.HeightExp))
			{
				GD.PushError($"[ChunkPersistence] 反序列化 ChunkDataStorage 失败：尺寸指数非法 ({storage.WidthExp}, {storage.HeightExp})。\n");
				return PersistenceRequestResult.PermanentFailure;
			}

			MapElementSize storageSize = new(storage.WidthExp, storage.HeightExp);
			if (storageSize != expectedChunkSize)
			{
				GD.PushError($"[ChunkPersistence] 反序列化 ChunkDataStorage 失败：尺寸不匹配，期望={expectedChunkSize}，实际={storageSize}。\n");
				return PersistenceRequestResult.PermanentFailure;
			}

			if (storage.Tiles == null)
			{
				GD.PushError("[ChunkPersistence] 反序列化 ChunkDataStorage 失败：Tiles 数组为 null。\n");
				return PersistenceRequestResult.PermanentFailure;
			}

			if (storage.Tiles.Length != expectedChunkSize.Area)
			{
				GD.PushError($"[ChunkPersistence] 反序列化 ChunkDataStorage 失败：Tiles 数组长度不匹配，期望={expectedChunkSize.Area}，实际={storage.Tiles.Length}。\n");
				return PersistenceRequestResult.PermanentFailure;
			}

			return PersistenceRequestResult.Success;
		}
	}
}
