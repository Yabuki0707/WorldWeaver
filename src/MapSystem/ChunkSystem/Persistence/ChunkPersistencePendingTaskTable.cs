using System.Collections.Generic;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Persistence.Region;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// 持久化待办任务表。
    /// <para>该表负责保存 chunk key 与内部 IO 操作类型的映射，并提供按 region 与操作类型归桶的视图。</para>
    /// <para>该表不负责切分持久化任务组，也不负责向 worker 分发任务。</para>
    /// <para>待办表由缓存器主循环访问，后台 worker 不直接读写它，因此这里不做额外加锁。</para>
    /// </summary>
    public sealed class ChunkPersistencePendingTaskTable
    {
        // ================================================================================
        //                                  字段
        // ================================================================================

        /// <summary>
        /// 待办任务主字典。
        /// <para>键为 chunk key，值为该 chunk 当前唯一的待办操作类型。</para>
        /// </summary>
        private readonly Dictionary<long, PersistenceOperationType> _tasks = new();

        // ================================================================================
        //                                  基础操作
        // ================================================================================

        /// <summary>
        /// 查询指定 chunk 是否已有待办任务。
        /// </summary>
        /// <param name="chunkKey">需要查询的 chunk key。</param>
        /// <returns>待办表中是否存在该 chunk 的任务。</returns>
        public bool Contains(long chunkKey)
        {
            // 只读取当前主循环状态，避免调用方重复创建同一 chunk 的待办任务。
            return _tasks.ContainsKey(chunkKey);
        }

        /// <summary>
        /// 尝试读取指定 chunk 的待办操作类型。
        /// </summary>
        /// <param name="chunkKey">需要查询的 chunk key。</param>
        /// <param name="operationType">读取到的内部 IO 操作类型。</param>
        /// <returns>待办表中是否存在该 chunk 的任务。</returns>
        public bool TryGetOperation(long chunkKey, out PersistenceOperationType operationType)
        {
            // 待办表值直接就是操作类型，调用方无需再拆额外结构。
            return _tasks.TryGetValue(chunkKey, out operationType);
        }

        /// <summary>
        /// 写入或覆盖指定 chunk 的待办任务。
        /// </summary>
        /// <param name="chunkKey">需要写入待办任务的 chunk key。</param>
        /// <param name="operationType">待办任务的内部 IO 操作类型。</param>
        public void Set(long chunkKey, PersistenceOperationType operationType)
        {
            // 调用方已经完成可入队判断；这里直接覆盖，保持表本身职责单纯。
            _tasks[chunkKey] = operationType;
        }

        /// <summary>
        /// 移除指定 chunk 的待办任务。
        /// </summary>
        /// <param name="chunkKey">需要移除待办任务的 chunk key。</param>
        /// <returns>是否确实移除了一个待办任务。</returns>
        public bool Remove(long chunkKey)
        {
            // 移除通常发生在任务已分配、缓存命中或异常清理时。
            return _tasks.Remove(chunkKey);
        }

        /// <summary>
        /// 批量移除一组 chunk 的待办任务。
        /// </summary>
        /// <param name="chunkKeys">需要移除待办任务的 chunk key 集合。</param>
        public void RemoveMultiple(IEnumerable<long> chunkKeys)
        {
            foreach (long chunkKey in chunkKeys)
            {
                _tasks.Remove(chunkKey);
            }
        }

        // ================================================================================
        //                                  任务分桶
        // ================================================================================

        /// <summary>
        /// 将当前待办任务按 region 与操作类型归桶。
        /// <para>分桶结果是一次性计算结果，键为 region 坐标与操作类型，值为该桶内的 chunk key 列表。</para>
        /// </summary>
        /// <returns>按 region 坐标和操作类型收集出的待办任务分桶字典。</returns>
        internal Dictionary<(Vector2I RegionPosition, PersistenceOperationType OperationType), List<long>> BuildRegionOperationBuckets()
        {
            Dictionary<(Vector2I RegionPosition, PersistenceOperationType OperationType), List<long>> buckets = [];
            foreach (KeyValuePair<long, PersistenceOperationType> pair in _tasks)
            {
                // 待办表只存 chunk key，分桶所需的 region 坐标由位运算直接恢复。
                Vector2I regionPosition = ChunkRegionPositionProcessor.GetRegionPosition(pair.Key);
                (Vector2I RegionPosition, PersistenceOperationType OperationType) bucketKey = (regionPosition, pair.Value);
                List<long> bucket = GetOrCreateBucket(buckets, bucketKey);

                // 待办表只负责归桶，真正的 Read / Store 数据判断留到缓存器分发前完成。
                bucket.Add(pair.Key);
            }

            return buckets;
        }

        /// <summary>
        /// 获取或创建指定 region 坐标与操作类型对应的 chunk key 分桶。
        /// </summary>
        /// <param name="buckets">按 region 坐标与操作类型保存 chunk key 列表的分桶字典。</param>
        /// <param name="bucketKey">待读取或创建的分桶键。</param>
        /// <returns>读取或创建出的 chunk key 列表。</returns>
        private static List<long> GetOrCreateBucket(
            Dictionary<(Vector2I RegionPosition, PersistenceOperationType OperationType), List<long>> buckets,
            (Vector2I RegionPosition, PersistenceOperationType OperationType) bucketKey)
        {
            if (buckets.TryGetValue(bucketKey, out List<long> bucket))
            {
                return bucket;
            }

            bucket = [];
            buckets[bucketKey] = bucket;
            return bucket;
        }
    }
}
