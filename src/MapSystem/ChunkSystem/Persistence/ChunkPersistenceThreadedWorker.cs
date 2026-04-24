using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;
using WorldWeaver.MapSystem.LayerSystem;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// 异步任务创建结果。
    /// </summary>
    /// <param name="requestResult">异步任务创建请求的整体结果。</param>
    /// <param name="acceptedChunkKeys">成功写入异步任务表并进入本次任务的 chunk key 列表。</param>
    /// <param name="rejectedChunkKeys">因为异步任务表冲突或占位失败而被拒绝的 chunk key 列表。</param>
    internal sealed class ChunkPersistenceAsyncStartResult(
        PersistenceRequestResult requestResult,
        IReadOnlyList<long> acceptedChunkKeys,
        IReadOnlyList<long> rejectedChunkKeys)
    {
        /// <summary>
        /// 创建请求结果。
        /// </summary>
        public PersistenceRequestResult RequestResult { get; } = requestResult;

        /// <summary>
        /// 已经进入异步任务表占位的 chunk key。
        /// </summary>
        public IReadOnlyList<long> AcceptedChunkKeys { get; } = acceptedChunkKeys;

        /// <summary>
        /// 因异步任务表冲突被拒绝的 chunk key。
        /// </summary>
        public IReadOnlyList<long> RejectedChunkKeys { get; } = rejectedChunkKeys;
    }

    /// <summary>
    /// 单个 chunk 的持久化完成结果。
    /// </summary>
    /// <param name="chunkKey">区块坐标 long key。</param>
    /// <param name="chunkPosition">区块坐标。</param>
    /// <param name="requestResult">该 chunk 的实际执行结果。</param>
    /// <param name="storage">Read 成功时返回的区块储存对象；Store 任务中为 null。</param>
    /// <param name="cacheStoredTick">Store 任务创建时记录的缓存写入 tick；Read 任务中为 0。</param>
    /// <param name="errorMessage">失败或异常时携带的错误信息。</param>
    internal readonly struct ChunkPersistenceCompletedChunkResult(
        long chunkKey,
        ChunkPosition chunkPosition,
        PersistenceRequestResult requestResult,
        ChunkDataStorage storage,
        ulong cacheStoredTick,
        string errorMessage)
    {
        /// <summary>
        /// 区块坐标 long key。
        /// </summary>
        public long ChunkKey { get; } = chunkKey;

        /// <summary>
        /// 区块坐标。
        /// </summary>
        public ChunkPosition ChunkPosition { get; } = chunkPosition;

        /// <summary>
        /// 单 chunk 执行结果。
        /// </summary>
        public PersistenceRequestResult RequestResult { get; } = requestResult;

        /// <summary>
        /// 读取任务返回的储存对象；储存任务中该值为 null。
        /// </summary>
        public ChunkDataStorage Storage { get; } = storage;

        /// <summary>
        /// 储存任务创建时对应的缓存写入 tick。
        /// </summary>
        public ulong CacheStoredTick { get; } = cacheStoredTick;

        /// <summary>
        /// 错误信息。
        /// </summary>
        public string ErrorMessage { get; } = errorMessage;
    }

    /// <summary>
    /// 已完成的持久化任务结构体。
    /// <para>异步型持久化器完成 task 后把该对象交回缓存器，由缓存器在主循环中统一回收。</para>
    /// </summary>
    /// <param name="operationType">任务对应的内部 IO 操作类型。</param>
    /// <param name="chunkResults">任务组内每个 chunk 的执行结果。</param>
    internal sealed class ChunkPersistenceCompletedTask(
        PersistenceOperationType operationType,
        IReadOnlyList<ChunkPersistenceCompletedChunkResult> chunkResults)
    {
        /// <summary>
        /// 任务操作类型。
        /// </summary>
        public PersistenceOperationType OperationType { get; } = operationType;

        /// <summary>
        /// 涉及的 chunk 结果列表。
        /// </summary>
        public IReadOnlyList<ChunkPersistenceCompletedChunkResult> ChunkResults { get; } = chunkResults;
    }

    /// <summary>
    /// 异步型持久化器。
    /// <para>该类型持有异步任务表与已完成任务列表；异步任务表仅作为 chunk 级占位表使用。</para>
    /// </summary>
    public sealed class ChunkPersistenceThreadedWorker
    {
        /// <summary>
        /// 异步持久化任务最大并发数。
        /// <para>该值限制的是任务组数量，不是单个 chunk 数量。</para>
        /// </summary>
        private const int MAX_CONCURRENT_TASKS = 24;

        /// <summary>
        /// 异步 worker 连续繁忙阈值。
        /// <para>达到该阈值时，创建请求返回结构性失败，由缓存器改用阻塞型持久化器兜底。</para>
        /// </summary>
        private const int BUSY_COUNT_THRESHOLD = 128;

        /// <summary>
        /// 实际执行 region IO 的阻塞型 worker。
        /// <para>异步 worker 本身只负责任务占位、线程调度和完成队列，不复制底层 IO 逻辑。</para>
        /// </summary>
        private readonly ChunkPersistenceBlockingWorker _ioWorker;

        /// <summary>
        /// 异步任务占位表。
        /// <para>键为区块坐标 long key，值为当前正在执行的内部 IO 操作类型。</para>
        /// </summary>
        private readonly ConcurrentDictionary<long, PersistenceOperationType> _activeTaskTable = new();

        /// <summary>
        /// 已完成异步任务队列。
        /// <para>后台任务只向该队列写入完成结果，缓存器在主循环中定期取出并回收。</para>
        /// </summary>
        private readonly ConcurrentQueue<ChunkPersistenceCompletedTask> _completedTasks = new();

        /// <summary>
        /// 异步任务组计数锁。
        /// <para>用于保护 <see cref="_activeTaskCount"/> 与 <see cref="_busyCount"/> 的复合判断和更新。</para>
        /// </summary>
        private readonly object _activeTaskCountLock = new();

        /// <summary>
        /// 当前正在执行的异步任务组数量。
        /// </summary>
        private int _activeTaskCount;

        /// <summary>
        /// 连续申请异步任务但无可用槽位的次数。
        /// </summary>
        private int _busyCount;

        /// <summary>
        /// 创建异步型持久化器。
        /// </summary>
        /// <param name="ownerLayer">所属地图层；当外部没有提供阻塞 worker 时用于创建默认 worker。</param>
        /// <param name="ioWorker">实际执行 region IO 的阻塞型 worker。</param>
        public ChunkPersistenceThreadedWorker(MapLayer ownerLayer, ChunkPersistenceBlockingWorker ioWorker)
        {
            // 异步 worker 只做调度包装，底层 IO 始终委托给同一套阻塞实现。
            _ioWorker = ioWorker ?? new ChunkPersistenceBlockingWorker(ownerLayer);
        }

        /// <summary>
        /// 查询指定 chunk 是否已经存在异步任务占位。
        /// </summary>
        /// <param name="chunkKey">需要查询的区块坐标 long key。</param>
        /// <returns>该 chunk 当前是否已经被异步任务占用。</returns>
        public bool HasTask(long chunkKey)
        {
            return _activeTaskTable.ContainsKey(chunkKey);
        }

        /// <summary>
        /// 尝试读取指定 chunk 的异步任务操作类型。
        /// </summary>
        /// <param name="chunkKey">需要查询的区块坐标 long key。</param>
        /// <param name="operationType">读取到的异步任务操作类型。</param>
        /// <returns>异步任务表中是否存在该 chunk。</returns>
        public bool TryGetOperation(long chunkKey, out PersistenceOperationType operationType)
        {
            return _activeTaskTable.TryGetValue(chunkKey, out operationType);
        }

        /// <summary>
        /// 在操作类型匹配时清理指定 chunk 的异步任务占位。
        /// </summary>
        /// <param name="chunkKey">需要清理占位的区块坐标 long key。</param>
        /// <param name="expectedOperationType">调用方期望清理的操作类型。</param>
        /// <returns>是否成功移除了匹配的占位。</returns>
        public bool ClearTaskOccupancy(long chunkKey, PersistenceOperationType expectedOperationType)
        {
            // 先校验存在性和操作类型，防止 Read 回收误删同 chunk 后续 Store 占位。
            if (!_activeTaskTable.TryGetValue(chunkKey, out PersistenceOperationType actualOperationType))
            {
                return false;
            }

            if (actualOperationType != expectedOperationType)
            {
                return false;
            }

            return _activeTaskTable.TryRemove(chunkKey, out _);
        }

        /// <summary>
        /// 尝试创建异步任务组。
        /// <para>若当前没有可用异步槽位，则累计繁忙次数；繁忙次数达到阈值时返回结构性失败，由缓存器改用阻塞型持久化器兜底。</para>
        /// </summary>
        /// <param name="taskGroup">需要异步执行的任务组。</param>
        /// <returns>异步任务创建结果，包含成功占位和被拒绝的 chunk key。</returns>
        internal ChunkPersistenceAsyncStartResult TryStartTaskGroup(RegionChunksPersistenceTaskGroup taskGroup)
        {
            if (taskGroup.IsEmpty)
            {
                // 空任务组没有需要执行的 IO，视为创建成功。
                return new ChunkPersistenceAsyncStartResult(PersistenceRequestResult.Success, [], []);
            }

            // 任务组级别先申请并发槽位，避免在无可用线程时提前污染异步任务表。
            if (!TryReserveTaskSlot(out PersistenceRequestResult reserveResult))
            {
                return new ChunkPersistenceAsyncStartResult(reserveResult, [], []);
            }

            List<long> acceptedChunkKeyList = [];
            List<long> acceptedChunkKeys = [];
            List<long> rejectedChunkKeys = [];
            foreach (long chunkKey in taskGroup)
            {
                // 异步任务表是 chunk 级占位表，组内任意 chunk 冲突时只拒绝该 chunk，不阻断其他 chunk。
                if (_activeTaskTable.ContainsKey(chunkKey))
                {
                    GD.PushError($"[ChunkPersistenceThreadedWorker] chunk {new ChunkPosition(chunkKey)} 已存在异步任务占位，阻止该 chunk 进入本次任务组。");
                    rejectedChunkKeys.Add(chunkKey);
                    continue;
                }

                // TryAdd 处理 Contains 与写入之间的竞态，失败同样视为该 chunk 冲突。
                if (!_activeTaskTable.TryAdd(chunkKey, taskGroup.OperationType))
                {
                    GD.PushError($"[ChunkPersistenceThreadedWorker] chunk {new ChunkPosition(chunkKey)} 异步任务占位写入失败，阻止该 chunk 进入本次任务组。");
                    rejectedChunkKeys.Add(chunkKey);
                    continue;
                }

                acceptedChunkKeyList.Add(chunkKey);
                acceptedChunkKeys.Add(chunkKey);
            }

            if (acceptedChunkKeyList.Count == 0)
            {
                // 没有 chunk 成功进入任务时释放刚才申请的任务组槽位。
                ReleaseTaskSlot();
                return new ChunkPersistenceAsyncStartResult(PersistenceRequestResult.Success, acceptedChunkKeys, rejectedChunkKeys);
            }

            // 只把成功占位的 chunk 组成实际执行组，避免后台 IO 触碰冲突 chunk。
            RegionChunksPersistenceTaskGroup acceptedTaskGroup =
                new(taskGroup.RegionPosition, taskGroup.OperationType, acceptedChunkKeyList);
            _ = Task.Run(() => ExecuteTaskGroup(acceptedTaskGroup));
            return new ChunkPersistenceAsyncStartResult(PersistenceRequestResult.Success, acceptedChunkKeys, rejectedChunkKeys);
        }

        /// <summary>
        /// 尝试取出一个已完成异步任务。
        /// </summary>
        /// <param name="completedTask">取出的已完成任务。</param>
        /// <returns>完成队列中是否存在可回收任务。</returns>
        internal bool TryDequeueCompletedTask(out ChunkPersistenceCompletedTask completedTask)
        {
            return _completedTasks.TryDequeue(out completedTask);
        }

        /// <summary>
        /// 尝试申请一个异步任务组槽位。
        /// </summary>
        /// <param name="requestResult">槽位申请结果。</param>
        /// <returns>是否成功申请到槽位。</returns>
        private bool TryReserveTaskSlot(out PersistenceRequestResult requestResult)
        {
            lock (_activeTaskCountLock)
            {
                // 并发数达到上限时不创建新任务，并累计繁忙次数供缓存器决定是否阻塞兜底。
                if (_activeTaskCount >= MAX_CONCURRENT_TASKS)
                {
                    _busyCount++;
                    if (_busyCount >= BUSY_COUNT_THRESHOLD)
                    {
                        // 达到阈值后清空繁忙计数，本次由调用方走阻塞路径，避免永久饥饿。
                        _busyCount = 0;
                        requestResult = PersistenceRequestResult.PermanentFailure;
                        return false;
                    }

                    requestResult = PersistenceRequestResult.RetryLater;
                    return false;
                }

                // 成功拿到槽位说明异步 worker 恢复可用，繁忙计数重新开始。
                _busyCount = 0;
                _activeTaskCount++;
                requestResult = PersistenceRequestResult.Success;
                return true;
            }
        }

        /// <summary>
        /// 释放一个异步任务组槽位。
        /// </summary>
        private void ReleaseTaskSlot()
        {
            lock (_activeTaskCountLock)
            {
                // 使用 Math.Max 防御异常路径重复释放，避免计数跌到负数。
                _activeTaskCount = Math.Max(0, _activeTaskCount - 1);
            }
        }

        /// <summary>
        /// 在线程池中执行任务组，并把结果写入已完成队列。
        /// </summary>
        /// <param name="taskGroup">已经成功写入异步占位表的任务组。</param>
        private void ExecuteTaskGroup(RegionChunksPersistenceTaskGroup taskGroup)
        {
            try
            {
                // 后台线程只执行 IO 并投递结果，不直接修改缓存表，避免缓存表状态跨线程分散。
                _completedTasks.Enqueue(_ioWorker.Execute(taskGroup));
            }
            catch (Exception exception)
            {
                GD.PushError($"[ChunkPersistenceThreadedWorker] 异步任务组执行异常: {exception.Message}");
                List<ChunkPersistenceCompletedChunkResult> results = [];
                // 未预期异常按临时失败回传，缓存器会在主线程决定是否重新加入待办表。
                foreach (long chunkKey in taskGroup)
                {
                    results.Add(new ChunkPersistenceCompletedChunkResult(
                        chunkKey,
                        new ChunkPosition(chunkKey),
                        PersistenceRequestResult.RetryLater,
                        null,
                        0,
                        exception.Message));
                }

                _completedTasks.Enqueue(new ChunkPersistenceCompletedTask(taskGroup.OperationType, results));
            }
            finally
            {
                // 不论 IO 成功、失败还是异常，都必须释放任务组并发槽位。
                ReleaseTaskSlot();
            }
        }
    }
}
