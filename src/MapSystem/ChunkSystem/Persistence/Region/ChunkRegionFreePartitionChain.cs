using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence.Region
{
    /// <summary>
    /// ChunkRegion 空闲分区链。
    /// <para>该类型负责维护当前 region 文件中的空闲分区头状态，并基于分区 next 指针执行活链读取、取头、头插与头状态写回。</para>
    /// <para>该实例采用“变脏时上锁、清洁时解锁”的策略：只有当内存中的头状态与文件头不一致时，才持有 region 锁。</para>
    /// </summary>
    public sealed class ChunkRegionFreePartitionChain : IEnumerable<uint>, IDisposable
    {
        private readonly FileStream _stream;
        private readonly string _regionFilePath;

        /// <summary>
        /// 当前内存中的空闲分区头状态是否已经与文件头保持同步。
        /// <para>当该值为 false 时，表示实例已经进入 region 锁，且仍有待写回的头状态变更。</para>
        /// </summary>
        public bool IsFlushed { get; private set; } = true;

        /// <summary>
        /// 当前内存中的空闲分区头状态快照。
        /// <para>所有对空闲分区链头的修改都先落在该内存状态上，随后再通过 <see cref="FlushStateToFile"/> 写回文件。</para>
        /// </summary>
        public ChunkRegionHeaderOperator.FreePartitionState FreePartitionState { get; private set; }

        /// <summary>
        /// 当前空闲分区数量。
        /// </summary>
        public uint FreePartitionCount => FreePartitionState.FreePartitionCount;

        /// <summary>
        /// 当前空闲分区链头索引。
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

            string regionFilePath = stream.Name;
            if (string.IsNullOrWhiteSpace(regionFilePath))
            {
                GD.PushError("[ChunkRegionFreePartitionChain] Create: 无法从 stream 获取有效的 region 文件路径。");
                return null;
            }

            if (!ChunkRegionHeaderOperator.TryReadFreePartitionState(stream, out ChunkRegionHeaderOperator.FreePartitionState freePartitionState))
            {
                GD.PushError("[ChunkRegionFreePartitionChain] Create: 读取空闲分区头状态失败。");
                return null;
            }

            if (!IsFreePartitionStateValid(stream, freePartitionState))
            {
                GD.PushError("[ChunkRegionFreePartitionChain] Create: 读取到的空闲分区头状态非法。");
                return null;
            }

            return new ChunkRegionFreePartitionChain(stream, freePartitionState, regionFilePath);
        }

        /// <summary>
        /// 构造空闲分区链实例。
        /// </summary>
        private ChunkRegionFreePartitionChain(
            FileStream stream,
            ChunkRegionHeaderOperator.FreePartitionState freePartitionState,
            string regionFilePath)
        {
            _stream = stream;
            _regionFilePath = regionFilePath;
            FreePartitionState = freePartitionState;
        }

        /// <summary>
        /// 在首次修改头状态前将实例切换为脏状态。
        /// <para>仅当当前仍为 clean 状态时才真正进入 region 锁，避免重复 Enter 导致锁引用失衡。</para>
        /// </summary>
        private bool TryMarkDirty(string callerName)
        {
            if (!IsFlushed)
            {
                return true;
            }

            try
            {
                ChunkRegionFreePartitionLockTable.EnterRegionLock(_regionFilePath);
                IsFlushed = false;
                return true;
            }
            catch (Exception exception)
            {
                GD.PushError($"[ChunkRegionFreePartitionChain] {callerName}: 获取 region 空闲分区锁失败: {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// 将实例切换回 clean 状态。
        /// <para>仅当当前仍为 dirty 状态时才真正退出 region 锁，避免重复 Exit。</para>
        /// </summary>
        private bool TryCleanState(string callerName)
        {
            if (IsFlushed)
            {
                return true;
            }

            try
            {
                ChunkRegionFreePartitionLockTable.ExitRegionLock(_regionFilePath);
                IsFlushed = true;
                return true;
            }
            catch (Exception exception)
            {
                GD.PushError($"[ChunkRegionFreePartitionChain] {callerName}: 释放 region 空闲分区锁失败: {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// 判断指定空闲分区头状态是否合法。
        /// <para>该方法只校验头状态与当前文件分区总量是否一致，不负责验证整条 free chain 是否存在环或提前结束。</para>
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
        /// 使用快慢指针法判断指定空闲分区链是否成环。
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
                if (!ChunkRegionPartitionOperator.TryReadValidatedNextPartitionIndex(stream, slowPartitionIndex, out slowPartitionIndex)) return false;

                // 快指针第一次前进；若此时已经到达链尾，说明当前链可正常结束而非成环。
                if (!ChunkRegionPartitionOperator.TryReadValidatedNextPartitionIndex(stream, fastPartitionIndex, out fastPartitionIndex)) return false;
                if (fastPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL) return false;

                // 快指针第二次前进；依然能走到链尾则说明不是环，若与慢指针相遇则说明成环。
                if (!ChunkRegionPartitionOperator.TryReadValidatedNextPartitionIndex(stream, fastPartitionIndex, out fastPartitionIndex)) return false;
                if (fastPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL) return false;
                if (slowPartitionIndex == fastPartitionIndex) return true;
            }
        }

        /// <summary>
        /// 获取指定数量的空闲分区列表，并同步更新内存中的头状态。
        /// <para>该方法只在成功时保留新的头状态；若链成环、越界或数量不足，则会把内存状态回滚到调用前快照。</para>
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
                GD.PushError($"[ChunkRegionFreePartitionChain] GetFreePartitionList: 当前空闲分区数量 {FreePartitionCount} 不足以提供 {takePartitionCount} 个分区。");
                return null;
            }

            // 先保存调用前的内存快照；一旦中途失败，需要把内存状态和脏/净状态都恢复回去。
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
                    if (oldIsFlushed)
                    {
                        TryCleanState(nameof(GetFreePartitionList));
                    }
                    return null;
                }

                if (!visitedPartitionIndices.Add(freePartitionIndex))
                {
                    GD.PushError("[ChunkRegionFreePartitionChain] GetFreePartitionList: 空闲分区链存在循环或重复节点。");
                    FreePartitionState = oldFreePartitionState;
                    if (oldIsFlushed)
                    {
                        TryCleanState(nameof(GetFreePartitionList));
                    }
                    return null;
                }

                freePartitionList[i] = freePartitionIndex;
            }

            return freePartitionList;
        }

        /// <summary>
        /// 从空闲分区链头取出一个空闲分区。
        /// <para>该方法只修改内存中的头状态；真正的文件头写回由 <see cref="FlushStateToFile"/> 统一负责。</para>
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
                GD.PushError($"[ChunkRegionFreePartitionChain] TakeHeadPartition: 头分区索引 {currentHeadPartitionIndex} 超出已分配范围。");
                return ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL;
            }

            if (!ChunkRegionPartitionOperator.TryReadValidatedNextPartitionIndex(_stream, currentHeadPartitionIndex, out uint nextHeadPartitionIndex))
            {
                GD.PushError("[ChunkRegionFreePartitionChain] TakeHeadPartition: 读取头分区 next 索引失败。");
                return ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL;
            }

            if (FreePartitionCount == 1)
            {
                if (nextHeadPartitionIndex != ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
                {
                    GD.PushError("[ChunkRegionFreePartitionChain] TakeHeadPartition: 单节点空闲链尾部未写入哨兵值。");
                    return ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL;
                }

                if (!TryMarkDirty(nameof(TakeHeadPartition)))
                {
                    return ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL;
                }

                FreePartitionState = new ChunkRegionHeaderOperator.FreePartitionState(
                    ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL,
                    0);
                return currentHeadPartitionIndex;
            }

            if (nextHeadPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
            {
                GD.PushError("[ChunkRegionFreePartitionChain] TakeHeadPartition: 空闲分区链在预期数量耗尽前提前结束。");
                return ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL;
            }

            if (!TryMarkDirty(nameof(TakeHeadPartition)))
            {
                return ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL;
            }

            FreePartitionState = new ChunkRegionHeaderOperator.FreePartitionState(
                nextHeadPartitionIndex,
                FreePartitionCount - 1);
            return currentHeadPartitionIndex;
        }

        /// <summary>
        /// 将指定分区注册到当前空闲分区链头部。
        /// <para>该方法会先确保实例进入 dirty 状态，再写入分区 next 指针并更新内存头状态。</para>
        /// </summary>
        public ChunkRegionHeaderOperator.FreePartitionState RegisterHeadPartition(uint partitionIndex)
        {
            if (!TryMarkDirty(nameof(RegisterHeadPartition)))
            {
                return FreePartitionState;
            }

            ChunkRegionHeaderOperator.FreePartitionState newFreePartitionState =
                RegisterHeadPartition(_stream, FreePartitionState, partitionIndex);
            if (newFreePartitionState.HeadFreePartitionIndex != FreePartitionState.HeadFreePartitionIndex ||
                newFreePartitionState.FreePartitionCount != FreePartitionState.FreePartitionCount)
            {
                FreePartitionState = newFreePartitionState;
            }

            return FreePartitionState;
        }

        /// <summary>
        /// 将指定分区注册到指定空闲分区头部状态。
        /// <para>该静态方法会直接写入分区 next 指针，但不会修改调用方实例的 <see cref="IsFlushed"/>。</para>
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
                GD.PushError($"[ChunkRegionFreePartitionChain] RegisterHeadPartition: 分区索引 {partitionIndex} 超出已分配范围。");
                return freePartitionState;
            }

            if (freePartitionState.FreePartitionCount > 0 &&
                !ChunkRegionPartitionOperator.IsPartitionIndexInRange(stream, freePartitionState.HeadFreePartitionIndex))
            {
                GD.PushError($"[ChunkRegionFreePartitionChain] RegisterHeadPartition: 当前头分区索引 {freePartitionState.HeadFreePartitionIndex} 超出已分配范围。");
                return freePartitionState;
            }

            uint nextPartitionIndex = freePartitionState.FreePartitionCount == 0
                ? ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL
                : freePartitionState.HeadFreePartitionIndex;
            Span<byte> nextBytes = stackalloc byte[ChunkRegionFileLayout.PARTITION_NEXT_INDEX_SIZE];
            BinaryPrimitives.WriteUInt32LittleEndian(nextBytes, nextPartitionIndex);

            // 头插只改 next 指针，payload 是否清理不是空闲链职责。
            if (!ChunkRegionFileAccessor.TryWriteBytes(stream, ChunkRegionFileLayout.GetPartitionNextOffsetInFile(partitionIndex), nextBytes))
            {
                return freePartitionState;
            }

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
        /// <para>该方法是“dirty 回到 clean”的标准出口；成功时会统一释放 region 锁。</para>
        /// </summary>
        public bool FlushStateToFile()
        {
            if (IsFlushed)
            {
                return true;
            }

            if (!IsFreePartitionStateValid(_stream, FreePartitionState))
            {
                GD.PushError("[ChunkRegionFreePartitionChain] FlushStateToFile: 当前空闲分区头状态非法，无法写回文件。");
                return false;
            }

            if (!ChunkRegionHeaderOperator.WriteFreePartitionState(_stream, FreePartitionState))
            {
                return false;
            }

            try
            {
                _stream.Flush(true);
            }
            catch (Exception exception)
            {
                GD.PushError($"[ChunkRegionFreePartitionChain] FlushStateToFile: 冲刷文件失败: {exception.Message}");
                return false;
            }

            return TryCleanState(nameof(FlushStateToFile));
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
                    GD.PushError($"[ChunkRegionFreePartitionChain] GetEnumerator: 空闲分区索引 {currentPartitionIndex} 超出已分配范围。");
                    yield break;
                }

                yield return currentPartitionIndex;
                if (!ChunkRegionPartitionOperator.TryReadValidatedNextPartitionIndex(_stream, currentPartitionIndex, out uint nextPartitionIndex))
                {
                    yield break;
                }

                currentPartitionIndex = nextPartitionIndex;
                if (currentPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL && i < FreePartitionCount - 1)
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// 返回非泛型枚举器。
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 释放当前空闲分区链实例。
        /// <para>若当前仍为 dirty 状态，会先尝试写回文件；若写回失败，则仍会执行 clean 流程以避免锁泄漏。</para>
        /// </summary>
        public void Dispose()
        {
            if (IsFlushed)
            {
                return;
            }

            if (FlushStateToFile())
            {
                return;
            }

            GD.PushError("[ChunkRegionFreePartitionChain] Dispose: 写回空闲分区头状态失败，将直接执行 clean 流程以避免锁泄漏。");
            TryCleanState(nameof(Dispose));
        }
    }
}
