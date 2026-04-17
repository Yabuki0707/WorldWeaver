using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// ChunkRegion 写入器。
    /// <para>该类型基于 ChunkRegionFileAccessor 与静态操作工具，实现区块数据的新链写入、头数据更新与旧链回收流程。</para>
    /// </summary>
    public sealed class ChunkRegionWriter : ChunkRegionFileAccessor
    {
        /// <summary>
        /// 仅允许通过 Open 创建写入器实例。
        /// </summary>
        private ChunkRegionWriter(string regionFilePath, Vector2I regionPosition, FileStream stream)
            : base(regionFilePath, regionPosition, stream)
        {
        }

        /// <summary>
        /// 打开指定 rootPath 下的 region 写入器。
        /// <para>若文件不存在、路径不匹配或格式校验失败，则返回 null 并输出警告。</para>
        /// </summary>
        public new static ChunkRegionWriter Open(string rootPath, Vector2I regionPosition)
        {
            if (!TryOpenValidatedStream(rootPath, regionPosition, System.IO.FileAccess.ReadWrite, out string regionFilePath, out FileStream stream))
            {
                return null;
            }

            return new ChunkRegionWriter(regionFilePath, regionPosition, stream);
        }

        /// <summary>
        /// 保存指定 chunk 的储存对象。
        /// </summary>
        public void SaveChunkStorage(ChunkPosition chunkPosition, ChunkDataStorage storage)
        {
            ArgumentNullException.ThrowIfNull(storage);

            ChunkRegionPositionProcessor.GetRegionAndLocalChunkPosition(
                chunkPosition,
                out Vector2I regionPosition,
                out Vector2I localChunkPosition);
            if (regionPosition != RegionPosition)
            {
                throw new ArgumentException(
                    $"chunkPosition {chunkPosition} 不属于当前 region ({RegionPosition.X}, {RegionPosition.Y})。",
                    nameof(chunkPosition));
            }

            try
            {
                ChunkRegionHeaderOperator.ChunkHeaderData oldChunkHeaderData =
                    ChunkRegionHeaderOperator.ReadChunkHeaderData(Stream, localChunkPosition);
                ChunkRegionHeaderOperator.ValidateChunkHeaderData(Stream, oldChunkHeaderData);

                byte[] compressedBytes = storage.ToCompressedBytes();
                int requiredPartitionCount = ChunkRegionPartitionOperator.CalculateRequiredPartitionCount(compressedBytes.Length);
                if (requiredPartitionCount <= 0)
                {
                    throw new InvalidDataException("压缩后的 chunk 数据为空，无法写入 region。");
                }

                uint[] newPartitionIndices = AllocatePartitionIndices(requiredPartitionCount);
                ushort lastPartitionDataLength = WriteNewChunkChain(newPartitionIndices, compressedBytes);

                Stream.Flush(true);

                ChunkRegionHeaderOperator.WriteChunkHeaderData(
                    Stream,
                    localChunkPosition,
                    newPartitionIndices[0],
                    lastPartitionDataLength,
                    (uint)requiredPartitionCount,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

                Stream.Flush(true);

                RecycleOldChunkChain(oldChunkHeaderData);

                Stream.Flush(true);
            }
            catch (InvalidDataException exception)
            {
                throw new InvalidDataException(
                    $"region 文件 {RegionFilePath} 中 chunk {chunkPosition} 写入失败: {exception.Message}",
                    exception);
            }
        }

        /// <summary>
        /// 为新链分配分区索引。
        /// <para>当空闲分区数量足够时，只使用空闲分区；否则全部在文件尾部新开分区。</para>
        /// </summary>
        private uint[] AllocatePartitionIndices(int requiredPartitionCount)
        {
            ChunkRegionHeaderOperator.FreePartitionState freePartitionState =
                ChunkRegionHeaderOperator.ReadFreePartitionState(Stream);
            if (!ChunkRegionHeaderOperator.TryValidateFreePartitionState(Stream, freePartitionState))
            {
                throw new InvalidDataException("region 文件中的空闲分区状态非法。");
            }

            if (freePartitionState.FreePartitionCount < (uint)requiredPartitionCount)
            {
                return ChunkRegionPartitionOperator.AppendTailPartitions(Stream, requiredPartitionCount);
            }

            uint[] partitionIndices = new uint[requiredPartitionCount];
            HashSet<uint> usedPartitionIndices = new(requiredPartitionCount);
            for (int i = 0; i < requiredPartitionCount; i++)
            {
                uint partitionIndex = ChunkRegionPartitionOperator.TakeFreePartition(Stream, ref freePartitionState);
                if (!usedPartitionIndices.Add(partitionIndex))
                {
                    throw new InvalidDataException("空闲分区链存在循环或重复节点。");
                }

                partitionIndices[i] = partitionIndex;
            }

            ChunkRegionHeaderOperator.WriteFreePartitionState(Stream, freePartitionState);
            return partitionIndices;
        }

        /// <summary>
        /// 将压缩后的区块数据写入新的分区链。
        /// </summary>
        private ushort WriteNewChunkChain(uint[] partitionIndices, byte[] compressedBytes)
        {
            int writtenLength = 0;
            ushort lastPartitionDataLength = 0;
            for (int i = 0; i < partitionIndices.Length; i++)
            {
                uint nextPartitionIndex = i == partitionIndices.Length - 1
                    ? ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL
                    : partitionIndices[i + 1];
                int currentWriteLength = Math.Min(
                    ChunkRegionFileLayout.PARTITION_PAYLOAD_SIZE,
                    compressedBytes.Length - writtenLength);
                if (currentWriteLength <= 0)
                {
                    throw new InvalidDataException("分区数量与压缩数据长度不匹配。");
                }

                ChunkRegionPartitionOperator.WritePartition(
                    Stream,
                    partitionIndices[i],
                    nextPartitionIndex,
                    compressedBytes.AsSpan(writtenLength, currentWriteLength));
                writtenLength += currentWriteLength;
                lastPartitionDataLength = checked((ushort)currentWriteLength);
            }

            if (writtenLength != compressedBytes.Length)
            {
                throw new InvalidDataException("chunk 数据写入结束后仍有剩余字节未落盘。");
            }

            return lastPartitionDataLength;
        }

        /// <summary>
        /// 回收旧的区块分区链，并将其加入空闲分区链。
        /// </summary>
        private void RecycleOldChunkChain(ChunkRegionHeaderOperator.ChunkHeaderData oldChunkHeaderData)
        {
            if (oldChunkHeaderData.IsEmpty)
            {
                return;
            }

            ChunkRegionHeaderOperator.ValidateChunkHeaderData(Stream, oldChunkHeaderData);
            ChunkRegionHeaderOperator.FreePartitionState freePartitionState =
                ChunkRegionHeaderOperator.ReadFreePartitionState(Stream);
            if (!ChunkRegionHeaderOperator.TryValidateFreePartitionState(Stream, freePartitionState))
            {
                throw new InvalidDataException("region 文件中的空闲分区状态非法，无法回收旧链。");
            }

            int partitionCount = checked((int)oldChunkHeaderData.PartitionCount);
            uint currentPartitionIndex = oldChunkHeaderData.FirstPartitionIndex;
            HashSet<uint> visitedPartitionIndices = new(partitionCount);
            for (int i = 0; i < partitionCount; i++)
            {
                if (!visitedPartitionIndices.Add(currentPartitionIndex))
                {
                    throw new InvalidDataException("旧 chunk 分区链存在循环或重复节点。");
                }

                uint nextPartitionIndex = ChunkRegionPartitionOperator.ReadPartitionNextIndex(Stream, currentPartitionIndex);
                ChunkRegionPartitionOperator.RegisterFreePartition(Stream, currentPartitionIndex, ref freePartitionState);

                if (nextPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
                {
                    ChunkRegionHeaderOperator.WriteFreePartitionState(Stream, freePartitionState);
                    return;
                }

                currentPartitionIndex = nextPartitionIndex;
            }

            throw new InvalidDataException("旧 chunk 分区链在达到记录的分区总数后仍未结束。");
        }
    }
}
