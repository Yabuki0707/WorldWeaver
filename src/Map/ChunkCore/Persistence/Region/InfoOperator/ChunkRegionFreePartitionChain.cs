using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace WorldWeaver.Map.ChunkCore.Persistence.Region.InfoOperator
{
    /// <summary>
    /// Region 文件内的空闲分区链。
    /// <para>构造时获取文件锁并从头数据区读取空闲分区状态；Dispose 时写回头状态、冲刷文件并释放锁。</para>
    /// <para>内部以 <see cref="FreePartitionState"/> 作为内存快照，HeadIndex / Count 均从该状态取值。</para>
    /// <para>取走分区时采用"先遍历收集、确认无环后再更新头状态"的策略，避免中途失败已修改 <see cref="_state"/> 无法回滚。</para>
    /// </summary>
    public sealed class ChunkRegionFreePartitionChain : IDisposable
    {
        /// <summary>
        /// 当前操作的 region 文件流，构造时注入，整个生命周期内只读。
        /// </summary>
        private readonly FileStream _stream;

        /// <summary>
        /// 标准化后的 region 文件路径，用于获取文件锁与日志上下文。
        /// </summary>
        private readonly string _regionFilePath;

        /// <summary>
        /// 文件级互斥锁句柄，构造时获取，Dispose 时释放。
        /// <para>锁覆盖整个空闲链实例的生命周期，确保内存快照与文件头状态在操作期间不被其他线程修改。</para>
        /// </summary>
        private readonly IDisposable _lockHandle;

        /// <summary>
        /// 空闲分区头状态的内存快照。所有对 HeadIndex / Count 的读写均通过该字段完成。
        /// </summary>
        private FreePartitionState _state;

        // ================================================================================
        //                              属性
        // ================================================================================

        /// <summary>
        /// 空闲分区链头索引。
        /// </summary>
        public uint HeadIndex => _state.HeadFreePartitionIndex;

        /// <summary>
        /// 空闲分区数量。
        /// </summary>
        public uint Count => _state.FreePartitionCount;

        // ================================================================================
        //                              操作符
        // ================================================================================

        /// <summary>
        /// 注册一组空闲分区到头链，返回是否全部成功。
        /// <para>等价于 <see cref="RegisterHeadPartitions"/>。</para>
        /// </summary>
        public static bool operator +(ChunkRegionFreePartitionChain chain, uint[] indices)
        {
            return chain.RegisterHeadPartitions(indices);
        }

        /// <summary>
        /// 注册单个空闲分区到头链，返回是否成功。
        /// <para>等价于 <see cref="RegisterHeadPartition"/>。</para>
        /// </summary>
        public static bool operator +(ChunkRegionFreePartitionChain chain, uint index)
        {
            return chain.RegisterHeadPartition(index);
        }

        /// <summary>
        /// 取走指定数量的空闲分区，返回索引数组。
        /// <para>等价于 <see cref="TakeOutFreePartitions"/>。不足或异常时返回 null。</para>
        /// </summary>
        public static uint[] operator -(ChunkRegionFreePartitionChain chain, int takeCount)
        {
            return chain.TakeOutFreePartitions(takeCount);
        }

        // ================================================================================
        //                              构造与销毁
        // ================================================================================

        /// <summary>
        /// 构造空闲分区链实例。
        /// <para>流程：获取 region 空闲分区锁 → 读取文件中的空闲分区头状态 → 校验状态合法性 → 缓存到 <see cref="_state"/>。</para>
        /// <para>若读取失败或状态非法则抛出异常，由上层决定是否终止当前操作。</para>
        /// </summary>
        /// <param name="stream">已打开的 region 文件流，必须可读写且已通过格式校验。</param>
        public ChunkRegionFreePartitionChain(FileStream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));

            _regionFilePath = stream.Name;
            if (string.IsNullOrWhiteSpace(_regionFilePath))
            {
                throw new ArgumentException(
                    "[ChunkRegionFreePartitionChain] 无法从 stream 获取有效的 region 文件路径。");
            }

            // 构造即加锁，确保该实例整个生命周期内空闲分区头不被并发修改。
            _lockHandle = ChunkRegionFreePartitionLockTable.Lock(_regionFilePath);

            // 读取文件头中的空闲分区状态（HeadFreePartitionIndex + FreePartitionCount）。
            if (!ChunkRegionHeaderOperator.TryReadFreePartitionState(stream, out FreePartitionState state))
            {
                throw new InvalidOperationException(
                    "[ChunkRegionFreePartitionChain] 读取空闲分区头状态失败。");
            }

            // 校验读取到的状态与当前文件分区总量是否一致。
            if (!IsStateValid(stream, state))
            {
                throw new InvalidOperationException(
                    "[ChunkRegionFreePartitionChain] 空闲分区头状态非法。");
            }

            _state = state;
        }

        /// <summary>
        /// 销毁实例：将当前内存中的头状态写回文件 → 强制冲刷 → 释放文件锁。
        /// <para>冲刷失败不会阻止锁释放（锁泄漏比数据丢失更严重）。</para>
        /// </summary>
        public void Dispose()
        {
            try
            {
                // 写回文件头中的空闲分区状态（HeadFreePartitionIndex + FreePartitionCount）。
                ChunkRegionHeaderOperator.WriteFreePartitionState(_stream, _state);
                // 确保数据落盘后再释放锁，避免其他线程读到半写状态。
                _stream.Flush(true);
            }
            finally
            {
                // 无论写回是否成功，都必须释放锁，防止死锁扩散。
                _lockHandle.Dispose();
            }
        }

        // ================================================================================
        //                              取走空闲分区
        // ================================================================================

        /// <summary>
        /// 从空闲链头取走指定数量的分区，返回索引数组。
        /// <para>策略：先遍历链收集目标索引，用 HashSet 防环；遍历全部成功后才更新 <see cref="_state"/>，
        /// 避免中途失败后内存快照已部分修改无法回滚。</para>
        /// </summary>
        /// <param name="takeCount">需要取走的分区数量。</param>
        /// <returns>成功时返回长度为 takeCount 的索引数组；失败时返回 null。</returns>
        public uint[] TakeOutFreePartitions(int takeCount)
        {
            if (takeCount <= 0)
            {
                GD.PushError(
                    $"[ChunkRegionFreePartitionChain] TakeOutFreePartitions: takeCount={takeCount} 非法。");
                return null;
            }

            if (_state.FreePartitionCount < (uint)takeCount)
            {
                GD.PushError(
                    $"[ChunkRegionFreePartitionChain] TakeOutFreePartitions: 空闲分区不足（需要 {takeCount}，当前 {_state.FreePartitionCount}）。");
                return null;
            }

            // —————— 遍历链，收集索引，校验环与边界 ——————
            uint[] resultIndices = new uint[takeCount];
            // 防环：记录已访问的分区索引，发现重复即报环。
            HashSet<uint> visitedIndices = new(takeCount);
            uint current = _state.HeadFreePartitionIndex;
            for (int i = 0; i < takeCount; i++)
            {
                // 索引越界检查（非首轮时也是对上一轮 next 的后验）。
                if (!ChunkRegionPartitionOperator.IsPartitionIndexInRange(_stream, current))
                {
                    GD.PushError(
                        $"[ChunkRegionFreePartitionChain] TakeOutFreePartitions: 分区索引 {current} 越界。");
                    return null;
                }

                // 环检测：同一条链内不应出现重复节点。
                if (!visitedIndices.Add(current))
                {
                    GD.PushError(
                        "[ChunkRegionFreePartitionChain] TakeOutFreePartitions: 空闲分区链存在循环或重复节点。");
                    return null;
                }

                // 统一读取原始 next——若下一轮仍在本循环内，越界会被 IsPartitionIndexInRange 截住。
                if (!ChunkRegionPartitionOperator.TryReadRawPartitionNextIndex(
                        _stream, current, out uint next))
                {
                    return null;
                }

                resultIndices[i] = current;
                current = next;
            }

            // —————— 遍历成功：更新内存快照 ——————
            uint newFreePartitionCount = _state.FreePartitionCount - (uint)takeCount;
            // 取走后若非空链——头数据记录的 FreePartitionCount 与实际链长不一致即为数据异常。
            if (newFreePartitionCount > 0)
            {
                if (current == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
                {
                    GD.PushError(
                        "[ChunkRegionFreePartitionChain] TakeOutFreePartitions: 空闲分区头数据异常——FreePartitionCount 未归零但实际链已耗尽。");
                    return null;
                }

                if (!ChunkRegionPartitionOperator.IsPartitionIndexInRange(_stream, current))
                {
                    GD.PushError(
                        $"[ChunkRegionFreePartitionChain] TakeOutFreePartitions: 空闲分区头数据异常——下一链头 {current} 越界，FreePartitionCount 与实际链结构不一致。");
                    return null;
                }
            }

            _state = new FreePartitionState(current, newFreePartitionCount);
            return resultIndices;
        }

        // ================================================================================
        //                              注册空闲分区
        // ================================================================================

        /// <summary>
        /// 注册单个分区到空闲链头部，返回是否成功。
        /// <para>流程：校验分区索引在已分配范围内 → 写入该分区的 next 指针（指向当前链头或哨兵）→ 更新 <see cref="_state"/>。</para>
        /// </summary>
        /// <param name="partitionIndex">要注册到空闲链头部的分区索引。</param>
        /// <returns>注册是否成功。</returns>
        public bool RegisterHeadPartition(uint partitionIndex)
        {
            // 分区索引必须处于当前文件的已分配范围内。
            if (!ChunkRegionPartitionOperator.IsPartitionIndexInRange(_stream, partitionIndex))
            {
                GD.PushError(
                    $"[ChunkRegionFreePartitionChain] RegisterHeadPartition: 分区索引 {partitionIndex} 越界。");
                return false;
            }

            // 确定该分区的 next 指针：当前链为空则为哨兵，否则指向现有链头。
            uint next = _state.FreePartitionCount == 0
                ? ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL
                : _state.HeadFreePartitionIndex;

            // 把 next 指针写入该分区的 next 字段。
            Span<byte> nextBytes = stackalloc byte[ChunkRegionFileLayout.PARTITION_NEXT_INDEX_SIZE];
            BinaryPrimitives.WriteUInt32LittleEndian(nextBytes, next);

            if (!ChunkRegionFileAccessor.TryWriteBytes(
                    _stream,
                    ChunkRegionFileLayout.GetPartitionNextOffsetInFile(partitionIndex),
                    nextBytes))
            {
                GD.PushError(
                    $"[ChunkRegionFreePartitionChain] RegisterHeadPartition: 写入分区 {partitionIndex} 的 next 指针失败。");
                return false;
            }

            // 更新内存快照：新链头为刚注册的分区，数量加一。
            _state = new FreePartitionState(partitionIndex, _state.FreePartitionCount + 1);
            return true;
        }

        /// <summary>
        /// 注册一组空闲分区到链头部，返回是否全部成功。
        /// <para>流程：将输入降序排序（保证结果链中索引递增），然后逐个调用 <see cref="RegisterHeadPartition"/> 注册。
        /// 任一失败则立即返回 false，已注册的分区不会回滚。</para>
        /// </summary>
        /// <param name="indices">要注册的分区索引只读跨度。</param>
        /// <returns>是否全部注册成功。</returns>
        public bool RegisterHeadPartitions(ReadOnlySpan<uint> indices)
        {
            if (indices.IsEmpty)
            {
                return true;
            }

            // 降序排序：先插入大索引，再插入小索引，保证最终链的遍历顺序递增。
            Span<uint> sorted = stackalloc uint[indices.Length];
            indices.CopyTo(sorted);
            sorted.Sort((a, b) => b.CompareTo(a));

            foreach (uint index in sorted)
            {
                if (!RegisterHeadPartition(index))
                {
                    return false;
                }
            }

            return true;
        }

        // ================================================================================
        //                              校验（静态）
        // ================================================================================

        /// <summary>
        /// 校验空闲分区头状态是否与当前文件的分区总数一致。
        /// <para>哨兵索引必须对应零数量；非零数量时链头必须处于已分配范围内。</para>
        /// </summary>
        /// <param name="stream">当前 region 文件流。</param>
        /// <param name="state">待校验的空闲分区头状态。</param>
        /// <returns>状态是否合法。</returns>
        private static bool IsStateValid(FileStream stream, FreePartitionState state)
        {
            uint allocatedCount = ChunkRegionPartitionOperator.GetAllocatedPartitionCount(stream);

            // 哨兵索引 + 零数量 = 空链，合法。
            if (state.HeadFreePartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
            {
                return state.FreePartitionCount == 0;
            }

            // 非哨兵索引但数量为零，不可能。
            if (state.FreePartitionCount == 0)
            {
                return false;
            }

            // 空闲分区数量不可能超过已分配总量。
            if (state.FreePartitionCount > allocatedCount)
            {
                return false;
            }

            // 链头索引必须在已分配范围内。
            return state.HeadFreePartitionIndex < allocatedCount;
        }
    }
}
