using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// ChunkRegion 读取器。
    /// <para>该类型基于 ChunkRegionFileAccessor 与静态操作工具，实现单个区块储存对象的读取流程。</para>
    /// </summary>
    public sealed class ChunkRegionReader : ChunkRegionFileAccessor
    {
        /// <summary>
        /// 仅允许通过 Open 创建读取器实例。
        /// </summary>
        private ChunkRegionReader(string regionFilePath, Vector2I regionPosition, FileStream stream)
            : base(regionFilePath, regionPosition, stream)
        {
        }

        /// <summary>
        /// 打开指定 rootPath 下的 region 读取器。
        /// <para>若文件不存在、路径不匹配或格式校验失败，则返回 null 并输出警告。</para>
        /// </summary>
        public new static ChunkRegionReader Open(string rootPath, Vector2I regionPosition)
        {
            if (!TryOpenValidatedStream(rootPath, regionPosition, System.IO.FileAccess.Read, out string regionFilePath, out FileStream stream))
            {
                return null;
            }

            return new ChunkRegionReader(regionFilePath, regionPosition, stream);
        }

        /// <summary>
        /// 读取指定 chunk 的储存对象。
        /// </summary>
        public ChunkDataStorage LoadChunkStorage(ChunkPosition chunkPosition)
        {
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
                ChunkRegionHeaderOperator.ChunkHeaderData chunkHeaderData =
                    ChunkRegionHeaderOperator.ReadChunkHeaderData(Stream, localChunkPosition);
                ChunkRegionHeaderOperator.ValidateChunkHeaderData(Stream, chunkHeaderData);
                if (chunkHeaderData.IsEmpty)
                {
                    return null;
                }

                byte[] compressedBytes = ReadChunkCompressedBytes(chunkHeaderData);
                ChunkDataStorage storage = ChunkDataStorage.FromCompressedBytes(compressedBytes);
                if (storage == null)
                {
                    throw new InvalidDataException("压缩数据无法还原为 ChunkDataStorage。");
                }

                return storage;
            }
            catch (InvalidDataException exception)
            {
                throw new InvalidDataException(
                    $"region 文件 {RegionFilePath} 中 chunk {chunkPosition} 读取失败: {exception.Message}",
                    exception);
            }
        }

        /// <summary>
        /// 根据头数据读取整条分区链中的压缩字节。
        /// </summary>
        private byte[] ReadChunkCompressedBytes(ChunkRegionHeaderOperator.ChunkHeaderData chunkHeaderData)
        {
            int partitionCount = checked((int)chunkHeaderData.PartitionCount);
            int totalCompressedLength = checked(
                (partitionCount - 1) * ChunkRegionFileLayout.PARTITION_PAYLOAD_SIZE + chunkHeaderData.LastPartitionDataLength);
            byte[] compressedBytes = new byte[totalCompressedLength];

            uint currentPartitionIndex = chunkHeaderData.FirstPartitionIndex;
            int copiedLength = 0;
            HashSet<uint> visitedPartitionIndices = new(partitionCount);
            for (int i = 0; i < partitionCount; i++)
            {
                if (!visitedPartitionIndices.Add(currentPartitionIndex))
                {
                    throw new InvalidDataException("chunk 分区链存在循环或重复节点。");
                }

                uint nextPartitionIndex = ChunkRegionPartitionOperator.ReadPartitionNextIndex(Stream, currentPartitionIndex);
                int currentReadLength = i == partitionCount - 1
                    ? chunkHeaderData.LastPartitionDataLength
                    : ChunkRegionFileLayout.PARTITION_PAYLOAD_SIZE;
                ChunkRegionPartitionOperator.ReadPartitionPayload(Stream, currentPartitionIndex, currentReadLength)
                    .AsSpan()
                    .CopyTo(compressedBytes.AsSpan(copiedLength, currentReadLength));
                copiedLength += currentReadLength;

                if (i == partitionCount - 1)
                {
                    if (nextPartitionIndex != ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
                    {
                        throw new InvalidDataException("chunk 分区链的最后一个分区未写入哨兵值。");
                    }

                    break;
                }

                if (nextPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
                {
                    throw new InvalidDataException("chunk 分区链在中途提前结束。");
                }

                currentPartitionIndex = nextPartitionIndex;
            }

            return compressedBytes;
        }
    }
}
