using System;
using System.Buffers.Binary;
using System.IO;
using Godot;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// ChunkRegion 分区基础操作工具。
    /// <para>该静态类只负责分区数据、分区索引与分区容量等基础操作，不持有任何实例状态。</para>
    /// </summary>
    public static class ChunkRegionPartitionOperator
    {
        /// <summary>
        /// 读取指定分区的 next 索引。
        /// </summary>
        public static uint ReadPartitionNextIndex(FileStream stream, uint partitionIndex)
        {
            if (!IsPartitionIndexInRange(stream, partitionIndex))
            {
                GD.PushError($"[ChunkRegionPartitionOperator] ReadPartitionNextIndex: 分区索引 {partitionIndex} 超出已分配范围。");
                return ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL;
            }

            byte[] nextBytes = ChunkRegionFileAccessor.ReadBytes(
                stream,
                ChunkRegionFileLayout.GetPartitionNextOffsetInFile(partitionIndex),
                ChunkRegionFileLayout.PARTITION_NEXT_INDEX_SIZE);
            return BinaryPrimitives.ReadUInt32LittleEndian(nextBytes);
        }

        /// <summary>
        /// 尝试读取指定分区的 next 索引。
        /// <para>该方法负责把当前分区索引与 next 索引的合法性判断收口到分区基础操作层。</para>
        /// </summary>
        public static bool TryReadNextPartitionIndex(FileStream stream, uint partitionIndex, out uint nextPartitionIndex)
        {
            nextPartitionIndex = ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL;
            if (!IsPartitionIndexInRange(stream, partitionIndex)) return false;

            nextPartitionIndex = ReadPartitionNextIndex(stream, partitionIndex);
            if (nextPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL) return true;

            return IsPartitionIndexInRange(stream, nextPartitionIndex);
        }

        /// <summary>
        /// 读取指定分区中的有效数据。
        /// </summary>
        public static byte[] ReadPartitionPayload(FileStream stream, uint partitionIndex, int validDataLength)
        {
            if (!IsPartitionIndexInRange(stream, partitionIndex))
            {
                GD.PushError($"[ChunkRegionPartitionOperator] ReadPartitionPayload: 分区索引 {partitionIndex} 超出已分配范围。");
                return Array.Empty<byte>();
            }

            if (validDataLength < 0 || validDataLength > ChunkRegionFileLayout.PARTITION_PAYLOAD_SIZE)
            {
                GD.PushError($"[ChunkRegionPartitionOperator] ReadPartitionPayload: validDataLength={validDataLength} 超出有效范围。");
                return Array.Empty<byte>();
            }

            return ChunkRegionFileAccessor.ReadBytes(
                stream,
                ChunkRegionFileLayout.GetPartitionPayloadOffsetInFile(partitionIndex),
                validDataLength);
        }

        /// <summary>
        /// 在文件末尾追加指定数量的新分区。
        /// </summary>
        public static uint[] AppendTailPartitions(FileStream stream, int partitionCount)
        {
            if (partitionCount <= 0)
            {
                GD.PushError($"[ChunkRegionPartitionOperator] AppendTailPartitions: partitionCount={partitionCount} 非法。");
                return Array.Empty<uint>();
            }

            uint allocatedPartitionCount = GetAllocatedPartitionCount(stream);
            uint[] partitionIndices = new uint[partitionCount];
            for (int i = 0; i < partitionCount; i++)
            {
                partitionIndices[i] = allocatedPartitionCount + (uint)i;
            }

            // 这里只扩容文件并预留索引，不在这里初始化内容，真正写入由上层按链路一次性完成。
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
            if (!IsPartitionIndexInRange(stream, partitionIndex))
            {
                GD.PushError($"[ChunkRegionPartitionOperator] WritePartition: 分区索引 {partitionIndex} 超出已分配范围。");
                return;
            }

            if (payloadBytes.Length <= 0 || payloadBytes.Length > ChunkRegionFileLayout.PARTITION_PAYLOAD_SIZE)
            {
                GD.PushError($"[ChunkRegionPartitionOperator] WritePartition: payloadBytes.Length={payloadBytes.Length} 超出有效范围。");
                return;
            }

            byte[] partitionBytes = new byte[ChunkRegionFileLayout.PARTITION_ENTRY_SIZE];
            BinaryPrimitives.WriteUInt32LittleEndian(
                partitionBytes.AsSpan(0, ChunkRegionFileLayout.PARTITION_NEXT_INDEX_SIZE),
                nextPartitionIndex);

            // payload 只覆盖有效长度，剩余字节保持 0，便于最后一个分区只写入部分有效数据。
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
                GD.PushError("[ChunkRegionPartitionOperator] GetAllocatedPartitionCount: region 文件长度小于分区区域起始偏移。");
                return 0;
            }

            // 分区数量完全由文件长度反推，因此长度一旦不对齐，所有分区索引计算都会失效。
            if (partitionBytesLength % ChunkRegionFileLayout.PARTITION_ENTRY_SIZE != 0)
            {
                GD.PushError("[ChunkRegionPartitionOperator] GetAllocatedPartitionCount: region 文件长度与分区大小不对齐。");
                return 0;
            }

            return checked((uint)(partitionBytesLength / ChunkRegionFileLayout.PARTITION_ENTRY_SIZE));
        }

        /// <summary>
        /// 校验指定分区索引是否处于已分配范围内。
        /// </summary>
        public static bool IsPartitionIndexInRange(FileStream stream, uint partitionIndex)
        {
            return partitionIndex < GetAllocatedPartitionCount(stream);
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