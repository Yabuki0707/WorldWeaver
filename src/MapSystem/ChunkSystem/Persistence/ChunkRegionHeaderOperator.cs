using System;
using System.Buffers.Binary;
using System.IO;
using Godot;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// ChunkRegion 头数据操作工具。
    /// <para>该静态类只负责头数据区与空闲分区头字段的基础读写，不持有任何实例状态。</para>
    /// </summary>
    public static class ChunkRegionHeaderOperator
    {
        /// <summary>
        /// 区块头数据记录。
        /// </summary>
        public readonly struct ChunkHeaderData(uint firstPartitionIndex, ushort lastPartitionDataLength, uint partitionCount, long timestamp)
        {
            public uint FirstPartitionIndex { get; } = firstPartitionIndex;
            public ushort LastPartitionDataLength { get; } = lastPartitionDataLength;
            public uint PartitionCount { get; } = partitionCount;
            public long Timestamp { get; } = timestamp;

            public bool IsEmpty =>
                FirstPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL &&
                LastPartitionDataLength == 0 &&
                PartitionCount == 0;
        }

        /// <summary>
        /// 空闲分区头状态。
        /// </summary>
        public readonly struct FreePartitionState(uint headFreePartitionIndex, uint freePartitionCount)
        {
            public uint HeadFreePartitionIndex { get; } = headFreePartitionIndex;
            public uint FreePartitionCount { get; } = freePartitionCount;
        }

        /// <summary>
        /// 根据局部 chunk 坐标读取头数据。
        /// </summary>
        public static ChunkHeaderData? ReadChunkHeaderData(FileStream stream, Vector2I localChunkPosition)
        {
            if (!ChunkRegionFileLayout.TryGetChunkDataOffsetInFile(localChunkPosition, out long chunkDataOffsetInFile))
            {
                return null;
            }

            // 头数据按固定长度连续布局，读取后直接按既定字段顺序反序列化即可。
            if (!ChunkRegionFileAccessor.TryReadBytes(
                    stream,
                    chunkDataOffsetInFile,
                    ChunkRegionFileLayout.CHUNK_DATA_ENTRY_SIZE,
                    out byte[] headerBytes))
            {
                return null;
            }

            return new ChunkHeaderData(
                BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan(0, sizeof(uint))),
                BinaryPrimitives.ReadUInt16LittleEndian(headerBytes.AsSpan(sizeof(uint), sizeof(ushort))),
                BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan(sizeof(uint) + sizeof(ushort), sizeof(uint))),
                BinaryPrimitives.ReadInt64LittleEndian(headerBytes.AsSpan(sizeof(uint) + sizeof(ushort) + sizeof(uint), sizeof(long))));
        }

        /// <summary>
        /// 根据局部 chunk 坐标覆写头数据。
        /// </summary>
        public static bool WriteChunkHeaderData(FileStream stream, Vector2I localChunkPosition, ChunkHeaderData chunkHeaderData)
        {
            if (!ChunkRegionFileLayout.TryGetChunkDataOffsetInFile(localChunkPosition, out long chunkDataOffsetInFile))
            {
                return false;
            }

            if (!ValidateChunkHeaderData(stream, chunkHeaderData))
            {
                return false;
            }

            // 写入前先做完整校验，避免把结构上不可能成立的头记录落盘后污染整个 region。
            Span<byte> headerBytes = stackalloc byte[ChunkRegionFileLayout.CHUNK_DATA_ENTRY_SIZE];
            BinaryPrimitives.WriteUInt32LittleEndian(headerBytes.Slice(0, sizeof(uint)), chunkHeaderData.FirstPartitionIndex);
            BinaryPrimitives.WriteUInt16LittleEndian(headerBytes.Slice(sizeof(uint), sizeof(ushort)), chunkHeaderData.LastPartitionDataLength);
            BinaryPrimitives.WriteUInt32LittleEndian(
                headerBytes.Slice(sizeof(uint) + sizeof(ushort), sizeof(uint)),
                chunkHeaderData.PartitionCount);
            BinaryPrimitives.WriteInt64LittleEndian(
                headerBytes.Slice(sizeof(uint) + sizeof(ushort) + sizeof(uint), sizeof(long)),
                chunkHeaderData.Timestamp);
            return ChunkRegionFileAccessor.TryWriteBytes(stream, chunkDataOffsetInFile, headerBytes);
        }

        /// <summary>
        /// 根据局部 chunk 坐标覆写头数据。
        /// </summary>
        public static bool WriteChunkHeaderData(
            FileStream stream,
            Vector2I localChunkPosition,
            uint firstPartitionIndex,
            ushort lastPartitionDataLength,
            uint partitionCount,
            long timestamp)
        {
            return WriteChunkHeaderData(
                stream,
                localChunkPosition,
                new ChunkHeaderData(firstPartitionIndex, lastPartitionDataLength, partitionCount, timestamp));
        }

        /// <summary>
        /// 读取空闲分区头状态。
        /// </summary>
        public static bool TryReadFreePartitionState(FileStream stream, out FreePartitionState freePartitionState)
        {
            freePartitionState = default;
            if (!ChunkRegionFileAccessor.TryReadBytes(
                    stream,
                    ChunkRegionFileLayout.HEAD_FREE_PARTITION_INDEX_OFFSET_IN_FILE,
                    ChunkRegionFileLayout.HEAD_FREE_PARTITION_INDEX_SIZE + ChunkRegionFileLayout.FREE_PARTITION_COUNT_SIZE,
                    out byte[] stateBytes))
            {
                return false;
            }

            freePartitionState = new FreePartitionState(
                BinaryPrimitives.ReadUInt32LittleEndian(
                    stateBytes.AsSpan(0, ChunkRegionFileLayout.HEAD_FREE_PARTITION_INDEX_SIZE)),
                BinaryPrimitives.ReadUInt32LittleEndian(
                    stateBytes.AsSpan(
                        ChunkRegionFileLayout.HEAD_FREE_PARTITION_INDEX_SIZE,
                        ChunkRegionFileLayout.FREE_PARTITION_COUNT_SIZE)));
            return true;
        }

        /// <summary>
        /// 写入空闲分区头状态。
        /// <para>该方法只负责字节写入，不负责状态合法性校验。</para>
        /// </summary>
        public static bool WriteFreePartitionState(FileStream stream, FreePartitionState freePartitionState)
        {
            Span<byte> stateBytes = stackalloc byte[
                ChunkRegionFileLayout.HEAD_FREE_PARTITION_INDEX_SIZE + ChunkRegionFileLayout.FREE_PARTITION_COUNT_SIZE];
            BinaryPrimitives.WriteUInt32LittleEndian(
                stateBytes.Slice(0, ChunkRegionFileLayout.HEAD_FREE_PARTITION_INDEX_SIZE),
                freePartitionState.HeadFreePartitionIndex);
            BinaryPrimitives.WriteUInt32LittleEndian(
                stateBytes.Slice(ChunkRegionFileLayout.HEAD_FREE_PARTITION_INDEX_SIZE, ChunkRegionFileLayout.FREE_PARTITION_COUNT_SIZE),
                freePartitionState.FreePartitionCount);
            return ChunkRegionFileAccessor.TryWriteBytes(stream, ChunkRegionFileLayout.HEAD_FREE_PARTITION_INDEX_OFFSET_IN_FILE, stateBytes);
        }

        /// <summary>
        /// 校验头数据是否合理。
        /// </summary>
        public static bool ValidateChunkHeaderData(FileStream stream, ChunkHeaderData chunkHeaderData)
        {
            if (chunkHeaderData.IsEmpty)
            {
                // 空头记录是合法状态，表示该 chunk 目前没有挂任何分区链。
                return true;
            }

            if (chunkHeaderData.FirstPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
            {
                GD.PushError("[ChunkRegionHeaderOperator] ValidateChunkHeaderData: chunk 头数据中的首分区索引非法。");
                return false;
            }

            if (chunkHeaderData.PartitionCount == 0)
            {
                GD.PushError("[ChunkRegionHeaderOperator] ValidateChunkHeaderData: chunk 头数据中的分区数量为 0。");
                return false;
            }

            if (chunkHeaderData.LastPartitionDataLength == 0 ||
                chunkHeaderData.LastPartitionDataLength > ChunkRegionFileLayout.PARTITION_PAYLOAD_SIZE)
            {
                GD.PushError("[ChunkRegionHeaderOperator] ValidateChunkHeaderData: chunk 头数据中的最后分区有效长度非法。");
                return false;
            }

            // 这里只校验“能否作为一条可能存在的链”的上界约束，链条本身是否断裂交给读写流程逐步验证。
            uint allocatedPartitionCount = ChunkRegionPartitionOperator.GetAllocatedPartitionCount(stream);
            if (chunkHeaderData.FirstPartitionIndex >= allocatedPartitionCount)
            {
                GD.PushError("[ChunkRegionHeaderOperator] ValidateChunkHeaderData: chunk 头数据中的首分区索引越界。");
                return false;
            }

            if (chunkHeaderData.PartitionCount > allocatedPartitionCount)
            {
                GD.PushError("[ChunkRegionHeaderOperator] ValidateChunkHeaderData: chunk 头数据中的分区数量超过当前已分配分区总数。");
                return false;
            }

            return true;
        }
    }
}