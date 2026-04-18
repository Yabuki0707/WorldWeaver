using System;
using System.Collections;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.IO;
using Godot;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// ChunkRegion 空闲分区链。
    /// <para>该类型负责维护当前 region 文件中的空闲分区头状态，并基于分区 next 指针执行活链读取、取头、头插与头状态写回。</para>
    /// </summary>
    public sealed class ChunkRegionFreePartitionChain : IEnumerable<uint>, IDisposable
    {
        /// <summary>
        /// 当前空闲分区链绑定的 region 文件流。
        /// </summary>
        private readonly FileStream _stream;

        /// <summary>
        /// 当前头状态是否已同步写回文件。
        /// </summary>
        public bool IsFlushed { get; private set; }

        /// <summary>
        /// 当前空闲分区链在内存中的头状态。
        /// </summary>
        public ChunkRegionHeaderOperator.FreePartitionState FreePartitionState { get; private set; }

        /// <summary>
        /// 当前空闲分区数量。
        /// </summary>
        public uint FreePartitionCount => FreePartitionState.FreePartitionCount;

        /// <summary>
        /// 当前空闲链头分区索引。
        /// </summary>
        public uint HeadPartitionIndex => FreePartitionState.HeadFreePartitionIndex;

        /// <summary>
        /// 通过读取文件头状态创建空闲分区链实例。
        /// </summary>
        public static ChunkRegionFreePartitionChain Create(FileStream stream)
        {
            if (stream == null)
            {
                GD.PushError("[ChunkRegionFreePartitionChain] Create: stream 不能为空。");
                return null;
            }

            ChunkRegionFreePartitionChain chain = new(stream);
            if (!IsFreePartitionStateValid(stream, chain.FreePartitionState))
            {
                GD.PushError("[ChunkRegionFreePartitionChain] Create: 读取到的空闲分区头状态非法。");
                return null;
            }

            return chain;
        }

        /// <summary>
        /// 仅允许通过 Create 创建实例。
        /// </summary>
        private ChunkRegionFreePartitionChain(FileStream stream)
        {
            _stream = stream;
            FreePartitionState = ChunkRegionHeaderOperator.ReadFreePartitionState(stream);
            IsFlushed = true;
        }

        /// <summary>
        /// 校验指定空闲分区头状态是否满足最基本的头字段约束。
        /// </summary>
        public static bool IsFreePartitionStateValid(
            FileStream stream,
            ChunkRegionHeaderOperator.FreePartitionState freePartitionState)
        {
            uint allocatedPartitionCount = ChunkRegionPartitionOperator.GetAllocatedPartitionCount(stream);
            if (freePartitionState.HeadFreePartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
            {
                return freePartitionState.FreePartitionCount == 0;
            }

            if (freePartitionState.FreePartitionCount == 0)
            {
                return false;
            }

            if (freePartitionState.FreePartitionCount > allocatedPartitionCount)
            {
                return false;
            }

            return freePartitionState.HeadFreePartitionIndex < allocatedPartitionCount;
        }

        /// <summary>
        /// 使用快慢指针法判断指定空闲分区链是否为环。
        /// <para>该方法只回答“是否成环”，不负责校验头状态记录的数量是否与整链长度完全一致。</para>
        /// </summary>
        public static bool IsFreePartitionListChainCyclic(
            FileStream stream,
            ChunkRegionHeaderOperator.FreePartitionState freePartitionState)
        {
            if (!IsFreePartitionStateValid(stream, freePartitionState)) return false;
            if (freePartitionState.FreePartitionCount == 0) return false;

            uint slowPartitionIndex = freePartitionState.HeadFreePartitionIndex;
            uint fastPartitionIndex = freePartitionState.HeadFreePartitionIndex;
            while (true)
            {
                // 慢指针每轮前进一步，用于与快指针比较是否相遇。
                if (!TryReadNextPartitionIndex(stream, slowPartitionIndex, out slowPartitionIndex)) return false;

                // 快指针第一次前进；若此时已经到达链尾，说明当前链可正常结束而非成环。
                if (!TryReadNextPartitionIndex(stream, fastPartitionIndex, out fastPartitionIndex)) return false;
                if (fastPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL) return false;

                // 快指针第二次前进；依然能走到链尾则说明不是环，若与慢指针相遇则说明成环。
                if (!TryReadNextPartitionIndex(stream, fastPartitionIndex, out fastPartitionIndex)) return false;
                if (fastPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL) return false;
                if (slowPartitionIndex == fastPartitionIndex) return true;
            }
        }

        /// <summary>
        /// 获取指定数量的空闲分区列表，并同步更新内存中的头状态。
        /// <para>该方法只在成功时修改内存状态；若链成环、越界或数量不足，则返回 null。</para>
        /// </summary>
        public uint[] GetFreePartitionList(int takePartitionCount)
        {
            if (takePartitionCount <= 0)
            {
                GD.PushError($"[ChunkRegionFreePartitionChain] GetFreePartitionList: takePartitionCount={takePartitionCount} 非法。");
                return null;
            }

            if (!IsFreePartitionStateValid(_stream, FreePartitionState))
            {
                GD.PushError("[ChunkRegionFreePartitionChain] GetFreePartitionList: 当前空闲分区头状态非法。");
                return null;
            }

            if (FreePartitionCount < (uint)takePartitionCount)
            {
                GD.PushError(
                    $"[ChunkRegionFreePartitionChain] GetFreePartitionList: 当前空闲分区数量 {FreePartitionCount} 不足以提供 {takePartitionCount} 个分区。");
                return null;
            }

            ChunkRegionHeaderOperator.FreePartitionState oldFreePartitionState = FreePartitionState;
            bool oldIsFlushed = IsFlushed;
            uint[] freePartitionList = new uint[takePartitionCount];
            HashSet<uint> visitedPartitionIndices = new(takePartitionCount);
            for (int i = 0; i < takePartitionCount; i++)
            {
                uint freePartitionIndex = TakeHeadPartition();
                if (freePartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
                {
                    FreePartitionState = oldFreePartitionState;
                    IsFlushed = oldIsFlushed;
                    return null;
                }

                if (!visitedPartitionIndices.Add(freePartitionIndex))
                {
                    GD.PushError("[ChunkRegionFreePartitionChain] GetFreePartitionList: 空闲分区链存在循环或重复节点。");
                    FreePartitionState = oldFreePartitionState;
                    IsFlushed = oldIsFlushed;
                    return null;
                }

                freePartitionList[i] = freePartitionIndex;
            }

            return freePartitionList;
        }

        /// <summary>
        /// 从空闲分区链头取出一个空闲分区。
        /// </summary>
        public uint TakeHeadPartition()
        {
            if (!IsFreePartitionStateValid(_stream, FreePartitionState))
            {
                GD.PushError("[ChunkRegionFreePartitionChain] TakeHeadPartition: 当前空闲分区头状态非法。");
                return ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL;
            }

            if (FreePartitionCount == 0)
            {
                GD.PushError("[ChunkRegionFreePartitionChain] TakeHeadPartition: 当前没有可取出的空闲分区。");
                return ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL;
            }

            uint currentHeadPartitionIndex = HeadPartitionIndex;
            if (!ChunkRegionPartitionOperator.IsPartitionIndexInRange(_stream, currentHeadPartitionIndex))
            {
                GD.PushError(
                    $"[ChunkRegionFreePartitionChain] TakeHeadPartition: 头分区索引 {currentHeadPartitionIndex} 超出已分配范围。");
                return ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL;
            }

            if (FreePartitionCount == 1)
            {
                uint lastPartitionNextIndex = ChunkRegionPartitionOperator.ReadPartitionNextIndex(_stream, currentHeadPartitionIndex);
                if (lastPartitionNextIndex != ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
                {
                    GD.PushError("[ChunkRegionFreePartitionChain] TakeHeadPartition: 单节点空闲链尾部未写入哨兵值。");
                    return ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL;
                }

                FreePartitionState = new ChunkRegionHeaderOperator.FreePartitionState(
                    ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL,
                    0);
                IsFlushed = false;
                return currentHeadPartitionIndex;
            }

            uint nextHeadPartitionIndex = ChunkRegionPartitionOperator.ReadPartitionNextIndex(_stream, currentHeadPartitionIndex);
            if (nextHeadPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
            {
                GD.PushError("[ChunkRegionFreePartitionChain] TakeHeadPartition: 空闲分区链在预期数量耗尽前提前结束。");
                return ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL;
            }

            if (!ChunkRegionPartitionOperator.IsPartitionIndexInRange(_stream, nextHeadPartitionIndex))
            {
                GD.PushError(
                    $"[ChunkRegionFreePartitionChain] TakeHeadPartition: 下一头分区索引 {nextHeadPartitionIndex} 超出已分配范围。");
                return ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL;
            }

            FreePartitionState = new ChunkRegionHeaderOperator.FreePartitionState(
                nextHeadPartitionIndex,
                FreePartitionCount - 1);
            IsFlushed = false;
            return currentHeadPartitionIndex;
        }

        /// <summary>
        /// 将指定分区注册到当前空闲分区链头部。
        /// </summary>
        public ChunkRegionHeaderOperator.FreePartitionState RegisterHeadPartition(uint partitionIndex)
        {
            ChunkRegionHeaderOperator.FreePartitionState newFreePartitionState =
                RegisterHeadPartition(_stream, FreePartitionState, partitionIndex);
            if (newFreePartitionState.HeadFreePartitionIndex != FreePartitionState.HeadFreePartitionIndex ||
                newFreePartitionState.FreePartitionCount != FreePartitionState.FreePartitionCount)
            {
                FreePartitionState = newFreePartitionState;
                IsFlushed = false;
            }

            return FreePartitionState;
        }

        /// <summary>
        /// 将指定分区注册到指定空闲分区头部状态。
        /// </summary>
        public static ChunkRegionHeaderOperator.FreePartitionState RegisterHeadPartition(
            FileStream stream,
            ChunkRegionHeaderOperator.FreePartitionState freePartitionState,
            uint partitionIndex)
        {
            if (!IsFreePartitionStateValid(stream, freePartitionState))
            {
                GD.PushError("[ChunkRegionFreePartitionChain] RegisterHeadPartition: 当前空闲分区头状态非法。");
                return freePartitionState;
            }

            if (!ChunkRegionPartitionOperator.IsPartitionIndexInRange(stream, partitionIndex))
            {
                GD.PushError(
                    $"[ChunkRegionFreePartitionChain] RegisterHeadPartition: 分区索引 {partitionIndex} 超出已分配范围。");
                return freePartitionState;
            }

            if (freePartitionState.FreePartitionCount > 0 &&
                !ChunkRegionPartitionOperator.IsPartitionIndexInRange(stream, freePartitionState.HeadFreePartitionIndex))
            {
                GD.PushError(
                    $"[ChunkRegionFreePartitionChain] RegisterHeadPartition: 当前头分区索引 {freePartitionState.HeadFreePartitionIndex} 超出已分配范围。");
                return freePartitionState;
            }

            uint nextPartitionIndex = freePartitionState.FreePartitionCount == 0
                ? ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL
                : freePartitionState.HeadFreePartitionIndex;
            Span<byte> nextBytes = stackalloc byte[ChunkRegionFileLayout.PARTITION_NEXT_INDEX_SIZE];
            BinaryPrimitives.WriteUInt32LittleEndian(nextBytes, nextPartitionIndex);

            // 头插只改写 next 指针，payload 是否清理不是空闲链职责。
            ChunkRegionFileAccessor.WriteBytes(stream, ChunkRegionFileLayout.GetPartitionNextOffsetInFile(partitionIndex), nextBytes);

            ChunkRegionHeaderOperator.FreePartitionState newFreePartitionState = new(
                partitionIndex,
                checked(freePartitionState.FreePartitionCount + 1));
            if (!IsFreePartitionStateValid(stream, newFreePartitionState))
            {
                GD.PushError("[ChunkRegionFreePartitionChain] RegisterHeadPartition: 注册后的空闲分区头状态非法。");
                return freePartitionState;
            }

            return newFreePartitionState;
        }

        /// <summary>
        /// 将当前内存中的空闲分区头状态写回文件头，并强制冲刷到底层文件。
        /// </summary>
        public void FlushStateToFile()
        {
            if (!IsFreePartitionStateValid(_stream, FreePartitionState))
            {
                GD.PushError("[ChunkRegionFreePartitionChain] FlushStateToFile: 当前空闲分区头状态非法，无法写回文件。");
                return;
            }

            ChunkRegionHeaderOperator.WriteFreePartitionState(_stream, FreePartitionState);
            _stream.Flush(true);
            IsFlushed = true;
        }

        /// <summary>
        /// 枚举当前活链中的全部空闲分区索引。
        /// </summary>
        public IEnumerator<uint> GetEnumerator()
        {
            if (!IsFreePartitionStateValid(_stream, FreePartitionState))
            {
                yield break;
            }

            uint currentPartitionIndex = HeadPartitionIndex;
            for (uint i = 0; i < FreePartitionCount; i++)
            {
                if (!ChunkRegionPartitionOperator.IsPartitionIndexInRange(_stream, currentPartitionIndex))
                {
                    GD.PushError(
                        $"[ChunkRegionFreePartitionChain] GetEnumerator: 空闲分区索引 {currentPartitionIndex} 超出已分配范围。");
                    yield break;
                }

                yield return currentPartitionIndex;
                currentPartitionIndex = ChunkRegionPartitionOperator.ReadPartitionNextIndex(_stream, currentPartitionIndex);
                if (currentPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL && i < FreePartitionCount - 1)
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// 非泛型枚举器。
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 若存在未写回的头状态，则在释放时自动写回文件。
        /// </summary>
        public void Dispose()
        {
            if (!IsFlushed)
            {
                FlushStateToFile();
            }
        }

        /// <summary>
        /// 读取指定分区的 next 索引；若读取链时遇到越界或非法值则返回 false。
        /// </summary>
        private static bool TryReadNextPartitionIndex(FileStream stream, uint partitionIndex, out uint nextPartitionIndex)
        {
            nextPartitionIndex = ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL;
            if (!ChunkRegionPartitionOperator.IsPartitionIndexInRange(stream, partitionIndex))
            {
                return false;
            }

            nextPartitionIndex = ChunkRegionPartitionOperator.ReadPartitionNextIndex(stream, partitionIndex);
            return nextPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL ||
                   ChunkRegionPartitionOperator.IsPartitionIndexInRange(stream, nextPartitionIndex);
        }
    }
}
