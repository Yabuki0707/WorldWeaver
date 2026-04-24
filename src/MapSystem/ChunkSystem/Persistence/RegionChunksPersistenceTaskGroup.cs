using System;
using System.Collections;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// 同一 region、同一 IO 操作类型下的一次性持久化任务组。
    /// <para>任务组只保存 chunk key、region 坐标和操作类型，构造后不支持继续追加。</para>
    /// </summary>
    internal readonly struct RegionChunksPersistenceTaskGroup : IEnumerable<long>
    {
        /// <summary>
        /// 任务组内的 chunk key 数组。
        /// <para>该数组只在构造函数中创建，外部只能通过枚举器访问。</para>
        /// </summary>
        private readonly long[] _chunkKeys;

        /// <summary>
        /// 创建同一 region 的持久化任务组。
        /// </summary>
        /// <param name="regionPosition">任务组对应的 region 坐标。</param>
        /// <param name="operationType">任务组内部 IO 操作类型。</param>
        /// <param name="chunkKeys">需要复制进任务组的 chunk key 列表。</param>
        public RegionChunksPersistenceTaskGroup(
            Vector2I regionPosition,
            PersistenceOperationType operationType,
            IReadOnlyList<long> chunkKeys)
        {
            RegionPosition = regionPosition;
            OperationType = operationType;

            // 空输入直接使用共享空数组，保持任务组值类型默认状态也能安全枚举。
            if (chunkKeys == null || chunkKeys.Count == 0)
            {
                _chunkKeys = Array.Empty<long>();
                return;
            }

            _chunkKeys = [ .. chunkKeys];
        }

        /// <summary>
        /// 任务组对应的 region 坐标。
        /// </summary>
        public Vector2I RegionPosition { get; }

        /// <summary>
        /// 任务组内部 IO 操作类型。
        /// </summary>
        public PersistenceOperationType OperationType { get; }

        /// <summary>
        /// 任务组内的 chunk key 数量。
        /// </summary>
        public int Count => _chunkKeys?.Length ?? 0;

        /// <summary>
        /// 任务组是否为空。
        /// </summary>
        public bool IsEmpty => Count == 0;

        /// <summary>
        /// 获取任务组内 chunk key 的枚举器。
        /// </summary>
        /// <returns>按构造时复制顺序遍历任务组的枚举器。</returns>
        public IEnumerator<long> GetEnumerator()
        {
            if (_chunkKeys == null)
            {
                // default 结构体中的数组为 null，枚举时按空任务组处理。
                yield break;
            }

            // 只暴露顺序迭代，不暴露底层数组，避免构造后被外部修改。
            for (int index = 0; index < _chunkKeys.Length; index++)
            {
                yield return _chunkKeys[index];
            }
        }

        /// <summary>
        /// 获取非泛型枚举器。
        /// </summary>
        /// <returns>按构造时复制顺序遍历任务组的枚举器。</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 返回任务组的可读表示。
        /// <para>包含 region 坐标、操作类型与 chunk key 列表。</para>
        /// </summary>
        public override string ToString()
        {
            // chunk key 列表拼接为逗号分隔字符串，便于直接在日志中查看组内全部 chunk。
            string chunkKeysString = Count > 0
                ? string.Join(", ", _chunkKeys)
                : "(empty)";
            return $"RegionChunksPersistenceTaskGroup[region=({RegionPosition.X}, {RegionPosition.Y}), operation={OperationType}, count={Count}, chunkKeys=[{chunkKeysString}]]";
        }
    }
}
