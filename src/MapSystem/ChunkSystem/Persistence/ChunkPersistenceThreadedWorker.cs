using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;
using WorldWeaver.MapSystem.LayerSystem;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// 单个 chunk 的持久化完成结果。
    /// </summary>
    /// <param name="chunkKey">区块坐标 long key。</param>
    /// <param name="requestResult">该 chunk 的实际执行结果。</param>
    /// <param name="storage">Read 成功时返回的区块储存对象；Store 任务中为 null。</param>
    /// <param name="cacheStoredTick">Store 任务创建时记录的缓存写入 tick；Read 任务中为 0。</param>
    /// <param name="errorMessage">失败或异常时携带的错误信息。</param>
    internal readonly struct ChunkPersistenceCompletedChunkResult(
        long chunkKey,
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
    /// 异步型持久化器。
    /// <para>该类型在主线程维护异步任务表，后台 Task 只执行实际 IO。</para>
    /// </summary>
    public sealed class ChunkPersistenceThreadedWorker
    {
        // ================================================================================
        //                                  异步任务项
        // ================================================================================

        /// <summary>
        /// 异步任务表中的单个任务项。
        /// <para>同一任务组内的多个 chunk key 会指向同一个任务项。</para>
        /// </summary>
        internal sealed class AsyncPersistenceTask(RegionChunksPersistenceTaskGroup taskGroup)
        {
            /// <summary>
            /// 当前任务的内部 IO 操作类型。
            /// </summary>
            public PersistenceOperationType OperationType { get; } = taskGroup.OperationType;

            /// <summary>
            /// 当前后台 Task 正在执行的任务组。
            /// </summary>
            public RegionChunksPersistenceTaskGroup TaskGroup { get; } = taskGroup;

            /// <summary>
            /// 后台执行实际 IO 的 Task。
            /// </summary>
            public Task<bool> Task { get; private set; }

            /// <summary>
            /// 后台任务写入的 chunk 完成结果列表。
            /// </summary>
            public IReadOnlyList<ChunkPersistenceCompletedChunkResult> CompletedChunkResults;

            /// <summary>
            /// 启动后台执行 Task。
            /// </summary>
            /// <param name="task">后台执行 Task。</param>
            public void Start(Task<bool> task)
            {
                Task = task;
            }
        }

        // ================================================================================
        //                                  常量
        // ================================================================================

        /// <summary>
        /// 异步持久化任务最大并发数。
        /// <para>该值限制的是任务组数量，不是单个 chunk 数量。</para>
        /// </summary>
        private const int MAX_CONCURRENT_TASKS = 128;

        /// <summary>
        /// 异步 worker 连续繁忙阈值。
        /// <para>达到该阈值时，创建请求返回结构性失败，由缓存器改用阻塞型持久化器兜底。</para>
        /// </summary>
        private const int BUSY_COUNT_THRESHOLD = 128;

        // ================================================================================
        //                                  字段
        // ================================================================================

        /// <summary>
        /// 实际执行 region IO 的阻塞型 worker。
        /// <para>异步 worker 本身只负责任务占位和 Task 调度，不复制底层 IO 逻辑。</para>
        /// </summary>
        private readonly ChunkPersistenceBlockingWorker _ioWorker;

        /// <summary>
        /// 异步区块组任务列表。
        /// <para>该列表只在主线程访问，用于统计并发数量与查找已完成任务。</para>
        /// </summary>
        private readonly List<AsyncPersistenceTask> _activeChunkGroupTasks = [];

        /// <summary>
        /// 异步任务区块表。
        /// <para>该表只在主线程访问；键为区块坐标 long key，值为该 chunk 所属的异步任务项。</para>
        /// </summary>
        private readonly Dictionary<long, AsyncPersistenceTask> _activeTaskChunkTable = new();

        /// <summary>
        /// 连续申请异步任务但无可用槽位的次数。
        /// </summary>
        private int _busyCount;

        // ================================================================================
        //                                  构造
        // ================================================================================

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
            return _activeTaskChunkTable.ContainsKey(chunkKey);
        }

        /// <summary>
        /// 尝试读取指定 chunk 的异步任务操作类型。
        /// </summary>
        /// <param name="chunkKey">需要查询的区块坐标 long key。</param>
        /// <param name="operationType">读取到的异步任务操作类型。</param>
        /// <returns>异步任务表中是否存在该 chunk。</returns>
        public bool TryGetOperation(long chunkKey, out PersistenceOperationType operationType)
        {
            if (_activeTaskChunkTable.TryGetValue(chunkKey, out AsyncPersistenceTask task))
            {
                operationType = task.OperationType;
                return true;
            }

            operationType = default;
            return false;
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
            if (!_activeTaskChunkTable.TryGetValue(chunkKey, out AsyncPersistenceTask task))
            {
                return false;
            }

            if (task.OperationType != expectedOperationType)
            {
                return false;
            }

            return _activeTaskChunkTable.Remove(chunkKey);
        }

        // ================================================================================
        //                                  任务调度
        // ================================================================================

        /// <summary>
        /// 尝试创建异步任务组。
        /// <para>若当前没有可用异步槽位，则累计繁忙次数；繁忙次数达到阈值时返回结构性失败，由缓存器改用阻塞型持久化器兜底。</para>
        /// </summary>
        /// <param name="taskGroup">需要异步执行的任务组。</param>
        /// <returns>异步任务创建结果。</returns>
        internal PersistenceRequestResult TryStartTaskGroup(RegionChunksPersistenceTaskGroup taskGroup)
        {
            if (taskGroup.IsEmpty)
            {
                // 空任务组没有需要执行的 IO，视为创建成功。
                return PersistenceRequestResult.Success;
            }

            // 任务组级别先申请并发槽位，避免在无可用线程时提前污染异步任务表。
            PersistenceRequestResult reserveResult = CanReserveAsyncTaskGroupSlot();
            if (reserveResult != PersistenceRequestResult.Success)
            {
                return reserveResult;
            }
            // 存储当前任务组内成功占位的 chunk key的列表。
            List<long> acceptedChunkKeys = [];
            // 存储当前任务组内被拒绝的 chunk key的列表。
            List<long> rejectedChunkKeys = [];
            foreach (long chunkKey in taskGroup)
            {
                // 异步任务表是 chunk 级占位表，组内任意 chunk 冲突时只拒绝该 chunk，不阻断其他 chunk。
                if (_activeTaskChunkTable.ContainsKey(chunkKey))
                {
                    rejectedChunkKeys.Add(chunkKey);
                    continue;
                }

                acceptedChunkKeys.Add(chunkKey);
            }

            if (rejectedChunkKeys.Count > 0)
            {
                GD.PushError(
                    $"[ChunkPersistenceThreadedWorker] 任务组 {taskGroup} 中存在已被异步任务占用的 chunk，拒绝列表: {FormatChunkKeys(rejectedChunkKeys)}。");
            }

            if (acceptedChunkKeys.Count == 0)
            {
                GD.PushError($"[ChunkPersistenceThreadedWorker] 任务组 {taskGroup} 中所有 chunk 都已被异步任务占用，无法创建异步任务。");
                return PersistenceRequestResult.Success;
            }

            // 只把成功占位的 chunk 组成实际执行组，避免后台 IO 触碰冲突 chunk。
            RegionChunksPersistenceTaskGroup acceptedTaskGroup =
                new(taskGroup.RegionPosition, taskGroup.OperationType, acceptedChunkKeys);
            AsyncPersistenceTask task = new(acceptedTaskGroup);
            task.Start(Task.Run(() => ExecuteTaskGroup(acceptedTaskGroup, out task.CompletedChunkResults)));
            _activeChunkGroupTasks.Add(task);
            // 把成功占位的 chunk key 都添加到异步任务表中，后续遍历任务表即可发现完成任务。
            foreach (long chunkKey in acceptedChunkKeys)
            {
                // 同一任务组内的 chunk key 指向同一个异步任务项，后续遍历任务表即可发现完成任务。
                _activeTaskChunkTable.Add(chunkKey, task);
            }

            return PersistenceRequestResult.Success;
        }

        /// <summary>
        /// 取出当前已经完成的异步区块组任务。
        /// <para>该方法会从头遍历异步区块组任务列表，完成的任务进入结果列表，未完成的任务写入新的任务列表。</para>
        /// </summary>
        /// <returns>本次扫描中已经完成、可以回收的异步区块组任务列表。</returns>
        internal List<AsyncPersistenceTask> TakeOutCompletedTasks()
        {
            // 存储当前扫描到的完成任务列表。
            List<AsyncPersistenceTask> completedTasks = [];
            // 存储当前扫描到的未完成任务列表,只有在已经进入重建流程后，才需要追加到该新列表中。
            List<AsyncPersistenceTask> activeTasks = null;
            // 遍历异步区块组任务列表，扫描完成任务。
            for (int index = 0; index < _activeChunkGroupTasks.Count; index++)
            {
                AsyncPersistenceTask task = _activeChunkGroupTasks[index];
                if (task.Task.IsCompleted)
                {
                    // 首次遇到完成任务时才创建新列表，并复制索引 0 开始、数量为 index 的前缀任务。
                    activeTasks ??= _activeChunkGroupTasks.GetRange(0, index);

                    completedTasks.Add(task);
                    RemoveTaskChunkOccupancy(task);
                    continue;
                }

                // 只有已经进入重建流程后，才需要继续把未完成任务追加到新列表。
                if (activeTasks != null)
                {
                    activeTasks.Add(task);
                }
            }
            // 动态地把未完成任务列表写入异步区块组任务列表。
            if (activeTasks != null)
            {
                _activeChunkGroupTasks.Clear();
                _activeChunkGroupTasks.AddRange(activeTasks);
            }

            return completedTasks;
        }

        /// <summary>
        /// 判断是否可以申请一个异步区块组任务槽位。
        /// <para>该方法同时负责维护异步任务拥堵计数，并决定是否触发阻塞兜底。</para>
        /// </summary>
        /// <returns>是否可以申请到槽位，或需要走阻塞兜底路径。</returns>
        private PersistenceRequestResult CanReserveAsyncTaskGroupSlot()
        {
            // 异步区块组任务列表只在主线程维护，直接按任务项数量作为并发槽位状态。
            if (_activeChunkGroupTasks.Count >= MAX_CONCURRENT_TASKS)
            {
                _busyCount++;
                if (_busyCount >= BUSY_COUNT_THRESHOLD)
                {
                    // 达到阈值后清空繁忙计数，本次由调用方走阻塞路径，避免永久饥饿。
                    _busyCount = 0;
                    GD.PushError($"[ChunkPersistenceThreadedWorker] 异步持久化任务连续拥堵达到阈值 {BUSY_COUNT_THRESHOLD}，当前运行任务数: {_activeChunkGroupTasks.Count}。");
                    return PersistenceRequestResult.PermanentFailure;
                }

                return PersistenceRequestResult.RetryLater;
            }

            // 成功拿到槽位说明异步 worker 恢复可用，繁忙计数重新开始。
            _busyCount = 0;
            return PersistenceRequestResult.Success;
        }

        /// <summary>
        /// 在线程池中执行任务组，并返回完成结果。
        /// </summary>
        /// <param name="taskGroup">已经成功写入异步占位表的任务组。</param>
        /// <returns>任务组执行完成结果。</returns>
        private bool ExecuteTaskGroup(
            RegionChunksPersistenceTaskGroup taskGroup,
            out IReadOnlyList<ChunkPersistenceCompletedChunkResult> chunkResults)
        {
            try
            {
                // 后台线程只执行 IO 并返回结果，不直接修改缓存表或异步任务表。
                chunkResults = _ioWorker.Execute(taskGroup);
                return true;
            }
            catch (Exception exception)
            {
                GD.PushError($"[ChunkPersistenceThreadedWorker] 异步任务组执行异常: {exception.Message}");
                chunkResults = CreateRetryLaterResults(taskGroup, exception.Message);
                return false;
            }
        }

        /// <summary>
        /// 移除指定异步区块组任务在区块表中的占位。
        /// </summary>
        /// <param name="task">已经完成并取出的异步区块组任务。</param>
        private void RemoveTaskChunkOccupancy(AsyncPersistenceTask task)
        {
            foreach (long chunkKey in task.TaskGroup)
            {
                if (_activeTaskChunkTable.TryGetValue(chunkKey, out AsyncPersistenceTask activeTask) &&
                    ReferenceEquals(activeTask, task))
                {
                    _activeTaskChunkTable.Remove(chunkKey);
                }
            }
        }

        /// <summary>
        /// 为异常结束的任务组创建临时失败结果。
        /// </summary>
        /// <param name="taskGroup">异常结束的任务组。</param>
        /// <param name="errorMessage">异常或取消信息。</param>
        /// <returns>可交给缓存器回收的临时失败 chunk 结果列表。</returns>
        private static IReadOnlyList<ChunkPersistenceCompletedChunkResult> CreateRetryLaterResults(
            RegionChunksPersistenceTaskGroup taskGroup,
            string errorMessage)
        {
            List<ChunkPersistenceCompletedChunkResult> results = [];
            // 未预期异常按临时失败回传，缓存器会在主线程决定是否重新加入待办表。
            foreach (long chunkKey in taskGroup)
            {
                results.Add(new ChunkPersistenceCompletedChunkResult(
                    chunkKey,
                    PersistenceRequestResult.RetryLater,
                    null,
                    0,
                    errorMessage));
            }

            return results;
        }

        /// <summary>
        /// 将 chunk key 列表转换为可读坐标文本。
        /// </summary>
        /// <param name="chunkKeys">需要输出的 chunk key 列表。</param>
        /// <returns>逗号分隔的 chunk 坐标文本。</returns>
        private static string FormatChunkKeys(IReadOnlyList<long> chunkKeys)
        {
            List<string> chunkPositions = [];
            foreach (long chunkKey in chunkKeys)
            {
                chunkPositions.Add(new ChunkPosition(chunkKey).ToString());
            }

            return string.Join(", ", chunkPositions);
        }
    }
}
