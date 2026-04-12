using System.Collections;
using System.Collections.Generic;
using WorldWeaver.MapSystem.ChunkSystem.State;

namespace WorldWeaver.MapSystem.ChunkSystem
{
    /// <summary>
    /// 区块加载更新表。
    /// <para>该对象是每个 tick 临时重建的“区块坐标 -> 目标稳定状态”映射表。</para>
    /// <para>它不是形状，也不承担点序语义；它只服务于 ChunkManager 当前 tick 的状态驱动。</para>
    /// </summary>
    public sealed class ChunkLoadUpdateTable : IEnumerable<KeyValuePair<ChunkPosition, ChunkStateNode>>
    {
        // ================================================================================
        //                                  核心字段
        // ================================================================================

        /// <summary>
        /// 区块更新字典。
        /// </summary>
        private readonly Dictionary<ChunkPosition, ChunkStateNode> _updates = [];


        // ================================================================================
        //                                  语义化属性
        // ================================================================================

        /// <summary>
        /// 当前更新表中的条目数量。
        /// </summary>
        public int Count => _updates.Count;

        /// <summary>
        /// 当前更新表是否为空。
        /// </summary>
        public bool IsEmpty => _updates.Count == 0;


        // ================================================================================
        //                                  更新表操作方法
        // ================================================================================

        /// <summary>
        /// 清空当前更新表。
        /// </summary>
        public void Clear()
        {
            _updates.Clear();
        }

        /// <summary>
        /// 判断指定区块是否已存在于更新表中。
        /// </summary>
        public bool ContainsChunk(ChunkPosition chunkPosition)
        {
            return _updates.ContainsKey(chunkPosition);
        }

        /// <summary>
        /// 获取指定区块的目标稳定状态。
        /// </summary>
        public bool TryGetTargetStableNode(ChunkPosition chunkPosition, out ChunkStateNode targetStableNode)
        {
            return _updates.TryGetValue(chunkPosition, out targetStableNode);
        }

        /// <summary>
        /// 直接设置指定区块的目标稳定状态。
        /// </summary>
        public void SetTargetStableNode(ChunkPosition chunkPosition, ChunkStateNode targetStableNode)
        {
            _updates[chunkPosition] = targetStableNode;
        }

        /// <summary>
        /// 按“取最大枚举值”的规则设置指定区块的目标稳定状态。
        /// <para>若区块尚不存在，则直接写入；若已存在，则保留更大的状态节点。</para>
        /// </summary>
        public void SetTargetStableNodeByMax(ChunkPosition chunkPosition, ChunkStateNode targetStableNode)
        {
            if (_updates.TryGetValue(chunkPosition, out ChunkStateNode existingTargetStableNode) &&
                existingTargetStableNode >= targetStableNode)
            {
                return;
            }

            _updates[chunkPosition] = targetStableNode;
        }

        /// <summary>
        /// 移除指定区块的更新项。
        /// </summary>
        public bool RemoveChunk(ChunkPosition chunkPosition)
        {
            return _updates.Remove(chunkPosition);
        }


        // ================================================================================
        //                                  迭代方法
        // ================================================================================

        /// <summary>
        /// 获取更新表的泛型迭代器。
        /// </summary>
        public IEnumerator<KeyValuePair<ChunkPosition, ChunkStateNode>> GetEnumerator()
        {
            return _updates.GetEnumerator();
        }

        /// <summary>
        /// 获取更新表的非泛型迭代器。
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
