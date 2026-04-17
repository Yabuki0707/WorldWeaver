using System;
using System.Buffers.Binary;
using System.IO;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// ChunkRegion 分区操作工具。
    /// <para>该静态类只负责分区链、分区数据与分区分配相关操作，不持有任何实例状态。</para>
    /// </summary>
    public static class ChunkRegionPartitionOperator
    {
        /// <summary>
        /// 读取指定分区的 next 索引。
        /// </summary>
        public static uint ReadPartitionNextIndex(FileStream stream, uint partitionIndex)
        {
            EnsurePartitionIndexInRange(stream, partitionIndex);
            byte[] nextBytes = ChunkRegionFileAccessor.ReadBytes(
                stream,
                ChunkRegionFileLayout.GetPartitionNextOffsetInFile(partitionIndex),
                ChunkRegionFileLayout.PARTITION_NEXT_INDEX_SIZE);
            return BinaryPrimitives.ReadUInt32LittleEndian(nextBytes);
        }

        /// <summary>
        /// 读取指定分区中的有效数据。
        /// </summary>
        public static byte[] ReadPartitionPayload(FileStream stream, uint partitionIndex, int validDataLength)
        {
            EnsurePartitionIndexInRange(stream, partitionIndex);
            if (validDataLength < 0 || validDataLength > ChunkRegionFileLayout.PARTITION_PAYLOAD_SIZE)
            {
                throw new ArgumentOutOfRangeException(nameof(validDataLength));
            }

            return ChunkRegionFileAccessor.ReadBytes(stream, ChunkRegionFileLayout.GetPartitionPayloadOffsetInFile(partitionIndex), validDataLength);
        }

        /// <summary>
        /// 执行空闲分区取出操作。
        /// </summary>
        public static uint TakeFreePartition(FileStream stream, ref ChunkRegionHeaderOperator.FreePartitionState freePartitionState)
        {
            if (freePartitionState.FreePartitionCount == 0)
            {
                throw new InvalidDataException("当前没有可取出的空闲分区。");
            }

            uint currentHeadIndex = freePartitionState.HeadFreePartitionIndex;
            EnsurePartitionIndexInRange(stream, currentHeadIndex);

            uint nextFreePartitionIndex = ReadPartitionNextIndex(stream, currentHeadIndex);
            uint remainingCount = freePartitionState.FreePartitionCount - 1;
            if (remainingCount == 0)
            {
                freePartitionState = new ChunkRegionHeaderOperator.FreePartitionState(
                    ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL,
                    0);
                return currentHeadIndex;
            }

            if (nextFreePartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
            {
                throw new InvalidDataException("空闲分区链在预期数量耗尽前提前结束。");
            }

            EnsurePartitionIndexInRange(stream, nextFreePartitionIndex);
            freePartitionState = new ChunkRegionHeaderOperator.FreePartitionState(nextFreePartitionIndex, remainingCount);
            return currentHeadIndex;
        }

        /// <summary>
        /// 执行空闲分区注册操作。
        /// </summary>
        public static void RegisterFreePartition(
            FileStream stream,
            uint partitionIndex,
            ref ChunkRegionHeaderOperator.FreePartitionState freePartitionState)
        {
            EnsurePartitionIndexInRange(stream, partitionIndex);
            if (freePartitionState.FreePartitionCount > 0)
            {
                EnsurePartitionIndexInRange(stream, freePartitionState.HeadFreePartitionIndex);
            }

            uint nextFreePartitionIndex = freePartitionState.FreePartitionCount == 0
                ? ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL
                : freePartitionState.HeadFreePartitionIndex;
            Span<byte> nextBytes = stackalloc byte[ChunkRegionFileLayout.PARTITION_NEXT_INDEX_SIZE];
            BinaryPrimitives.WriteUInt32LittleEndian(nextBytes, nextFreePartitionIndex);
            ChunkRegionFileAccessor.WriteBytes(stream, ChunkRegionFileLayout.GetPartitionNextOffsetInFile(partitionIndex), nextBytes);
            freePartitionState = new ChunkRegionHeaderOperator.FreePartitionState(
                partitionIndex,
                checked(freePartitionState.FreePartitionCount + 1));
        }

        /// <summary>
        /// 在文件末尾追加指定数量的新分区。
        /// </summary>
        public static uint[] AppendTailPartitions(FileStream stream, int partitionCount)
        {
            if (partitionCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(partitionCount));
            }

            uint allocatedPartitionCount = GetAllocatedPartitionCount(stream);
            uint[] partitionIndices = new uint[partitionCount];
            for (int i = 0; i < partitionCount; i++)
            {
                partitionIndices[i] = allocatedPartitionCount + (uint)i;
            }

            long newFileLength = ChunkRegionFileLayout.PARTITION_AREA_OFFSET_IN_FILE +
                (allocatedPartitionCount + (uint)partitionCount) * ChunkRegionFileLayout.PARTITION_ENTRY_SIZE;
            stream.SetLength(newFileLength);
            return partitionIndices;
        }

        /// <summary>
        /// 写入单个分区的 next 索引与有效数据。
        /// </summary>
        public static void WritePartition(FileStream stream, uint partitionIndex, uint nextPartitionIndex, ReadOnlySpan<byte> payloadBytes)
        {
            EnsurePartitionIndexInRange(stream, partitionIndex);
            if (payloadBytes.Length <= 0 || payloadBytes.Length > ChunkRegionFileLayout.PARTITION_PAYLOAD_SIZE)
            {
                throw new ArgumentOutOfRangeException(nameof(payloadBytes));
            }

            byte[] partitionBytes = new byte[ChunkRegionFileLayout.PARTITION_ENTRY_SIZE];
            BinaryPrimitives.WriteUInt32LittleEndian(
                partitionBytes.AsSpan(0, ChunkRegionFileLayout.PARTITION_NEXT_INDEX_SIZE),
                nextPartitionIndex);
            payloadBytes.CopyTo(partitionBytes.AsSpan(ChunkRegionFileLayout.PARTITION_NEXT_INDEX_SIZE, payloadBytes.Length));
            ChunkRegionFileAccessor.WriteBytes(stream, ChunkRegionFileLayout.GetPartitionOffsetInFile(partitionIndex), partitionBytes);
        }

        /// <summary>
        /// 获取当前文件中已分配的分区总数。
        /// </summary>
        public static uint GetAllocatedPartitionCount(FileStream stream)
        {
            long partitionBytesLength = stream.Length - ChunkRegionFileLayout.PARTITION_AREA_OFFSET_IN_FILE;
            if (partitionBytesLength < 0)
            {
                throw new InvalidDataException("region 文件长度小于分区区域起始偏移。");
            }

            if (partitionBytesLength % ChunkRegionFileLayout.PARTITION_ENTRY_SIZE != 0)
            {
                throw new InvalidDataException("region 文件长度与分区大小不对齐。");
            }

            return checked((uint)(partitionBytesLength / ChunkRegionFileLayout.PARTITION_ENTRY_SIZE));
        }

        /// <summary>
        /// 校验指定分区索引是否处于已分配范围内。
        /// </summary>
        public static void EnsurePartitionIndexInRange(FileStream stream, uint partitionIndex)
        {
            if (partitionIndex >= GetAllocatedPartitionCount(stream))
            {
                throw new InvalidDataException($"分区索引 {partitionIndex} 超出已分配分区范围。");
            }
        }

        /// <summary>
        /// 计算指定字节长度需要的分区数量。
        /// </summary>
        public static int CalculateRequiredPartitionCount(int dataLength)
        {
            if (dataLength <= 0)
            {
                return 0;
            }

            return (dataLength + ChunkRegionFileLayout.PARTITION_PAYLOAD_SIZE - 1) /
                   ChunkRegionFileLayout.PARTITION_PAYLOAD_SIZE;
        }


    }
}
