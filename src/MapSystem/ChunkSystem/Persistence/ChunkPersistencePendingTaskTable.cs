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
        /// 待办任务操作类型数量。
        /// <para>数组索引直接对应 <see cref="PersistenceOperationType"/> 的枚举值。</para>
        /// </summary>
        private const int OPERATION_TYPE_COUNT = 2;

        /// <summary>
        /// 将当前待办任务按操作类型与 region 归桶。
        /// <para>分桶结果是一次性计算结果，第一层数组索引对应操作类型，第二层列表保存该操作下的 region 桶。</para>
        /// </summary>
        /// <returns>按操作类型索引保存的 region 待办桶数组。</returns>
        internal List<(Vector2I RegionPosition, long[] ChunkKeys)>[] BuildRegionOperationBuckets()
        {
            // 构建期先使用 List<long>，因为待办表遍历时不知道每个 region 桶最终会收集多少 chunk。
            // 第一层数组固定为两个操作类型槽位：0 为 Read，1 为 Store。
            List<(Vector2I RegionPosition, List<long> ChunkKeys)>[] buildingBuckets =
            [
                [],
                []
            ];
            // 每个操作类型各自维护一个 region -> 桶索引的表。
            // 这样可以在保持首次出现顺序的同时，避免每加入一个 chunk 都线性查找 region 桶。
            Dictionary<Vector2I, int>[] bucketIndexTables =
            [
                new Dictionary<Vector2I, int>(),
                new Dictionary<Vector2I, int>()
            ];
            foreach (KeyValuePair<long, PersistenceOperationType> pair in _tasks)
            {
                // 待办表内部值直接是操作类型，因此可以把枚举值转换为第一层数组索引。
                // 如果未来枚举值变化或出现非法值，这里会直接报错并跳过，避免污染分桶结果。
                int operationIndex = (int)pair.Value;
                if (operationIndex < 0 || operationIndex >= OPERATION_TYPE_COUNT)
                {
                    GD.PushError($"[ChunkPersistencePendingTaskTable] 无效待办操作类型 {pair.Value}，chunk key: {pair.Key}。");
                    continue;
                }

                // 待办表只存 chunk key，分桶所需的 region 坐标由位运算直接恢复。
                Vector2I regionPosition = ChunkRegionPositionProcessor.GetRegionPosition(pair.Key);
                // 取到的是当前操作类型、当前 region 的构建期桶；没有则创建一个新桶。
                List<long> bucket = GetOrCreateBucket(
                    buildingBuckets[operationIndex],
                    bucketIndexTables[operationIndex],
                    regionPosition);

                // 待办表只负责归桶，真正的 Read / Store 数据判断留到缓存器分发前完成。
                bucket.Add(pair.Key);
            }

            // 调度阶段只需要按索引顺序读取 chunk key，因此最终桶内容转为更紧凑的 long[]。
            // 这一步也把构建期可变列表和本轮一次性调度视图分离开。
            return ConvertBuckets(buildingBuckets);
        }

        /// <summary>
        /// 获取或创建指定 region 坐标对应的 chunk key 分桶。
        /// </summary>
        /// <param name="buckets">当前操作类型下的 region 分桶列表。</param>
        /// <param name="bucketIndexTable">region 坐标到分桶索引的映射表。</param>
        /// <param name="regionPosition">待读取或创建的 region 坐标。</param>
        /// <returns>读取或创建出的 chunk key 列表。</returns>
        private static List<long> GetOrCreateBucket(
            List<(Vector2I RegionPosition, List<long> ChunkKeys)> buckets,
            Dictionary<Vector2I, int> bucketIndexTable,
            Vector2I regionPosition)
        {
            if (bucketIndexTable.TryGetValue(regionPosition, out int bucketIndex))
            {
                // region 已出现过，直接复用原桶，保持同 region 的 chunk 聚集在一起。
                return buckets[bucketIndex].ChunkKeys;
            }

            // region 第一次出现时追加到列表尾部。
            // 因为 Dictionary 枚举顺序在当前运行时保持插入顺序，桶列表也会沿用待办表遍历中的首次出现顺序。
            List<long> bucket = [];
            bucketIndexTable[regionPosition] = buckets.Count;
            buckets.Add((regionPosition, bucket));
            return bucket;
        }

        /// <summary>
        /// 将可追加的构建期分桶转换为一次性 long 数组分桶。
        /// </summary>
        /// <param name="buildingBuckets">构建期分桶列表。</param>
        /// <returns>按操作类型索引保存的 region 待办桶数组。</returns>
        private static List<(Vector2I RegionPosition, long[] ChunkKeys)>[] ConvertBuckets(
            List<(Vector2I RegionPosition, List<long> ChunkKeys)>[] buildingBuckets)
        {
            List<(Vector2I RegionPosition, long[] ChunkKeys)>[] buckets =
            [
                [],
                []
            ];
            for (int operationIndex = 0; operationIndex < OPERATION_TYPE_COUNT; operationIndex++)
            {
                // 保持每个操作类型内部 region 桶的构建顺序，只替换桶内集合类型。
                foreach ((Vector2I regionPosition, List<long> chunkKeys) in buildingBuckets[operationIndex])
                {
                    // 转为数组后，该分桶成为本轮分发的一次性结果；调度阶段用游标顺序消费。
                    buckets[operationIndex].Add((regionPosition, [.. chunkKeys]));
                }
            }

            return buckets;
        }
    }
}
