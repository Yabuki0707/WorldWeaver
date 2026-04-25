using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;
using WorldWeaver.MapSystem.ChunkSystem.Persistence.Region;
using WorldWeaver.MapSystem.ChunkSystem.Persistence.Region.InfoOperator;
using WorldWeaver.MapSystem.LayerSystem;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// 阻塞型持久化器。
    /// <para>该类型只负责实际 region IO，不持有缓存表、待办任务表或异步任务表主状态。</para>
    /// </summary>
    public sealed class ChunkPersistenceBlockingWorker
    {
        /// <summary>
        /// 当前 worker 所属地图层。
        /// <para>当前只用于读取 chunk 尺寸校验上下文。</para>
        /// </summary>
        private readonly MapLayer _ownerLayer;

        /// <summary>
        /// region 文件根路径。
        /// <para>阻塞 worker 是实际 IO 基层，只有在打开、创建或加锁 region 文件前才把 region 坐标转换为路径。</para>
        /// </summary>
        private readonly string _rootPath;

        /// <summary>
        /// 当前缓存器的缓存表。
    /// <para>Store 任务组只携带 chunk key，实际写入时从这里按 key 读取当前缓存项。</para>
        /// </summary>
        private readonly ChunkPersistenceCacheTable _cacheTable;

        /// <summary>
        /// 创建阻塞型持久化器。
        /// </summary>
        /// <param name="ownerLayer">当前 worker 所属地图层。</param>
        /// <param name="cacheTable">当前缓存器的缓存表；Store 任务执行时用于读取储存对象与写入 tick。</param>
        public ChunkPersistenceBlockingWorker(MapLayer ownerLayer, ChunkPersistenceCacheTable cacheTable = null)
            : this(ownerLayer, ownerLayer.StorageFilePath, cacheTable)
        {
        }

        /// <summary>
        /// 创建阻塞型持久化器。
        /// </summary>
        /// <param name="ownerLayer">当前 worker 所属地图层。</param>
        /// <param name="rootPath">region 文件根路径。</param>
        /// <param name="cacheTable">当前缓存器的缓存表；Store 任务执行时用于读取储存对象与写入 tick。</param>
        public ChunkPersistenceBlockingWorker(
            MapLayer ownerLayer,
            string rootPath,
            ChunkPersistenceCacheTable cacheTable = null)
        {
            _ownerLayer = ownerLayer;
            _rootPath = rootPath;
            _cacheTable = cacheTable;
        }

        /// <summary>
        /// 执行一个持久化任务组。
        /// </summary>
        /// <param name="taskGroup">需要执行的持久化任务组。</param>
        /// <returns>任务组内每个 chunk 的完成结果。</returns>
        internal IReadOnlyList<ChunkPersistenceCompletedChunkResult> Execute(RegionChunksPersistenceTaskGroup taskGroup)
        {
            if (taskGroup.IsEmpty)
            {
                // 空任务组没有实际 IO，返回空读取结果供调用方安全忽略。
                return [];
            }

            // 阻塞 worker 只区分内部 IO 类型：Read 从 region 读取，Store 写回 region。
            return taskGroup.OperationType switch
            {
                PersistenceOperationType.Read => ExecuteReadGroup(taskGroup),
                PersistenceOperationType.Store => ExecuteStoreGroup(taskGroup),
                _ => CreateGroupFailure(taskGroup, "未知持久化操作类型。")
            };
        }

        /// <summary>
        /// 执行读取任务组。
        /// </summary>
        /// <param name="taskGroup">只包含同一 region 的读取任务组。</param>
        /// <returns>读取任务组的完成结果。</returns>
        private IReadOnlyList<ChunkPersistenceCompletedChunkResult> ExecuteReadGroup(RegionChunksPersistenceTaskGroup taskGroup)
        {
            List<ChunkPersistenceCompletedChunkResult> results = [];
            try
            {
                Vector2I regionPosition = taskGroup.RegionPosition;

                // 读取前仍经过 FileAccessor 的确保逻辑，统一处理 region 首次创建的并发保护。
                if (!ChunkRegionFileAccessor.TryEnsureRegionFileExists(_rootPath, regionPosition, out bool alreadyExists))
                {
                    return CreateGroupFailure(taskGroup, "无法确保 region 文件存在。");
                }

                if (!alreadyExists)
                {
                    // 调用时 region 原本不存在，说明该 region 没有任何历史 chunk 数据；所有读取都返回空成功。
                    foreach (long chunkKey in taskGroup)
                    {
                        results.Add(new ChunkPersistenceCompletedChunkResult(
                            chunkKey,
                            PersistenceRequestResult.Success,
                            null,
                            0,
                            null));
                    }

                    return results;
                }

                // region 已存在时打开读取器，逐 chunk 读取储存对象。
                using ChunkRegionReader regionReader = ChunkRegionReader.Open(_rootPath, regionPosition);
                if (regionReader == null)
                {
                    return CreateGroupFailure(taskGroup, "无法打开 region 读取器。");
                }

                foreach (long chunkKey in taskGroup)
                {
                    ChunkPosition chunkPosition = new(chunkKey);
                    // LoadChunkStorage 返回 false 表示读取流程本身失败；storage 为 null 则表示该 chunk 没有旧数据。
                    if (!regionReader.LoadChunkStorage(chunkPosition, out ChunkDataStorage storage))
                    {
                        results.Add(new ChunkPersistenceCompletedChunkResult(
                            chunkKey,
                            PersistenceRequestResult.PermanentFailure,
                            null,
                            0,
                            "读取 chunk 储存对象失败。"));
                        continue;
                    }

                    // 读取成功后必须校验尺寸与数组完整性，避免坏文件数据进入缓存表。
                    PersistenceRequestResult validateResult =
                        ChunkPersistenceStorageValidator.ValidateLoadedStorage(storage, _ownerLayer.ChunkSize);
                    results.Add(new ChunkPersistenceCompletedChunkResult(
                        chunkKey,
                        validateResult,
                        validateResult == PersistenceRequestResult.Success ? storage : null,
                        0,
                        validateResult == PersistenceRequestResult.Success ? null : "读取到的 chunk 储存对象校验失败。"));
                }
            }
            catch (IOException exception)
            {
                GD.PushError($"[ChunkPersistenceBlockingWorker] 读取任务组发生 IO 异常: {exception.Message}");
                return CreateGroupRetryLater(taskGroup, exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                GD.PushError($"[ChunkPersistenceBlockingWorker] 读取任务组访问被拒绝: {exception.Message}");
                return CreateGroupRetryLater(taskGroup, exception.Message);
            }
            catch (Exception exception)
            {
                GD.PushError($"[ChunkPersistenceBlockingWorker] 读取任务组发生异常: {exception.Message}");
                return CreateGroupRetryLater(taskGroup, exception.Message);
            }

            return results;
        }

        /// <summary>
        /// 执行储存任务组。
        /// </summary>
        /// <param name="taskGroup">只包含同一 region 的储存任务组。</param>
        /// <returns>储存任务组的完成结果。</returns>
        private IReadOnlyList<ChunkPersistenceCompletedChunkResult> ExecuteStoreGroup(RegionChunksPersistenceTaskGroup taskGroup)
        {
            try
            {
                if (_cacheTable == null)
                {
                    return CreateGroupFailure(taskGroup, "Store 任务缺少缓存表引用。");
                }

                Vector2I regionPosition = taskGroup.RegionPosition;

                // Store 必须确保 region 文件存在；具体创建互斥由 FileAccessor 内部负责。
                if (!ChunkRegionFileAccessor.TryEnsureRegionFileExists(_rootPath, regionPosition, out _))
                {
                    return CreateGroupFailure(taskGroup, "无法确保 region 文件存在。");
                }

                // 空闲分区锁仍以标准 region 文件路径为键；这里是进入底层 IO 前的路径转换点。
                if (!ChunkRegionFilePath.TryGetRegionFilePath(_rootPath, regionPosition, out string regionFilePath))
                {
                    return CreateGroupFailure(taskGroup, "无法生成 region 文件路径。");
                }

                bool regionLockEntered = false;
                try
                {
                    // 一个 Store 任务组内的 chunk 共享一次空闲分区链操作，必须持有同一 region 的空闲分区锁。
                    ChunkRegionFreePartitionLockTable.EnterRegionLock(regionFilePath);
                    regionLockEntered = true;

                    // writer 只负责具体 region 写入，外层已经保证 region 文件存在并持有空闲分区锁。
                    using ChunkRegionWriter regionWriter = ChunkRegionWriter.Open(_rootPath, regionPosition);
                    if (regionWriter == null)
                    {
                        return CreateGroupFailure(taskGroup, "无法打开 region 写入器。");
                    }

                    List<ChunkRegionWriter.ChunkStorageWriteItem> writeItems = [];
                    Dictionary<long, ulong> storedTicks = [];
                    foreach (long chunkKey in taskGroup)
                    {
                        ChunkPosition chunkPosition = new(chunkKey);
                        // Store 任务组只携带 key，真正写入前从缓存表读取当前缓存项。
                        if (!_cacheTable.TryGetStorageInfo(chunkKey, out ChunkDataStorage storage, out ulong storedTick))
                        {
                            return CreateGroupFailure(taskGroup, $"Store 任务组因 chunk {chunkPosition} 缺少缓存项而整体取消。");
                        }

                        if (storage == null)
                        {
                            return CreateGroupFailure(taskGroup, $"Store 任务组因 chunk {chunkPosition} 的储存对象为空而整体取消。");
                        }

                        storedTicks[chunkKey] = storedTick;
                        writeItems.Add(new ChunkRegionWriter.ChunkStorageWriteItem(chunkPosition, storage));
                    }

                    // 组储存内部会以组为单位处理空闲分区，再逐 chunk 完成链替换与旧链回收。
                    if (!regionWriter.StoreChunkStorageGroup(writeItems))
                    {
                        return CreateGroupFailure(taskGroup, "region 组储存失败。");
                    }

                    List<ChunkPersistenceCompletedChunkResult> results = [];
                    // Store 组执行成功时，组内每个 chunk 都返回成功，并携带本次写入使用的缓存 tick。
                    foreach (long chunkKey in taskGroup)
                    {
                        results.Add(new ChunkPersistenceCompletedChunkResult(
                            chunkKey,
                            PersistenceRequestResult.Success,
                            null,
                            storedTicks.TryGetValue(chunkKey, out ulong storedTick) ? storedTick : 0,
                            null));
                    }

                    return results;
                }
                finally
                {
                    if (regionLockEntered)
                    {
                        // 锁只覆盖本任务组的 region 写入窗口，避免长期占用空闲分区锁。
                        ChunkRegionFreePartitionLockTable.ExitRegionLock(regionFilePath);
                    }
                }
            }
            catch (IOException exception)
            {
                GD.PushError($"[ChunkPersistenceBlockingWorker] 储存任务组发生 IO 异常: {exception.Message}");
                return CreateGroupRetryLater(taskGroup, exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                GD.PushError($"[ChunkPersistenceBlockingWorker] 储存任务组访问被拒绝: {exception.Message}");
                return CreateGroupRetryLater(taskGroup, exception.Message);
            }
            catch (Exception exception)
            {
                GD.PushError($"[ChunkPersistenceBlockingWorker] 储存任务组发生异常: {exception.Message}");
                return CreateGroupRetryLater(taskGroup, exception.Message);
            }
        }

        /// <summary>
        /// 为整个任务组创建结构性失败结果。
        /// </summary>
        /// <param name="taskGroup">失败的任务组。</param>
        /// <param name="errorMessage">需要写入每个 chunk 结果的错误信息。</param>
        /// <returns>任务组内所有 chunk 都为结构性失败的完成结果。</returns>
        private static IReadOnlyList<ChunkPersistenceCompletedChunkResult> CreateGroupFailure(RegionChunksPersistenceTaskGroup taskGroup, string errorMessage)
        {
            List<ChunkPersistenceCompletedChunkResult> results = [];
            // 结构性失败不进入自动重试，避免坏路径、坏数据或逻辑错误造成无限循环。
            foreach (long chunkKey in taskGroup)
            {
                results.Add(new ChunkPersistenceCompletedChunkResult(
                    chunkKey,
                    PersistenceRequestResult.PermanentFailure,
                    null,
                    0,
                    errorMessage));
            }

            return results;
        }

        /// <summary>
        /// 为整个任务组创建临时失败结果。
        /// </summary>
        /// <param name="taskGroup">临时失败的任务组。</param>
        /// <param name="errorMessage">需要写入每个 chunk 结果的错误信息。</param>
        /// <returns>任务组内所有 chunk 都为稍后重试的完成结果。</returns>
        private static IReadOnlyList<ChunkPersistenceCompletedChunkResult> CreateGroupRetryLater(RegionChunksPersistenceTaskGroup taskGroup, string errorMessage)
        {
            List<ChunkPersistenceCompletedChunkResult> results = [];
            // IO 异常和访问波动按临时失败处理，由缓存器根据当前缓存状态决定是否重新入队。
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
    }
}
