using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;
using WorldWeaver.MapSystem.ChunkSystem.Persistence;
using WorldWeaver.MapSystem.LayerSystem;

namespace WorldWeaver.MapSystem.ChunkSystem
{
	/// <summary>
	/// 区块持久化器。
	/// <para>该类型负责区块数据的同步读写、异步任务调度与结果回收。</para>
	/// <para>当前实现基于 ChunkRegion 文件完成阻塞式与异步式持久化。</para>
	/// </summary>
	public static class ChunkPersistence
	{
		// ================================================================================
		//                                  公共配置
		// ================================================================================

		/// <summary>
		/// 最大并发异步任务数量。
		/// </summary>
		private const int MAX_CONCURRENT_TASKS = 24;

		/// <summary>
		/// 主线程阻塞式读写的冷却时长。
		/// </summary>
		private const ulong BLOCKING_COOLDOWN_MS = 700;

		/// <summary>
		/// 异步结果默认过期时长。
		/// </summary>
		private const ulong DEFAULT_RESULT_EXPIRATION_MS = 20000;

		/// <summary>
		/// 在冷却窗口内允许累计的阻塞请求上限。
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
			/// 请求成功完成。
			/// </summary>
			Success = 0,

			/// <summary>
			/// 当前暂时无法完成，稍后可重试。
			/// </summary>
			RetryLater = 1,

			/// <summary>
			/// 请求存在结构性错误，不应继续重试。
			/// </summary>
			PermanentFailure = 2
		}


		// ================================================================================
		//                                  阻塞窗口状态
		// ================================================================================

		/// <summary>
		/// 下一次允许进行阻塞式读写的时间戳。
		/// </summary>
		private static ulong nextAllowedBlockingTime;

		/// <summary>
		/// 当前冷却窗口内累计收到的阻塞请求数量。
		/// </summary>
		private static int blockingRequestCount;


		// ================================================================================
		//                                  异步状态
		// ================================================================================

		/// <summary>
		/// 当前活跃的异步任务表，键为区块 UID。
		/// </summary>
		private static readonly ConcurrentDictionary<string, Task> _ACTIVE_TASKS = new();

		/// <summary>
		/// 已完成异步请求的结果表。
		/// </summary>
		private static readonly ConcurrentDictionary<string, ResultEntry> _RESULT_TABLE = new();

		/// <summary>
		/// 控制异步任务并发数量的信号量。
		/// </summary>
		private static readonly SemaphoreSlim _CONCURRENCY_SEMAPHORE = new(MAX_CONCURRENT_TASKS);


		// ================================================================================
		//                                  私有结果类型
		// ================================================================================

		/// <summary>
		/// 异步加载任务的返回对象。
		/// </summary>
		private sealed class LoadTaskResult(PersistenceRequestResult requestResult, ChunkDataStorage storage)
		{
			/// <summary>
			/// 加载请求结果。
			/// </summary>
			public PersistenceRequestResult RequestResult { get; } = requestResult;

			/// <summary>
			/// 加载得到的区块存储对象。
			/// </summary>
			public ChunkDataStorage Storage { get; } = storage;
		}

		/// <summary>
		/// 结果表项。
		/// </summary>
		private sealed class ResultEntry(object result, ulong timestamp, PersistenceOperationType operationType)
		{
			/// <summary>
			/// 结果对象。
			/// </summary>
			public object Result = result;

			/// <summary>
			/// 写入结果表时的时间戳。
			/// </summary>
			public ulong Timestamp = timestamp;

			/// <summary>
			/// 对应的持久化操作类型。
			/// </summary>
			public PersistenceOperationType OperationType = operationType;
		}


		// ================================================================================
		//                                  阻塞式读写
		// ================================================================================

		/// <summary>
		/// 以阻塞方式保存区块数据。
		/// </summary>
		public static PersistenceRequestResult SaveBlocking(MapLayer ownerLayer, Chunk chunk, string saveDir)
		{
			if (!ValidateCommonArguments(ownerLayer, chunk, saveDir, nameof(SaveBlocking)))
			{
				return PersistenceRequestResult.PermanentFailure;
			}

			ulong currentTime = Time.GetTicksMsec();
			if (!TryEnterBlockingWindow(currentTime, chunk.Uid))
			{
				return PersistenceRequestResult.RetryLater;
			}

			if (chunk.Data == null)
			{
				return PersistenceRequestResult.Success;
			}

			if (!TryCreateChunkDataStorage(chunk.Data, out ChunkDataStorage storage))
			{
				GD.PushError($"[ChunkPersistence] SaveBlocking: 区块 {chunk.Uid} 的 ChunkData 无法转换为有效存储对象。");
				return PersistenceRequestResult.PermanentFailure;
			}

			try
			{
				string regionFilePath = GetRegionFilePath(saveDir, chunk.CPosition, true);
				if (string.IsNullOrWhiteSpace(regionFilePath))
				{
					SetBlockingCooldown(currentTime);
					return PersistenceRequestResult.PermanentFailure;
				}
				PersistenceRequestResult saveResult = ChunkRegionFreePartitionLockTable.ExecuteLocked(regionFilePath, () =>
				{
					if (!ChunkRegionFileAccessor.CreateRegion(saveDir, chunk.CPosition))
					{
						GD.PushError($"[ChunkPersistence] SaveBlocking: 无法为 {regionFilePath} 创建 Region 文件。");
						return PersistenceRequestResult.PermanentFailure;
					}

					using ChunkRegionWriter regionWriter = OpenRegionWriter(saveDir, chunk.CPosition);
					if (regionWriter == null)
					{
						GD.PushError($"[ChunkPersistence] SaveBlocking: 无法打开 Region 文件 {regionFilePath}。");
						return PersistenceRequestResult.PermanentFailure;
					}

					if (!regionWriter.SaveChunkStorage(chunk.CPosition, storage))
					{
						return PersistenceRequestResult.PermanentFailure;
					}

					return PersistenceRequestResult.Success;
				});

				SetBlockingCooldown(currentTime);
				return saveResult;
			}
			catch (IOException e)
			{
				SetBlockingCooldown(currentTime);
				GD.PushError($"[ChunkPersistence] SaveBlocking: 区块 {chunk.Uid} 保存失败: {e.Message}");
				return PersistenceRequestResult.RetryLater;
			}
			catch (UnauthorizedAccessException e)
			{
				SetBlockingCooldown(currentTime);
				GD.PushError($"[ChunkPersistence] SaveBlocking: 区块 {chunk.Uid} 保存被拒绝: {e.Message}");
				return PersistenceRequestResult.RetryLater;
			}
			catch (Exception e)
			{
				SetBlockingCooldown(currentTime);
				GD.PushError($"[ChunkPersistence] SaveBlocking: 区块 {chunk.Uid} 保存时发生异常: {e.Message}");
				return PersistenceRequestResult.RetryLater;
			}
		}

		/// <summary>
		/// 以阻塞方式加载区块数据。
		/// </summary>
		public static PersistenceRequestResult LoadBlocking(MapLayer ownerLayer, Chunk chunk, string saveDir, out ChunkDataStorage storage)
		{
			storage = null;
			if (!ValidateCommonArguments(ownerLayer, chunk, saveDir, nameof(LoadBlocking)))
			{
				return PersistenceRequestResult.PermanentFailure;
			}

			ulong currentTime = Time.GetTicksMsec();
			if (!TryEnterBlockingWindow(currentTime, chunk.Uid))
			{
				return PersistenceRequestResult.RetryLater;
			}

			try
			{
				string regionFilePath = GetRegionFilePath(saveDir, chunk.CPosition, false);
				if (string.IsNullOrWhiteSpace(regionFilePath))
				{
					SetBlockingCooldown(currentTime);
					return PersistenceRequestResult.PermanentFailure;
				}

				if (!ChunkRegionFileAccessor.IsRegionFileExists(saveDir, chunk.CPosition))
				{
					return PersistenceRequestResult.Success;
				}

				using ChunkRegionReader regionReader = OpenRegionReader(saveDir, chunk.CPosition);
				if (regionReader == null)
				{
					SetBlockingCooldown(currentTime);
					GD.PushError($"[ChunkPersistence] LoadBlocking: 无法打开 Region 文件 {regionFilePath}。");
					return PersistenceRequestResult.PermanentFailure;
				}

				if (!regionReader.LoadChunkStorage(chunk.CPosition, out storage))
				{
					SetBlockingCooldown(currentTime);
					return PersistenceRequestResult.PermanentFailure;
				}
				PersistenceRequestResult validateResult = ValidateLoadedStorage(storage, ownerLayer.ChunkSize);
				SetBlockingCooldown(currentTime);
				return validateResult;
			}
			catch (IOException e)
			{
				SetBlockingCooldown(currentTime);
				GD.PushError($"[ChunkPersistence] LoadBlocking: 区块 {chunk.Uid} 加载失败: {e.Message}");
				return PersistenceRequestResult.RetryLater;
			}
			catch (UnauthorizedAccessException e)
			{
				SetBlockingCooldown(currentTime);
				GD.PushError($"[ChunkPersistence] LoadBlocking: 区块 {chunk.Uid} 读取被拒绝: {e.Message}");
				return PersistenceRequestResult.RetryLater;
			}
			catch (Exception e)
			{
				SetBlockingCooldown(currentTime);
				GD.PushError($"[ChunkPersistence] LoadBlocking: 区块 {chunk.Uid} 读取时发生异常: {e.Message}");
				return PersistenceRequestResult.RetryLater;
			}
		}


		// ================================================================================
		//                                  异步接口
		// ================================================================================

		/// <summary>
		/// 尝试获取异步加载结果；若结果尚未就绪，则在需要时启动加载任务。
		/// </summary>
		public static PersistenceRequestResult TryLoadAsync(MapLayer ownerLayer, Chunk chunk, string saveDir, out ChunkDataStorage storage)
		{
			storage = null;
			if (!ValidateCommonArguments(ownerLayer, chunk, saveDir, nameof(TryLoadAsync)))
			{
				return PersistenceRequestResult.PermanentFailure;
			}

			string uid = chunk.Uid;
			string resultKey = GetResultKey(uid, PersistenceOperationType.Load);

			if (_RESULT_TABLE.TryRemove(resultKey, out ResultEntry entry))
			{
				if (entry.OperationType != PersistenceOperationType.Load)
				{
					GD.PushError($"[ChunkPersistence] TryLoadAsync: 结果表项 {resultKey} 的操作类型不匹配。");
					return PersistenceRequestResult.PermanentFailure;
				}

				if (entry.Result is not LoadTaskResult loadResult)
				{
					GD.PushError($"[ChunkPersistence] TryLoadAsync: 结果表项 {resultKey} 的数据类型不匹配。");
					return PersistenceRequestResult.PermanentFailure;
				}

				storage = loadResult.Storage;
				return loadResult.RequestResult;
			}

			if (_ACTIVE_TASKS.ContainsKey(uid))
			{
				return PersistenceRequestResult.RetryLater;
			}

			if (_ACTIVE_TASKS.Count >= MAX_CONCURRENT_TASKS)
			{
				return PersistenceRequestResult.RetryLater;
			}

			CreateLoadTask(uid, chunk.CPosition, saveDir, ownerLayer.ChunkSize);
			return PersistenceRequestResult.RetryLater;
		}

		/// <summary>
		/// 尝试获取异步保存结果；若结果尚未就绪，则在需要时启动保存任务。
		/// </summary>
		public static PersistenceRequestResult TrySaveAsync(MapLayer ownerLayer, Chunk chunk, string saveDir)
		{
			if (!ValidateCommonArguments(ownerLayer, chunk, saveDir, nameof(TrySaveAsync)))
			{
				return PersistenceRequestResult.PermanentFailure;
			}

			string uid = chunk.Uid;
			string resultKey = GetResultKey(uid, PersistenceOperationType.Save);

			if (_RESULT_TABLE.TryRemove(resultKey, out ResultEntry entry))
			{
				if (entry.OperationType != PersistenceOperationType.Save)
				{
					GD.PushError($"[ChunkPersistence] TrySaveAsync: 结果表项 {resultKey} 的操作类型不匹配。");
					return PersistenceRequestResult.PermanentFailure;
				}

				if (entry.Result is not PersistenceRequestResult requestResult)
				{
					GD.PushError($"[ChunkPersistence] TrySaveAsync: 结果表项 {resultKey} 的数据类型不匹配。");
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

			if (chunk.Data == null)
			{
				return PersistenceRequestResult.Success;
			}

			if (!TryCreateChunkDataStorage(chunk.Data, out ChunkDataStorage storage))
			{
				GD.PushError($"[ChunkPersistence] TrySaveAsync: 区块 {chunk.Uid} 的 ChunkData 无法转换为有效存储对象。");
				return PersistenceRequestResult.PermanentFailure;
			}

			CreateSaveTask(uid, chunk.CPosition, saveDir, storage);
			return PersistenceRequestResult.RetryLater;
		}


		// ================================================================================
		//                                  异步任务构建
		// ================================================================================

		/// <summary>
		/// 创建异步加载任务。
		/// </summary>
		private static void CreateLoadTask(string uid, ChunkPosition chunkPosition, string saveDir, MapElementSize expectedChunkSize)
		{
			string resultKey = GetResultKey(uid, PersistenceOperationType.Load);
			_ACTIVE_TASKS[uid] = Task.CompletedTask;
			_ = Task.Run(async () =>
			{
				await _CONCURRENCY_SEMAPHORE.WaitAsync();
				try
				{
					if (!ChunkRegionFileAccessor.IsRegionFileExists(saveDir, chunkPosition))
					{
						_RESULT_TABLE[resultKey] = new ResultEntry(
							new LoadTaskResult(PersistenceRequestResult.Success, null),
							Time.GetTicksMsec(),
							PersistenceOperationType.Load);
						return;
					}

					using ChunkRegionReader regionReader = OpenRegionReader(saveDir, chunkPosition);
					if (regionReader == null)
					{
						_RESULT_TABLE[resultKey] = new ResultEntry(
							new LoadTaskResult(PersistenceRequestResult.PermanentFailure, null),
							Time.GetTicksMsec(),
							PersistenceOperationType.Load);
						return;
					}

					if (!regionReader.LoadChunkStorage(chunkPosition, out ChunkDataStorage storage))
					{
						_RESULT_TABLE[resultKey] = new ResultEntry(
							new LoadTaskResult(PersistenceRequestResult.PermanentFailure, null),
							Time.GetTicksMsec(),
							PersistenceOperationType.Load);
						return;
					}

					PersistenceRequestResult requestResult = ValidateLoadedStorage(storage, expectedChunkSize);
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
		}

		/// <summary>
		/// 创建异步保存任务。
		/// </summary>
		private static void CreateSaveTask(string uid, ChunkPosition chunkPosition, string saveDir, ChunkDataStorage storage)
		{
			string resultKey = GetResultKey(uid, PersistenceOperationType.Save);
			_ACTIVE_TASKS[uid] = Task.CompletedTask;
			_ = Task.Run(async () =>
			{
				await _CONCURRENCY_SEMAPHORE.WaitAsync();
				try
				{
					string regionFilePath = GetRegionFilePath(saveDir, chunkPosition, true);
					if (string.IsNullOrWhiteSpace(regionFilePath))
					{
						_RESULT_TABLE[resultKey] = new ResultEntry(
							PersistenceRequestResult.PermanentFailure,
							Time.GetTicksMsec(),
							PersistenceOperationType.Save);
						return;
					}

					PersistenceRequestResult saveResult = ChunkRegionFreePartitionLockTable.ExecuteLocked(regionFilePath, () =>
					{
						if (!ChunkRegionFileAccessor.CreateRegion(saveDir, chunkPosition))
						{
							return PersistenceRequestResult.PermanentFailure;
						}

						using ChunkRegionWriter regionWriter = OpenRegionWriter(saveDir, chunkPosition);
						if (regionWriter == null)
						{
							return PersistenceRequestResult.PermanentFailure;
						}

						if (!regionWriter.SaveChunkStorage(chunkPosition, storage))
						{
							return PersistenceRequestResult.PermanentFailure;
						}

						return PersistenceRequestResult.Success;
					});

					_RESULT_TABLE[resultKey] = new ResultEntry(
						saveResult,
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
		}


		// ================================================================================
		//                                  通用工具方法
		// ================================================================================

		/// <summary>
		/// 清理过期的异步结果。
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
		private static string GetResultKey(string uid, PersistenceOperationType operationType)
		{
			return $"{uid}_{operationType}";
		}

		/// <summary>
		/// 生成指定区块所属的 Region 文件路径。
		/// </summary>
		private static string GetRegionFilePath(string saveDir, ChunkPosition chunkPosition, bool ensureDirectoryExists)
		{
			if (!ChunkRegionFilePath.TryGetRegionFilePath(saveDir, chunkPosition, out string regionFilePath))
			{
				GD.PushError($"[ChunkPersistence] 无法为区块 {chunkPosition} 生成有效 Region 文件路径。");
				return null;
			}

			if (ensureDirectoryExists)
			{
				string regionDirectoryPath = Path.GetDirectoryName(regionFilePath);
				if (!string.IsNullOrWhiteSpace(regionDirectoryPath))
				{
					Directory.CreateDirectory(regionDirectoryPath);
				}
			}

			return regionFilePath;
		}

		/// <summary>
		/// 打开指定区块所属的 Region 读取器。
		/// </summary>
		private static ChunkRegionReader OpenRegionReader(string rootPath, ChunkPosition chunkPosition)
		{
			Vector2I regionPosition = ChunkRegionPositionProcessor.GetRegionPosition(chunkPosition);
			return ChunkRegionReader.Open(rootPath, regionPosition);
		}

		/// <summary>
		/// 打开指定区块所属的 Region 写入器。
		/// </summary>
		private static ChunkRegionWriter OpenRegionWriter(string rootPath, ChunkPosition chunkPosition)
		{
			Vector2I regionPosition = ChunkRegionPositionProcessor.GetRegionPosition(chunkPosition);
			return ChunkRegionWriter.Open(rootPath, regionPosition);
		}

		/// <summary>
		/// 尝试进入阻塞式读写窗口。
		/// </summary>
		private static bool TryEnterBlockingWindow(ulong currentTime, string chunkUid)
		{
			if (currentTime >= nextAllowedBlockingTime)
			{
				blockingRequestCount = 0;
				return true;
			}

			if (blockingRequestCount == MAX_BLOCKING_REQUESTS_IN_COOLDOWN)
			{
				GD.PushError(
					$"[ChunkPersistence] 冷却窗口内的阻塞式请求次数超过阈值 {MAX_BLOCKING_REQUESTS_IN_COOLDOWN}，当前时间 {currentTime}，区块 {chunkUid}。");
			}

			blockingRequestCount++;
			return false;
		}

		/// <summary>
		/// 设置新的阻塞冷却截止时间。
		/// </summary>
		private static void SetBlockingCooldown(ulong currentTime)
		{
			nextAllowedBlockingTime = currentTime + BLOCKING_COOLDOWN_MS;
		}

		/// <summary>
		/// 校验持久化公共参数。
		/// </summary>
		private static bool ValidateCommonArguments(MapLayer ownerLayer, Chunk chunk, string saveDir, string callerName)
		{
			if (ownerLayer == null)
			{
				GD.PushError($"[ChunkPersistence] {callerName}: ownerLayer 参数不能为 null。");
				return false;
			}

			if (chunk == null)
			{
				GD.PushError($"[ChunkPersistence] {callerName}: chunk 参数不能为 null。");
				return false;
			}

			if (chunk == Chunk.EMPTY)
			{
				GD.PushError($"[ChunkPersistence] {callerName}: chunk 参数不能为 Empty。");
				return false;
			}

			if (string.IsNullOrWhiteSpace(chunk.Uid))
			{
				GD.PushError($"[ChunkPersistence] {callerName}: chunk.Uid 不能为空。");
				return false;
			}

			if (string.IsNullOrWhiteSpace(saveDir))
			{
				GD.PushError($"[ChunkPersistence] {callerName}: saveDir 不能为空。");
				return false;
			}

			return true;
		}

		/// <summary>
		/// 将运行时 ChunkData 转换为可持久化的 ChunkDataStorage。
		/// </summary>
		private static bool TryCreateChunkDataStorage(ChunkData data, out ChunkDataStorage storage)
		{
			storage = ChunkDataStorage.FromData(data);
			return storage != null;
		}

		/// <summary>
		/// 校验已加载的存储对象是否与当前区块尺寸匹配。
		/// </summary>
		private static PersistenceRequestResult ValidateLoadedStorage(ChunkDataStorage storage, MapElementSize expectedChunkSize)
		{
			if (storage == null)
			{
				return PersistenceRequestResult.Success;
			}

			if (!MapElementSize.IsValidExp(storage.WidthExp, storage.HeightExp))
			{
				GD.PushError(
					$"[ChunkPersistence] 已加载的 ChunkDataStorage 尺寸指数非法 ({storage.WidthExp}, {storage.HeightExp})。");
				return PersistenceRequestResult.PermanentFailure;
			}

			MapElementSize storageSize = new(storage.WidthExp, storage.HeightExp);
			if (storageSize != expectedChunkSize)
			{
				GD.PushError(
					$"[ChunkPersistence] 已加载的 ChunkDataStorage 尺寸不匹配，期望 {expectedChunkSize}，实际 {storageSize}。");
				return PersistenceRequestResult.PermanentFailure;
			}

			if (storage.Tiles == null)
			{
				GD.PushError("[ChunkPersistence] 已加载的 ChunkDataStorage 的 Tiles 不能为 null。");
				return PersistenceRequestResult.PermanentFailure;
			}

			if (storage.Tiles.Length != expectedChunkSize.Area)
			{
				GD.PushError(
					$"[ChunkPersistence] 已加载的 ChunkDataStorage 的 Tiles 长度不匹配，期望 {expectedChunkSize.Area}，实际 {storage.Tiles.Length}。");
				return PersistenceRequestResult.PermanentFailure;
			}

			return PersistenceRequestResult.Success;
		}
	}
}
