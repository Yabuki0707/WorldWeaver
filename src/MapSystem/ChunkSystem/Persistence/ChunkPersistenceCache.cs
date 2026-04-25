using System.Collections.Generic;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;
using WorldWeaver.MapSystem.LayerSystem;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
	/// <summary>
	/// 持久化内部 IO 操作类型。
	/// <para>缓存器对外暴露 TakeOut / Save 两种缓存策略；内部实际 IO 只区分 Read / Store。</para>
	/// </summary>
	public enum PersistenceOperationType
	{
		/// <summary>
		/// 从 region 文件读取区块储存对象。
		/// </summary>
		Read = 0,

		/// <summary>
		/// 将缓存中的区块储存对象储存到 region 文件。
		/// </summary>
		Store = 1
	}

	/// <summary>
	/// 持久化请求结果。
	/// <para>该枚举用于状态机、缓存器、阻塞 worker 与异步 worker 之间传递统一的结果语义。</para>
	/// </summary>
	public enum PersistenceRequestResult
	{
		/// <summary>
		/// 请求成功完成。
		/// <para>对于缓存器夺取，表示已经拿到结果；对于保存，表示已经写入缓存；对于内部 IO，表示任务执行完成。</para>
		/// </summary>
		Success = 0,

		/// <summary>
		/// 当前暂时无法完成，稍后可重试。
		/// <para>该结果通常表示异步任务尚未完成、线程池暂时繁忙，或 IO 出现可重试异常。</para>
		/// </summary>
		RetryLater = 1,

		/// <summary>
		/// 请求存在结构性错误，不应按原请求继续重试。
		/// <para>该结果通常表示参数、数据结构、路径或持久化格式存在不可自动恢复的问题。</para>
		/// </summary>
		PermanentFailure = 2
	}

	/// <summary>
	/// 区块持久化缓存器。
	/// <para>该类型是持久化缓存策略入口，只对外提供夺取与保存两种操作。</para>
	/// <para>夺取负责“取出缓存并删除，若无缓存则请求 Read IO”；保存负责“写入缓存，等待过期扫描触发 Store IO”。</para>
	/// </summary>
	public sealed class ChunkPersistenceCache
	{
		// ================================================================================
		//                                  常量
		// ================================================================================

		/// <summary>
		/// 待办任务分发间隔 tick。
		/// <para>每经过该间隔，缓存器会尝试把待办表中的 Read / Store 任务分组成实际 IO 任务。</para>
		/// </summary>
		private const ulong PENDING_TASK_DISPATCH_INTERVAL_TICKS = 4;

		/// <summary>
		/// 已完成异步任务回收间隔 tick。
		/// <para>每经过该间隔，缓存器会从异步 worker 的完成队列中取回结果并更新缓存表。</para>
		/// </summary>
		private const ulong COMPLETED_TASK_COLLECT_INTERVAL_TICKS = 4;

		/// <summary>
		/// 缓存表过期扫描间隔 tick。
		/// <para>扫描只负责把过期缓存项转为 Store 待办任务，不直接执行文件写入。</para>
		/// </summary>
		private const ulong CACHE_SCAN_INTERVAL_TICKS = 3 * 30;

		/// <summary>
		/// 缓存项弥留时间 tick。
		/// <para>保存后的缓存项在该时长内优先服务后续夺取请求，避免频繁读写文件。</para>
		/// </summary>
		private const ulong CACHE_EXPIRATION_TICKS = 7 * 30;

		/// <summary>
		/// 单个 IO 任务组允许携带的最大 chunk 数量。
		/// <para>任务组仍必须满足同一 region、同一内部 IO 操作类型。</para>
		/// </summary>
		private const int MAX_CHUNK_COUNT_IN_TASK_GROUP = 4;

		/// <summary>
		/// 单次从待办分桶中尝试分发一个任务组的结果。
		/// </summary>
		private enum PendingBucketDispatchResult
		{
			/// <summary>
			/// 已成功分发一个任务组。
			/// </summary>
			Dispatched = 0,

			/// <summary>
			/// 当前操作类型已经没有可分发任务。
			/// </summary>
			NoAvailableTask = 1,

			/// <summary>
			/// 任务组分发失败，本轮分发流程应立即终止。
			/// </summary>
			DispatchFailed = 2
		}

		// ================================================================================
		//                                  字段
		// ================================================================================

		/// <summary>
		/// 阻塞型持久化 worker。
		/// <para>当异步 worker 持续繁忙达到阈值时，缓存器会使用它进行主线程兜底 IO。</para>
		/// </summary>
		private readonly ChunkPersistenceBlockingWorker _blockingWorker;

		/// <summary>
		/// 异步型持久化 worker。
		/// <para>正常情况下所有 Read / Store IO 都优先交给它执行。</para>
		/// </summary>
		private readonly ChunkPersistenceThreadedWorker _threadedWorker;

		/// <summary>
		/// 当前缓存器 tick。
		/// <para>由 <see cref="Update"/> 每次调用自增，所有周期调度都基于该值计算。</para>
		/// </summary>
		private ulong _currentTick;

		/// <summary>
		/// 内存缓存表，始终代表当前系统认定的最新区块储存信息。
		/// </summary>
		public ChunkPersistenceCacheTable CacheTable { get; }

		/// <summary>
		/// 待办任务表。
		/// </summary>
		public ChunkPersistencePendingTaskTable PendingTaskTable { get; }

		/// <summary>
		/// region 文件根路径。
		/// <para>缓存器只保存根路径上下文，内部任务调度以 region 坐标为基准传递。</para>
		/// </summary>
		public string RootPath { get; }

		// ================================================================================
		//                                  构造
		// ================================================================================

		/// <summary>
		/// 创建持久化缓存器。
		/// </summary>
		/// <param name="ownerLayer">当前缓存器所属地图层。</param>
		public ChunkPersistenceCache(MapLayer ownerLayer)
		{
			// 缓存器绑定到具体地图层，后续所有 IO 都围绕该层的储存路径和 chunk 尺寸执行。
			RootPath = ownerLayer.StorageFilePath;

			// 缓存表与待办表是缓存器的两张核心内存表。
			CacheTable = new ChunkPersistenceCacheTable();
			PendingTaskTable = new ChunkPersistencePendingTaskTable();

			// 异步 worker 复用阻塞 worker 的实际 IO 实现，避免两套 region 读写逻辑分叉。
			_blockingWorker = new ChunkPersistenceBlockingWorker(ownerLayer, RootPath, CacheTable);
			_threadedWorker = new ChunkPersistenceThreadedWorker(ownerLayer, _blockingWorker);
		}

		// ================================================================================
		//                                  生命周期入口
		// ================================================================================

		/// <summary>
		/// 推进缓存器一轮 tick。
		/// <para>该方法由 <see cref="ChunkManager.Update"/> 驱动，是缓存器所有周期逻辑的唯一入口。</para>
		/// </summary>
		public void Update()
		{
			// 先推进 tick，后续周期判断都使用本轮最新 tick。
			_currentTick++;

			// 优先回收已完成任务，让刚完成的 Read 结果尽快进入缓存表。
			if (_currentTick % COMPLETED_TASK_COLLECT_INTERVAL_TICKS == 0)
			{
				ProcessCompletedTasks();
			}

			// 再扫描过期缓存，把长期未被夺取的保存结果转成 Store 待办。
			if (_currentTick % CACHE_SCAN_INTERVAL_TICKS == 0)
			{
				foreach (long chunkKey in ScanCacheForStoreTasks())
				{
					PendingTaskTable.Set(chunkKey, PersistenceOperationType.Store);
				}
			}

			// 最后分发待办任务；这样本轮扫描出的 Store 待办可以尽快进入 IO 流程。
			if (_currentTick % PENDING_TASK_DISPATCH_INTERVAL_TICKS == 0)
			{
				DispatchPendingTasks();
			}
		}

		// ================================================================================
		//                                  缓存请求
		// ================================================================================

		/// <summary>
		/// 夺取区块储存对象，或在缓存未命中时创建读取待办任务。
		/// <para>夺取命中时会从缓存表中取出并删除该缓存项；缓存未命中时注册 Read 待办并返回 RetryLater。</para>
		/// </summary>
		/// <param name="chunk">请求夺取储存对象的 chunk。</param>
		/// <param name="storage">成功夺取到的储存对象；可以为 null，表示文件中没有旧数据。</param>
		/// <returns>夺取成功、等待 IO 或结构性失败。</returns>
		public PersistenceRequestResult TryTakeOut(Chunk chunk, out ChunkDataStorage storage)
		{
			storage = null;
			if (Chunk.IsNullOrEmpty(chunk))
			{
				GD.PushError("[ChunkPersistenceCache] TryTakeOut: chunk 不能为空或 Chunk.EMPTY。");
				return PersistenceRequestResult.PermanentFailure;
			}

			long chunkKey = chunk.CPosition.ToKey();

			// 已有异步 IO 正在处理该 chunk 时不能夺取缓存；Store 后台任务仍可能需要读取缓存项。
			if (_threadedWorker.HasTask(chunkKey))
			{
				return PersistenceRequestResult.RetryLater;
			}

			// 夺取命中时立即取走并删除缓存，表示该缓存项已经被当前运行时 chunk 消费。
			if (CacheTable.TryTakeOutStorage(chunkKey, out storage))
			{
				PendingTaskTable.Remove(chunkKey);
				return PersistenceRequestResult.Success;
			}

			// 已经有待办任务时不重复添加，保证同一 chunk 同时只有一个待办请求。
			if (PendingTaskTable.Contains(chunkKey))
			{
				return PersistenceRequestResult.RetryLater;
			}

			// 缓存、异步占位、待办表都没有，才创建新的 Read 待办。
			PendingTaskTable.Set(chunkKey, PersistenceOperationType.Read);
			return PersistenceRequestResult.RetryLater;
		}

		/// <summary>
		/// 保存当前 chunk 数据到缓存表。
		/// <para>该方法不执行实际磁盘写入；后续由缓存扫描创建 Store 任务完成延迟储存。</para>
		/// </summary>
		/// <param name="chunk">需要保存当前内存数据的 chunk。</param>
		/// <returns>缓存写入是否成功。</returns>
		public PersistenceRequestResult TrySave(Chunk chunk)
		{
			if (Chunk.IsNullOrEmpty(chunk))
			{
				GD.PushError("[ChunkPersistenceCache] TrySave: chunk 不能为空或 Chunk.EMPTY。");
				return PersistenceRequestResult.PermanentFailure;
			}

			if (chunk.Data == null)
			{
				// 没有内存数据就没有可保存内容，这在退出空 chunk 时是合法路径。
				return PersistenceRequestResult.Success;
			}

			// 保存操作的本质是把运行时 ChunkData 固化为缓存表中的 ChunkDataStorage。
			ChunkDataStorage storage = ChunkDataStorage.FromData(chunk.Data);
			if (storage == null)
			{
				GD.PushError($"[ChunkPersistenceCache] TrySave: chunk {chunk.CPosition} 无法转换为 ChunkDataStorage。");
				return PersistenceRequestResult.PermanentFailure;
			}

			long chunkKey = chunk.CPosition.ToKey();

			// 写入缓存表会刷新弥留 tick，使接下来一段时间内的读取直接从内存夺取。
			CacheTable.SetStorage(chunkKey, storage, _currentTick);

			// 保存后的缓存项已经代表最新数据，旧的待办需求不再可靠，直接移除。
			PendingTaskTable.Remove(chunkKey);
			return PersistenceRequestResult.Success;
		}

		// ================================================================================
		//                                  待办任务分发
		// ================================================================================

		/// <summary>
		/// 分发待办任务表中的区块任务。
		/// <para>该方法只从待办表读取区块任务单体，并把它们改造成 region 级任务组后交给 worker。</para>
		/// </summary>
		private void DispatchPendingTasks()
		{
			// 待办表负责生成一次性分桶，缓存器按优先度把桶内 chunk key 改造成任务组并分发。
			// 分桶本身不做可执行性判断，因为缓存表和异步占用表在分桶后仍可能发生变化。
			// 因此本方法只拿到“本轮待尝试的候选集合”，真正能不能执行留给后续遍历过程逐项确认。
			List<(Vector2I RegionPosition, long[] ChunkKeys)>[] pendingBuckets =
				PendingTaskTable.BuildRegionOperationBuckets();
			DispatchTaskGroups(pendingBuckets);
		}

		/// <summary>
		/// 按操作优先度分发待办持久化任务组。
		/// <para>该方法在同一个循环框架内交替处理优势操作类型与劣势操作类型，分发失败会终止整轮分发。</para>
		/// </summary>
		/// <param name="operationBuckets">按操作类型索引保存的一次性 region 分桶数组。</param>
		private void DispatchTaskGroups(List<(Vector2I RegionPosition, long[] ChunkKeys)>[] operationBuckets)
		{
			if (operationBuckets == null) return;

			// 缓存表保存调度偏好：正数代表 Read 优先，负数代表 Store 优先。
			// 这里把符号和绝对值拆成“优势操作类型、劣势操作类型、优势连续配额”三部分，
			// 后面的循环就不再关心正负号，只按两组桶和一个配额推进。
			CacheTable.GetPrioritizedOperationBuckets(
				operationBuckets,
				// 优势操作类型
				out PersistenceOperationType priorityOperationType,
				// 优势操作类型的待办region桶列表。
				out List<(Vector2I RegionPosition, long[] ChunkKeys)> priorityBuckets,
				// 劣势操作类型
				out PersistenceOperationType secondaryOperationType,
				// 劣势操作类型的待办region桶列表。
				out List<(Vector2I RegionPosition, long[] ChunkKeys)> secondaryBuckets,
				// 优势操作类型连续分发任务组数量。
				out int priorityTaskGroupQuota);

			// 本轮分桶中需要尝试分配的 chunk key 总数。
			int pendingChunkCount = CountPendingBucketChunkKeys(operationBuckets);
			// 本轮已经成功分发的任务组数量。
			int dispatchedTaskGroupCount = 0;
			// 优势操作类型当前扫描到的 region 桶索引。
			int priorityBucketIndex = 0;
			// 优势操作类型当前扫描到的桶内 chunk key 索引。
			int priorityChunkIndex = 0;
			// 劣势操作类型当前扫描到的 region 桶索引。
			int secondaryBucketIndex = 0;
			// 劣势操作类型当前扫描到的桶内 chunk key 索引。
			int secondaryChunkIndex = 0;
			while (true)
			{
				// 当前优劣调度轮中是否成功分发过任务组。
				bool dispatchedAnyTaskGroup = false;

				// 先分发优势操作类型，连续分发数量由缓存表中的优先度绝对值控制。
				for (int priorityDispatchAttemptIndex = 0;
					priorityDispatchAttemptIndex < priorityTaskGroupQuota;
					priorityDispatchAttemptIndex++)
				{
					PendingBucketDispatchResult priorityResult = DispatchNextTaskGroupFromBuckets(
						priorityOperationType,
						priorityBuckets,
						ref priorityBucketIndex,
						ref priorityChunkIndex);
					if (priorityResult == PendingBucketDispatchResult.DispatchFailed)
					{
						// 分发失败通常意味着异步槽位暂时不可用，继续遍历只会制造更多失败判断。
						// 直接终止本轮分桶分发，未消费的桶元素会保留在待办表里，下一轮重新分桶再处理。
						GD.Print(
							$"[ChunkPersistenceCache] DispatchTaskGroups: {priorityOperationType} 任务组分发失败，整体分发流程已终止。已分发任务组数: {dispatchedTaskGroupCount}，估算已分发于异步任务的区块数: {dispatchedTaskGroupCount * MAX_CHUNK_COUNT_IN_TASK_GROUP}，本轮要分配的区块总数: {pendingChunkCount}。");
						return;
					}

					if (priorityResult == PendingBucketDispatchResult.NoAvailableTask)
					{
						// 优势方本轮已经没有可分发任务，不强行凑够配额，立刻尝试劣势方。
						break;
					}

					dispatchedAnyTaskGroup = true;
					dispatchedTaskGroupCount++;
				}

				// 每轮优势操作类型达到配额后，劣势操作类型只尝试分发一个任务组。
				PendingBucketDispatchResult secondaryResult = DispatchNextTaskGroupFromBuckets(
					secondaryOperationType,
					secondaryBuckets,
					ref secondaryBucketIndex,
					ref secondaryChunkIndex);
				if (secondaryResult == PendingBucketDispatchResult.DispatchFailed)
				{
					// 劣势方分发失败也同样终止整轮流程，避免一次调度 tick 内反复撞同一个限制。
					GD.Print(
						$"[ChunkPersistenceCache] DispatchTaskGroups: {secondaryOperationType} 任务组分发失败，整体分发流程已终止。已分发任务组数: {dispatchedTaskGroupCount}，估算已分发于异步任务的区块数: {dispatchedTaskGroupCount * MAX_CHUNK_COUNT_IN_TASK_GROUP}，本轮要分配的区块总数: {pendingChunkCount}。");
					return;
				}

				if (secondaryResult == PendingBucketDispatchResult.Dispatched)
				{
					dispatchedAnyTaskGroup = true;
					dispatchedTaskGroupCount++;
				}

				// 两边都没有可分发任务时，本轮一次性分桶已经耗尽。
				if (!dispatchedAnyTaskGroup)
				{
					return;
				}
			}
		}

		/// <summary>
		/// 统计一次性分桶中包含的 chunk key 总数。
		/// </summary>
		/// <param name="operationBuckets">按操作类型索引保存的一次性 region 分桶数组。</param>
		/// <returns>本轮分桶中待尝试分配的 chunk key 总数。</returns>
		private static int CountPendingBucketChunkKeys(List<(Vector2I RegionPosition, long[] ChunkKeys)>[] operationBuckets)
		{
			if (operationBuckets == null)
			{
				return 0;
			}

			// 统计所有操作类型下的桶内 chunk 数量。
			int chunkCount = 0;
			foreach (List<(Vector2I RegionPosition, long[] ChunkKeys)> operationBucketList in operationBuckets)
			{
				if (operationBucketList == null)
				{
					continue;
				}

				foreach ((Vector2I _, long[] chunkKeys) in operationBucketList)
				{
					chunkCount += chunkKeys?.Length ?? 0;
				}
			}

			return chunkCount;
		}

		/// <summary>
		/// 从指定操作类型的 region 分桶中取出并分发下一个任务组。
		/// <para>该方法会沿用桶索引与桶内索引作为游标，避免每次都从头扫描一次性分桶。</para>
		/// </summary>
		/// <param name="operationType">当前要分发的内部 IO 操作类型。</param>
		/// <param name="buckets">当前操作类型对应的 region 桶列表。</param>
		/// <param name="bucketIndex">当前 region 桶索引。</param>
		/// <param name="chunkIndex">当前桶内 chunk key 索引。</param>
		/// <returns>本次尝试的分发结果。</returns>
		private PendingBucketDispatchResult DispatchNextTaskGroupFromBuckets(
			PersistenceOperationType operationType,
			List<(Vector2I RegionPosition, long[] ChunkKeys)> buckets,
			ref int bucketIndex,
			ref int chunkIndex)
		{
			if (buckets == null || buckets.Count == 0)
			{
				// 当前操作类型没有任何 region 桶，本次自然无法产出任务组。
				return PendingBucketDispatchResult.NoAvailableTask;
			}

			// 临时列表只服务当前待构建任务组；满额或当前 region 桶结束时立即分发。
			List<long> pendingChunkKeys = [];
			while (bucketIndex < buckets.Count)
			{
				(Vector2I regionPosition, long[] chunkKeys) = buckets[bucketIndex];
				if (chunkKeys == null || chunkKeys.Length == 0)
				{
					// 空桶没有可扫描内容，推进到下一个 region 桶，并把桶内游标复位。
					bucketIndex++;
					chunkIndex = 0;
					continue;
				}

				while (chunkIndex < chunkKeys.Length)
				{
					// 读取当前 chunk key。
					long chunkKey = chunkKeys[chunkIndex];
					// 桶内游标立即推进到下一个候选项，避免下一次进入方法时重复扫描当前元素。
					chunkIndex++;

					// 遍历桶时直接审查单体任务，只有当前仍然可执行的 chunk key 才进入临时列表。
					if (!CanDispatchPendingChunk(operationType, chunkKey))
					{
						// 不可分发的任务会在审查方法内部按原因清理或保留；这里继续看同桶后续元素。
						continue;
					}

					// 进入临时列表即代表该 chunk 已经被当前游标消费；游标不会倒退，因此不需要额外哨兵标记。
					pendingChunkKeys.Add(chunkKey);
					if (pendingChunkKeys.Count >= MAX_CHUNK_COUNT_IN_TASK_GROUP)
					{
						// 达到任务组容量上限就立刻分发，不等待当前 region 桶继续遍历。
						// 当前游标已经指向下一候选 chunk，下一次进入本方法会从这里继续。
						RegionChunksPersistenceTaskGroup pendingGroup = new(regionPosition, operationType, pendingChunkKeys);
						if (!DispatchTaskGroup(pendingGroup))
						{
							return PendingBucketDispatchResult.DispatchFailed;
						}

						return PendingBucketDispatchResult.Dispatched;
					}
				}

				// 当前 region 桶结束时，尾组不足容量上限也要立即分发，但不跨 region 拼组。
				bucketIndex++;
				chunkIndex = 0;
				if (pendingChunkKeys.Count > 0)
				{
					// region 桶结束后必须先发出尾组，因为任务组要求同一 region。
					// 不能拿下一个 region 的 chunk 来补满当前临时列表。
					RegionChunksPersistenceTaskGroup pendingGroup = new(regionPosition, operationType, pendingChunkKeys);
					if (!DispatchTaskGroup(pendingGroup))
					{
						return PendingBucketDispatchResult.DispatchFailed;
					}

					return PendingBucketDispatchResult.Dispatched;
				}
			}

			// 所有 region 桶都扫完，且没有形成任何任务组。
			return PendingBucketDispatchResult.NoAvailableTask;
		}

		/// <summary>
		/// 分发单个待办任务组。
		/// <para>调用该方法前，任务组内的 chunk key 已经在分桶遍历阶段完成可执行性审查。</para>
		/// </summary>
		/// <param name="pendingGroup">已经通过可执行性审查的待办任务组。</param>
		/// <returns>是否可以继续分发后续任务组。</returns>
		private bool DispatchTaskGroup(RegionChunksPersistenceTaskGroup pendingGroup)
		{
			if (pendingGroup.IsEmpty)
			{
				GD.PushWarning($"[ChunkPersistenceCache] DispatchTaskGroup: 任务组 {pendingGroup} 为空，直接返回。");
				return true;
			}

			// 正常路径优先交给异步 worker；它会登记可执行 chunk 并启动后台 IO。
			PersistenceRequestResult startResult = _threadedWorker.TryStartTaskGroup(pendingGroup);
			if (startResult == PersistenceRequestResult.RetryLater)
			{
				// 异步槽位暂时不可用，停止本批剩余分发，交给下一轮继续。
				return false;
			}

			// 只要不是稍后重试，本组待办都已经被本次分发流程接管，直接移除整个任务组。
			PendingTaskTable.RemoveMultiple(pendingGroup);

			if (startResult == PersistenceRequestResult.PermanentFailure)
			{
				// 异步路径返回结构性失败时，用阻塞 worker 兜底处理当前可执行组。
				GD.PushWarning(
					$"[ChunkPersistenceCache] 异步持久化器返回结构性错误，改用阻塞型持久化器执行 {pendingGroup.OperationType} 任务组，chunk 数量: {pendingGroup.Count}，任务组信息: {pendingGroup}");
				ProcessCompletedTask(pendingGroup.OperationType, _blockingWorker.Execute(pendingGroup));
			}
			return true;
		}

		/// <summary>
		/// 审查待办表中的单个 chunk 任务当前是否仍可分发。
		/// <para>Read 任务要求缓存表仍未命中；Store 任务要求缓存表中存在非空且仍过期的缓存项。</para>
		/// </summary>
		/// <param name="operationType">待办任务的内部 IO 操作类型。</param>
		/// <param name="chunkKey">待审查的 chunk key。</param>
		/// <returns>该 chunk 任务是否可以加入当前临时任务列表。</returns>
		private bool CanDispatchPendingChunk(PersistenceOperationType operationType, long chunkKey)
		{
			if (operationType == PersistenceOperationType.Read)
			{
				// Read 分发前确认缓存没有被 TrySave 或其他 Read 结果填充。
				if (CacheTable.Contains(chunkKey))
				{
					GD.PushError($"[ChunkPersistenceCache] CanDispatchPendingChunk: chunk {new ChunkPosition(chunkKey)} 已被 TrySave 或其他 Read 结果填充，无法分发。");
					PendingTaskTable.Remove(chunkKey);
					return false;
				}

				return true;
			}

			// Store 必须从缓存表取最新缓存项，因为真正写入文件的是缓存中的储存对象。
			if (!CacheTable.TryGetStorageInfo(chunkKey, out ChunkDataStorage storage, out ulong storedTick))
			{
				GD.PushError($"[ChunkPersistenceCache] CanDispatchPendingChunk: chunk {new ChunkPosition(chunkKey)} 存在 Store 待办，但缓存表中没有对应数据。");
				PendingTaskTable.Remove(chunkKey);
				return false;
			}

			// null 缓存只代表“读取确认无文件数据”，不能作为 Store 数据写入 region。
			if (storage == null)
			{
				GD.PushError($"[ChunkPersistenceCache] CanDispatchPendingChunk: chunk {new ChunkPosition(chunkKey)} 的 Store 待办对应空缓存。");
				PendingTaskTable.Remove(chunkKey);
				return false;
			}

			// 若缓存项在待办等待期间被 TrySave 刷新，说明弥留期重新开始，取消本次 Store。
			if (!ChunkPersistenceCacheTable.IsExpired(storedTick, _currentTick, CACHE_EXPIRATION_TICKS))
			{
				PendingTaskTable.Remove(chunkKey);
				return false;
			}

			// Store 可执行组只携带 key，worker 执行时再按 key 读取当前缓存项。
			return true;
		}

		// ================================================================================
		//                                  缓存扫描
		// ================================================================================

		/// <summary>
		/// 扫描缓存表并找出需要创建 Store 待办的 chunk key。
		/// <para>缓存表是最高优先级且最新的信息源；当非空缓存已经存在时，不能再让 Read 待办从文件读取旧数据回填缓存。</para>
		/// </summary>
		/// <returns>本轮扫描中需要加入 Store 待办表的 chunk key 数组。</returns>
		private long[] ScanCacheForStoreTasks()
		{
			List<long> storeChunkKeys = [];
			foreach ((long chunkKey, ChunkDataStorage storage) in CacheTable.GetExpiredEntries(_currentTick, CACHE_EXPIRATION_TICKS))
			{
				// null 缓存是“读到无旧数据”的结果，过期后直接移除，不需要 Store。
				if (storage == null)
				{
					CacheTable.RemoveIfExpired(chunkKey, _currentTick, CACHE_EXPIRATION_TICKS);
					continue;
				}

				// 同一 chunk 已经在执行 Store 时无需重复添加；Read 占位出现在过期缓存上属于异常状态。
				if (_threadedWorker.TryGetOperation(chunkKey, out PersistenceOperationType activeOperationType))
				{
					if (activeOperationType == PersistenceOperationType.Store)
					{
						continue;
					}

					GD.PushError($"[ChunkPersistenceCache] ScanCacheForStoreTasks: 过期缓存 chunk {new ChunkPosition(chunkKey)} 存在 Read 异步任务，正在清理异常占位。");
					_threadedWorker.ClearTaskOccupancy(chunkKey, PersistenceOperationType.Read);
				}

				// Store 待办已经存在时无需重复添加；Read 待办与当前缓存冲突，必须先移除。
				if (PendingTaskTable.TryGetOperation(chunkKey, out PersistenceOperationType pendingOperationType))
				{
					if (pendingOperationType == PersistenceOperationType.Store)
					{
						continue;
					}

					// 缓存表代表当前最新数据，继续读取文件只会用旧文件数据反向污染缓存状态。
					GD.PushError($"[ChunkPersistenceCache] ScanCacheForStoreTasks: 过期缓存 chunk {new ChunkPosition(chunkKey)} 存在 Read 待办任务，正在移除冲突待办。");
					PendingTaskTable.Remove(chunkKey);
				}

				// 扫描阶段只收集需要 Store 的 chunk key，入待办表由 Update 统一完成。
				storeChunkKeys.Add(chunkKey);
			}

			return [ .. storeChunkKeys];
		}

		// ================================================================================
		//                                  完成任务回收
		// ================================================================================

		/// <summary>
		/// 回收异步 worker 已完成的任务。
		/// <para>该方法会扫描异步区块组任务列表，并回收本轮已经完成的全部任务。</para>
		/// </summary>
		private void ProcessCompletedTasks()
		{
			List<ChunkPersistenceThreadedWorker.AsyncPersistenceTask> completedTasks =
				_threadedWorker.TakeOutCompletedTasks();
			foreach (ChunkPersistenceThreadedWorker.AsyncPersistenceTask completedTask in completedTasks)
			{
				ProcessCompletedTask(completedTask.OperationType, completedTask.CompletedChunkResults);
			}
		}

		/// <summary>
		/// 处理单个完成任务。
		/// </summary>
		/// <param name="operationType">完成任务所属内部 IO 操作类型。</param>
		/// <param name="chunkResults">完成任务中的 chunk 结果列表。</param>
		private void ProcessCompletedTask(
			PersistenceOperationType operationType,
			IReadOnlyList<ChunkPersistenceCompletedChunkResult> chunkResults)
		{
			if (chunkResults == null)
			{
				return;
			}

			foreach (ChunkPersistenceCompletedChunkResult chunkResult in chunkResults)
			{
				// 成功结果进入正常回收路径：Read 写缓存，Store 尝试移除过期缓存。
				if (chunkResult.RequestResult == PersistenceRequestResult.Success)
				{
					ProcessSuccessfulCompletedChunk(operationType, chunkResult);
					continue;
				}

				// 临时失败保留重试机会，避免一次 IO 波动就丢掉任务。
				if (chunkResult.RequestResult == PersistenceRequestResult.RetryLater)
				{
					RequeueRetryLaterChunk(operationType, chunkResult);
					continue;
				}

				// 结构性失败只记录错误，不自动重试，避免坏数据或坏路径造成无限循环。
				GD.PushError(
					$"[ChunkPersistenceCache] 持久化任务结构性失败，操作 {operationType}，chunk {new ChunkPosition(chunkResult.ChunkKey)}: {chunkResult.ErrorMessage}");
			}
		}

		/// <summary>
		/// 处理成功完成的单 chunk 结果。
		/// </summary>
		/// <param name="operationType">完成任务所属内部 IO 操作类型。</param>
		/// <param name="chunkResult">单 chunk 完成结果。</param>
		private void ProcessSuccessfulCompletedChunk(
			PersistenceOperationType operationType,
			ChunkPersistenceCompletedChunkResult chunkResult)
		{
			if (operationType == PersistenceOperationType.Read)
			{
				// Read 结果只在缓存表仍未出现该 chunk 时写入，避免旧 IO 覆盖 TrySave 写入的新缓存。
				if (CacheTable.Contains(chunkResult.ChunkKey))
				{
					GD.PushWarning($"[ChunkPersistenceCache] ProcessSuccessfulCompletedChunk: chunk {new ChunkPosition(chunkResult.ChunkKey)} 已存在缓存，却存在于已完成的 Read 异步任务中。");
					return;
				}
				CacheTable.SetStorage(chunkResult.ChunkKey, chunkResult.Storage, _currentTick);
				return;
			}

			if (operationType == PersistenceOperationType.Store)
			{
				// Store 完成后必须校验 tick，防止旧 Store 删除后续 TrySave 刷新的新缓存。
				CacheTable.RemoveIfExpiredAndStoredTickMatches(
					chunkResult.ChunkKey,
					chunkResult.CacheStoredTick,
					_currentTick,
					CACHE_EXPIRATION_TICKS);
			}
		}

		/// <summary>
		/// 将临时失败的 chunk 重新放回待办表。
		/// </summary>
		/// <param name="operationType">失败任务所属内部 IO 操作类型。</param>
		/// <param name="chunkResult">单 chunk 失败结果。</param>
		private void RequeueRetryLaterChunk(
			PersistenceOperationType operationType,
			ChunkPersistenceCompletedChunkResult chunkResult)
		{
			if (operationType == PersistenceOperationType.Read)
			{
				// Read 重试前必须确认缓存、异步占位和待办表都没有该 chunk。
				if (!CacheTable.Contains(chunkResult.ChunkKey) &&
					!_threadedWorker.HasTask(chunkResult.ChunkKey) &&
					!PendingTaskTable.Contains(chunkResult.ChunkKey))
				{
					PendingTaskTable.Set(chunkResult.ChunkKey, PersistenceOperationType.Read);
				}

				return;
			}

			// Store 重试依赖缓存表仍存在对应数据；缓存被夺取后不再重试 Store。
			if (!CacheTable.TryGetStorageInfo(chunkResult.ChunkKey, out ChunkDataStorage storage, out ulong storedTick))
			{
				return;
			}

			// 只有缓存仍过期且没有其他任务占用时才重新加入 Store 待办。
			if (storage != null &&
				ChunkPersistenceCacheTable.IsExpired(storedTick, _currentTick, CACHE_EXPIRATION_TICKS) &&
				!_threadedWorker.HasTask(chunkResult.ChunkKey) &&
				!PendingTaskTable.Contains(chunkResult.ChunkKey))
			{
				PendingTaskTable.Set(chunkResult.ChunkKey, PersistenceOperationType.Store);
			}
		}

	}
}
