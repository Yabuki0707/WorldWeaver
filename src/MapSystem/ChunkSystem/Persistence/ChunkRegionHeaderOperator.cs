using System;
using System.Buffers.Binary;
using System.IO;
using Godot;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// ChunkRegion 头数据操作工具。
    /// <para>该静态类只负责头数据区与空闲分区头状态的读写，不持有任何实例状态。</para>
    /// </summary>
    public static class ChunkRegionHeaderOperator
    {
        /// <summary>
        /// 区块头数据记录。
        /// </summary>
        public readonly struct ChunkHeaderData(uint firstPartitionIndex, ushort lastPartitionDataLength, uint partitionCount, long timestamp)
        {
            /// <summary>
            /// 区块数据链的首分区索引。
            /// </summary>
            public uint FirstPartitionIndex { get; } = firstPartitionIndex;

            /// <summary>
            /// 最后一个分区中的有效数据长度。
            /// </summary>
            public ushort LastPartitionDataLength { get; } = lastPartitionDataLength;

            /// <summary>
            /// 当前区块占用的分区总数。
            /// </summary>
            public uint PartitionCount { get; } = partitionCount;

            /// <summary>
            /// 区块头记录中的时间戳。
            /// </summary>
            public long Timestamp { get; } = timestamp;

            /// <summary>
            /// 当前头记录是否为空。
            /// </summary>
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
            /// <summary>
            /// 当前空闲分区链表头索引。
            /// </summary>
            public uint HeadFreePartitionIndex { get; } = headFreePartitionIndex;

            /// <summary>
            /// 当前空闲分区总数。
            /// </summary>
            public uint FreePartitionCount { get; } = freePartitionCount;
        }

        /// <summary>
        /// 根据局部 chunk 坐标读取头数据。
        /// </summary>
        public static ChunkHeaderData ReadChunkHeaderData(FileStream stream, Vector2I localChunkPosition)
        {
            ValidateLocalChunkPosition(localChunkPosition);

            byte[] headerBytes = ChunkRegionFileAccessor.ReadBytes(
                stream,
                ChunkRegionFileLayout.GetChunkDataOffsetInFile(localChunkPosition),
                ChunkRegionFileLayout.CHUNK_DATA_ENTRY_SIZE);
            return new ChunkHeaderData(
                BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan(0, sizeof(uint))),
                BinaryPrimitives.ReadUInt16LittleEndian(headerBytes.AsSpan(sizeof(uint), sizeof(ushort))),
                BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan(sizeof(uint) + sizeof(ushort), sizeof(uint))),
                BinaryPrimitives.ReadInt64LittleEndian(headerBytes.AsSpan(sizeof(uint) + sizeof(ushort) + sizeof(uint), sizeof(long))));
        }

        /// <summary>
        /// 根据局部 chunk 坐标覆写头数据。
        /// </summary>
        public static void WriteChunkHeaderData(FileStream stream, Vector2I localChunkPosition, ChunkHeaderData chunkHeaderData)
        {
            ValidateLocalChunkPosition(localChunkPosition);
            ValidateChunkHeaderData(stream, chunkHeaderData);

            Span<byte> headerBytes = stackalloc byte[ChunkRegionFileLayout.CHUNK_DATA_ENTRY_SIZE];
            BinaryPrimitives.WriteUInt32LittleEndian(headerBytes.Slice(0, sizeof(uint)), chunkHeaderData.FirstPartitionIndex);
            BinaryPrimitives.WriteUInt16LittleEndian(headerBytes.Slice(sizeof(uint), sizeof(ushort)), chunkHeaderData.LastPartitionDataLength);
            BinaryPrimitives.WriteUInt32LittleEndian(
                headerBytes.Slice(sizeof(uint) + sizeof(ushort), sizeof(uint)),
                chunkHeaderData.PartitionCount);
            BinaryPrimitives.WriteInt64LittleEndian(
                headerBytes.Slice(sizeof(uint) + sizeof(ushort) + sizeof(uint), sizeof(long)),
                chunkHeaderData.Timestamp);
            ChunkRegionFileAccessor.WriteBytes(stream, ChunkRegionFileLayout.GetChunkDataOffsetInFile(localChunkPosition), headerBytes);
        }

        /// <summary>
        /// 根据局部 chunk 坐标覆写头数据。
        /// </summary>
        public static void WriteChunkHeaderData(
            FileStream stream,
            Vector2I localChunkPosition,
            uint firstPartitionIndex,
            ushort lastPartitionDataLength,
            uint partitionCount,
            long timestamp)
        {
            WriteChunkHeaderData(
                stream,
                localChunkPosition,
                new ChunkHeaderData(firstPartitionIndex, lastPartitionDataLength, partitionCount, timestamp));
        }

        /// <summary>
        /// 读取空闲分区头状态。
        /// </summary>
        public static FreePartitionState ReadFreePartitionState(FileStream stream)
        {
            byte[] stateBytes = ChunkRegionFileAccessor.ReadBytes(
                stream,
                ChunkRegionFileLayout.HEAD_FREE_PARTITION_INDEX_OFFSET_IN_FILE,
                ChunkRegionFileLayout.HEAD_FREE_PARTITION_INDEX_SIZE + ChunkRegionFileLayout.FREE_PARTITION_COUNT_SIZE);
            return new FreePartitionState(
                BinaryPrimitives.ReadUInt32LittleEndian(
                    stateBytes.AsSpan(0, ChunkRegionFileLayout.HEAD_FREE_PARTITION_INDEX_SIZE)),
                BinaryPrimitives.ReadUInt32LittleEndian(
                    stateBytes.AsSpan(
                        ChunkRegionFileLayout.HEAD_FREE_PARTITION_INDEX_SIZE,
                        ChunkRegionFileLayout.FREE_PARTITION_COUNT_SIZE)));
        }

        /// <summary>
        /// 写入空闲分区头状态。
        /// </summary>
        public static void WriteFreePartitionState(FileStream stream, FreePartitionState freePartitionState)
        {
            if (!TryValidateFreePartitionState(stream, freePartitionState))
            {
                throw new InvalidDataException("region 文件中的空闲分区头状态非法。");
            }

            Span<byte> stateBytes = stackalloc byte[
                ChunkRegionFileLayout.HEAD_FREE_PARTITION_INDEX_SIZE + ChunkRegionFileLayout.FREE_PARTITION_COUNT_SIZE];
            BinaryPrimitives.WriteUInt32LittleEndian(
                stateBytes.Slice(0, ChunkRegionFileLayout.HEAD_FREE_PARTITION_INDEX_SIZE),
                freePartitionState.HeadFreePartitionIndex);
            BinaryPrimitives.WriteUInt32LittleEndian(
                stateBytes.Slice(ChunkRegionFileLayout.HEAD_FREE_PARTITION_INDEX_SIZE, ChunkRegionFileLayout.FREE_PARTITION_COUNT_SIZE),
                freePartitionState.FreePartitionCount);
            ChunkRegionFileAccessor.WriteBytes(stream, ChunkRegionFileLayout.HEAD_FREE_PARTITION_INDEX_OFFSET_IN_FILE, stateBytes);
        }

        /// <summary>
        /// 校验头数据是否合理。
        /// </summary>
        public static void ValidateChunkHeaderData(FileStream stream, ChunkHeaderData chunkHeaderData)
        {
            if (chunkHeaderData.IsEmpty)
            {
                return;
            }

            if (chunkHeaderData.FirstPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
            {
                throw new InvalidDataException("chunk 头数据中的首分区索引非法。");
            }

            if (chunkHeaderData.PartitionCount == 0)
            {
                throw new InvalidDataException("chunk 头数据中的分区数量为 0。");
            }

            if (chunkHeaderData.LastPartitionDataLength == 0 ||
                chunkHeaderData.LastPartitionDataLength > ChunkRegionFileLayout.PARTITION_PAYLOAD_SIZE)
            {
                throw new InvalidDataException("chunk 头数据中的最后分区有效长度非法。");
            }

            uint allocatedPartitionCount = ChunkRegionPartitionOperator.GetAllocatedPartitionCount(stream);
            if (chunkHeaderData.FirstPartitionIndex >= allocatedPartitionCount)
            {
                throw new InvalidDataException("chunk 头数据中的首分区索引越界。");
            }

            if (chunkHeaderData.PartitionCount > allocatedPartitionCount)
            {
                throw new InvalidDataException("chunk 头数据中的分区数量超过当前已分配分区总数。");
            }
        }

        /// <summary>
        /// 校验空闲分区状态是否合理。
        /// </summary>
        public static bool TryValidateFreePartitionState(FileStream stream, FreePartitionState freePartitionState)
        {
            uint allocatedPartitionCount = ChunkRegionPartitionOperator.GetAllocatedPartitionCount(stream);
            if (freePartitionState.FreePartitionCount == 0)
            {
                return freePartitionState.HeadFreePartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL;
            }

            if (freePartitionState.HeadFreePartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
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
        /// 校验局部 chunk 坐标是否合法。
        /// </summary>
        private static void ValidateLocalChunkPosition(Vector2I localChunkPosition)
        {
            if (localChunkPosition.X < 0 || localChunkPosition.X >= ChunkRegionFileLayout.REGION_CHUNK_AXIS ||
                localChunkPosition.Y < 0 || localChunkPosition.Y >= ChunkRegionFileLayout.REGION_CHUNK_AXIS)
            {
                throw new ArgumentOutOfRangeException(nameof(localChunkPosition));
            }
        }


    }
}
