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
        private const ulong PENDING_TASK_DISPATCH_INTERVAL_TICKS = 3;

        /// <summary>
        /// 已完成异步任务回收间隔 tick。
        /// <para>每经过该间隔，缓存器会从异步 worker 的完成队列中取回结果并更新缓存表。</para>
        /// </summary>
        private const ulong COMPLETED_TASK_COLLECT_INTERVAL_TICKS = 3;

        /// <summary>
        /// 缓存表过期扫描间隔 tick。
        /// <para>扫描只负责把过期缓存项转为 Store 待办任务，不直接执行文件写入。</para>
        /// </summary>
        private const ulong CACHE_SCAN_INTERVAL_TICKS = 3 * 30;

        /// <summary>
        /// 缓存项弥留时间 tick。
        /// <para>保存后的缓存项在该时长内优先服务后续夺取请求，避免频繁读写文件。</para>
        /// </summary>
        private const ulong CACHE_EXPIRATION_TICKS = 20 * 30;

        /// <summary>
        /// 单个 IO 任务组允许携带的最大 chunk 数量。
        /// <para>任务组仍必须满足同一 region、同一内部 IO 操作类型。</para>
        /// </summary>
        private const int MAX_CHUNK_COUNT_IN_TASK_GROUP = 4;

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

            // 夺取是缓存器的最高优先级路径：命中缓存时立即取走并删除，避免后续 Store 重复处理。
            if (CacheTable.TryTakeOutStorage(chunkKey, out storage))
            {
                PendingTaskTable.Remove(chunkKey);
                return PersistenceRequestResult.Success;
            }

            // 已有异步 IO 正在为该 chunk 工作时，只需要等待下一轮结果回收。
            if (_threadedWorker.HasTask(chunkKey))
            {
                return PersistenceRequestResult.RetryLater;
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
            // 待办表负责归桶，缓存器只负责把桶内 chunk key 改造成任务组并分发。
            Dictionary<(Vector2I RegionPosition, PersistenceOperationType OperationType), List<long>> pendingBuckets =
                PendingTaskTable.BuildRegionOperationBuckets();
            DispatchTaskGroups(pendingBuckets);
        }

        /// <summary>
        /// 按分桶顺序分发待办持久化任务组。
        /// <para>该方法直接遍历 region 分桶，临时列表满额或桶结束时立即构造并分发任务组。</para>
        /// </summary>
        /// <param name="buckets">按 region 坐标和操作类型收集出的 chunk key 分桶字典。</param>
        private void DispatchTaskGroups(
            Dictionary<(Vector2I RegionPosition, PersistenceOperationType OperationType), List<long>> buckets)
        {
            if (buckets == null) return;
            foreach (KeyValuePair<(Vector2I RegionPosition, PersistenceOperationType OperationType), List<long>> bucket in buckets)
            {
                List<long> pendingChunkKeys = [];
                foreach (long chunkKey in bucket.Value)
                {
                    // 遍历桶时直接审查单体任务，只有当前仍然可执行的 chunk key 才进入临时列表。
                    if (!CanDispatchPendingChunk(bucket.Key.OperationType, chunkKey))
                    {
                        GD.PushError($"[ChunkPersistenceCache] DispatchTaskGroups: chunk {chunkKey} 被判定无法分发。");
                        continue;
                    }

                    pendingChunkKeys.Add(chunkKey);

                    // 临时列表满了就立刻构造任务组并分发，避免再维护一份完整任务组列表。
                    if (pendingChunkKeys.Count >= MAX_CHUNK_COUNT_IN_TASK_GROUP)
                    {
                        RegionChunksPersistenceTaskGroup pendingGroup = new(
                            bucket.Key.RegionPosition,
                            bucket.Key.OperationType,
                            pendingChunkKeys);
                        if (!DispatchTaskGroup(pendingGroup))
                        {
                            return;
                        }

                        pendingChunkKeys.Clear();
                    }
                }

                // 一个 region 桶遍历结束后，把不足容量上限的尾组也立即分发。
                if (pendingChunkKeys.Count > 0)
                {
                    RegionChunksPersistenceTaskGroup pendingGroup = new(
                        bucket.Key.RegionPosition,
                        bucket.Key.OperationType,
                        pendingChunkKeys);
                    if (!DispatchTaskGroup(pendingGroup))
                    {
                        return;
                    }
                }
            }
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

            // 正常路径优先交给异步 worker；它会逐 chunk 写入占位并返回接受/拒绝列表。
            ChunkPersistenceAsyncStartResult startResult = _threadedWorker.TryStartTaskGroup(pendingGroup);
            if (startResult.RequestResult == PersistenceRequestResult.RetryLater)
            {
                // 异步槽位暂时不可用，停止本批剩余分发，交给下一轮继续。
                return false;
            }

            if (startResult.RequestResult == PersistenceRequestResult.PermanentFailure)
            {
                // 异步 worker 连续繁忙达到阈值后，用阻塞 worker 兜底处理当前可执行组。
                GD.Print(
                    $"[ChunkPersistenceCache] 异步持久化器繁忙达到阈值，改用阻塞型持久化器执行 {pendingGroup.OperationType} 任务组，chunk 数量: {pendingGroup.Count}，任务组信息: {pendingGroup}");
                PendingTaskTable.RemoveMultiple(pendingGroup);
                ProcessCompletedTask(_blockingWorker.Execute(pendingGroup), false);
                return true;
            }

            // 已被接受或明确拒绝的 chunk 都移出待办表；拒绝项通常已经有异步占位或冲突。
            PendingTaskTable.RemoveMultiple(startResult.AcceptedChunkKeys);
            PendingTaskTable.RemoveMultiple(startResult.RejectedChunkKeys);
            return true;
        }

        /// <summary>
        /// 审查待办表中的单个 chunk 任务当前是否仍可分发。
        /// <para>Read 任务要求缓存表仍未命中；Store 任务要求缓存表中存在非空且仍过期的缓存快照。</para>
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

            // Store 必须从缓存表取最新快照，因为真正写入文件的是缓存中的储存对象。
            if (!CacheTable.TryGetSnapshot(chunkKey, out ChunkPersistenceCacheSnapshot snapshot))
            {
                GD.PushError($"[ChunkPersistenceCache] CanDispatchPendingChunk: chunk {new ChunkPosition(chunkKey)} 存在 Store 待办，但缓存表中没有对应数据。");
                PendingTaskTable.Remove(chunkKey);
                return false;
            }

            // null 缓存只代表“读取确认无文件数据”，不能作为 Store 数据写入 region。
            if (snapshot.Storage == null)
            {
                GD.PushError($"[ChunkPersistenceCache] CanDispatchPendingChunk: chunk {new ChunkPosition(chunkKey)} 的 Store 待办对应空缓存。");
                PendingTaskTable.Remove(chunkKey);
                return false;
            }

            // 若缓存项在待办等待期间被 TrySave 刷新，说明弥留期重新开始，取消本次 Store。
            if (!snapshot.IsExpired(_currentTick, CACHE_EXPIRATION_TICKS))
            {
                PendingTaskTable.Remove(chunkKey);
                return false;
            }

            // Store 可执行组只携带 key，worker 执行时再按 key 读取当前缓存快照。
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
            foreach (ChunkPersistenceCacheSnapshot snapshot in CacheTable.GetExpiredSnapshots(_currentTick, CACHE_EXPIRATION_TICKS))
            {
                // null 缓存是“读到无旧数据”的结果，过期后直接移除，不需要 Store。
                if (snapshot.Storage == null)
                {
                    CacheTable.RemoveIfExpired(snapshot.ChunkKey, _currentTick, CACHE_EXPIRATION_TICKS);
                    continue;
                }

                // 同一 chunk 已经在执行 Store 时无需重复添加；Read 占位出现在过期缓存上属于异常状态。
                if (_threadedWorker.TryGetOperation(snapshot.ChunkKey, out PersistenceOperationType activeOperationType))
                {
                    if (activeOperationType == PersistenceOperationType.Store)
                    {
                        continue;
                    }

                    GD.PushError($"[ChunkPersistenceCache] ScanCacheForStoreTasks: 过期缓存 chunk {new ChunkPosition(snapshot.ChunkKey)} 存在 Read 异步任务，正在清理异常占位。");
                    _threadedWorker.ClearTaskOccupancy(snapshot.ChunkKey, PersistenceOperationType.Read);
                }

                // Store 待办已经存在时无需重复添加；Read 待办与当前缓存冲突，必须先移除。
                if (PendingTaskTable.TryGetOperation(snapshot.ChunkKey, out PersistenceOperationType pendingOperationType))
                {
                    if (pendingOperationType == PersistenceOperationType.Store)
                    {
                        continue;
                    }

                    // 缓存表代表当前最新数据，继续读取文件只会用旧文件数据反向污染缓存状态。
                    GD.PushError($"[ChunkPersistenceCache] ScanCacheForStoreTasks: 过期缓存 chunk {new ChunkPosition(snapshot.ChunkKey)} 存在 Read 待办任务，正在移除冲突待办。");
                    PendingTaskTable.Remove(snapshot.ChunkKey);
                }

                // 扫描阶段只收集需要 Store 的 chunk key，入待办表由 Update 统一完成。
                storeChunkKeys.Add(snapshot.ChunkKey);
            }

            return [ .. storeChunkKeys];
        }

        // ================================================================================
        //                                  完成任务回收
        // ================================================================================

        /// <summary>
        /// 回收异步 worker 已完成的任务。
        /// <para>该方法会持续取空已完成队列，使本轮 tick 能尽量处理全部已完成结果。</para>
        /// </summary>
        private void ProcessCompletedTasks()
        {
            while (_threadedWorker.TryDequeueCompletedTask(out ChunkPersistenceCompletedTask completedTask))
            {
                ProcessCompletedTask(completedTask, true);
            }
        }

        /// <summary>
        /// 处理单个完成任务。
        /// </summary>
        /// <param name="completedTask">完成任务对象。</param>
        /// <param name="clearAsyncOccupancy">是否需要清理异步 worker 中的 chunk 占位。</param>
        private void ProcessCompletedTask(ChunkPersistenceCompletedTask completedTask, bool clearAsyncOccupancy)
        {
            if (completedTask == null)
            {
                return;
            }

            foreach (ChunkPersistenceCompletedChunkResult chunkResult in completedTask.ChunkResults)
            {
                // 异步完成结果需要释放异步占位；阻塞兜底结果没有异步占位，不需要清理。
                if (clearAsyncOccupancy)
                {
                    _threadedWorker.ClearTaskOccupancy(chunkResult.ChunkKey, completedTask.OperationType);
                }

                // 成功结果进入正常回收路径：Read 写缓存，Store 尝试移除过期缓存。
                if (chunkResult.RequestResult == PersistenceRequestResult.Success)
                {
                    ProcessSuccessfulCompletedChunk(completedTask.OperationType, chunkResult);
                    continue;
                }

                // 临时失败保留重试机会，避免一次 IO 波动就丢掉任务。
                if (chunkResult.RequestResult == PersistenceRequestResult.RetryLater)
                {
                    RequeueRetryLaterChunk(completedTask.OperationType, chunkResult);
                    continue;
                }

                // 结构性失败只记录错误，不自动重试，避免坏数据或坏路径造成无限循环。
                GD.PushError(
                    $"[ChunkPersistenceCache] 持久化任务结构性失败，操作 {completedTask.OperationType}，chunk {chunkResult.ChunkPosition}: {chunkResult.ErrorMessage}");
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
                if (!CacheTable.Contains(chunkResult.ChunkKey))
                {
                    CacheTable.SetStorage(chunkResult.ChunkKey, chunkResult.Storage, _currentTick);
                }

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
            if (!CacheTable.TryGetSnapshot(chunkResult.ChunkKey, out ChunkPersistenceCacheSnapshot snapshot))
            {
                return;
            }

            // 只有缓存仍过期且没有其他任务占用时才重新加入 Store 待办。
            if (snapshot.Storage != null &&
                snapshot.IsExpired(_currentTick, CACHE_EXPIRATION_TICKS) &&
                !_threadedWorker.HasTask(chunkResult.ChunkKey) &&
                !PendingTaskTable.Contains(chunkResult.ChunkKey))
            {
                PendingTaskTable.Set(chunkResult.ChunkKey, PersistenceOperationType.Store);
            }
        }

    }
}
