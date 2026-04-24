using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;
using WorldWeaver.MapSystem.ChunkSystem.Persistence.Region.InfoOperator;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence.Region
{
    /// <summary>
    /// ChunkRegion 读取器。
    /// <para>该类型基于 ChunkRegionFileAccessor 与静态操作工具，实现单个区块储存对象的读取流程。</para>
    /// </summary>
    public sealed class ChunkRegionReader : ChunkRegionFileAccessor
    {
        private ChunkRegionReader(string regionFilePath, Vector2I regionPosition, FileStream stream)
            : base(regionFilePath, regionPosition, stream)
        {
        }

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
        public bool LoadChunkStorage(ChunkPosition chunkPosition, out ChunkDataStorage storage)
        {
            storage = null;
            ChunkRegionPositionProcessor.GetRegionAndLocalChunkPosition(
                chunkPosition,
                out Vector2I regionPosition,
                out Vector2I localChunkPosition);
            // 读取器一旦绑定到某个 region，就拒绝跨 region 坐标，避免误读到错误文件中的同索引头记录。
            if (regionPosition != RegionPosition)
            {
                GD.PushError(
                    $"[ChunkRegionReader] LoadChunkStorage: chunkPosition {chunkPosition} 不属于当前 region ({RegionPosition.X}, {RegionPosition.Y})。");
                return false;
            }

            ChunkRegionHeaderOperator.ChunkHeaderData? chunkHeaderData =
                ChunkRegionHeaderOperator.ReadChunkHeaderData(Stream, localChunkPosition);
            if (!chunkHeaderData.HasValue)
            {
                return false;
            }

            if (!ChunkRegionHeaderOperator.ValidateChunkHeaderData(Stream, chunkHeaderData.Value))
            {
                GD.PushError($"[ChunkRegionReader] LoadChunkStorage: region 文件 {RegionFilePath} 中 chunk {chunkPosition} 头数据非法。");
                return false;
            }

            if (chunkHeaderData.Value.IsEmpty)
            {
                return true;
            }

            byte[] compressedBytes = ReadChunkCompressedBytes(chunkHeaderData.Value);
            if (compressedBytes == null)
            {
                GD.PushError($"[ChunkRegionReader] LoadChunkStorage: region 文件 {RegionFilePath} 中 chunk {chunkPosition} 分区链读取失败。");
                return false;
            }

            storage = ChunkDataStorage.FromCompressedBytes(compressedBytes);
            if (storage == null)
            {
                GD.PushError($"[ChunkRegionReader] LoadChunkStorage: region 文件 {RegionFilePath} 中 chunk {chunkPosition} 压缩数据无法还原为 ChunkDataStorage。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 根据头数据读取整条分区链中的压缩字节。
        /// </summary>
        private byte[] ReadChunkCompressedBytes(ChunkRegionHeaderOperator.ChunkHeaderData chunkHeaderData)
        {
            int partitionCount = checked((int)chunkHeaderData.PartitionCount);
            // 总长度由“前 N-1 个满载分区 + 最后一个分区有效长度”推导，避免逐分区累加时重复分配。
            int totalCompressedLength = checked(
                (partitionCount - 1) * ChunkRegionFileLayout.PARTITION_PAYLOAD_SIZE + chunkHeaderData.LastPartitionDataLength);
            byte[] compressedBytes = new byte[totalCompressedLength];

            uint currentPartitionIndex = chunkHeaderData.FirstPartitionIndex;
            int copiedLength = 0;
            HashSet<uint> visitedPartitionIndices = new(partitionCount);
            for (int i = 0; i < partitionCount; i++)
            {
                // 读链阶段必须主动防环，否则损坏文件会把读取流程卡死在循环分区上。
                if (!visitedPartitionIndices.Add(currentPartitionIndex))
                {
                    GD.PushError("[ChunkRegionReader] ReadChunkCompressedBytes: chunk 分区链存在循环或重复节点。");
                    return null;
                }

                if (!ChunkRegionPartitionOperator.TryReadValidatedNextPartitionIndex(Stream, currentPartitionIndex, out uint nextPartitionIndex))
                {
                    GD.PushError("[ChunkRegionReader] ReadChunkCompressedBytes: 读取 chunk 分区链 next 索引失败。");
                    return null;
                }

                int currentReadLength = i == partitionCount - 1
                    ? chunkHeaderData.LastPartitionDataLength
                    : ChunkRegionFileLayout.PARTITION_PAYLOAD_SIZE;
                if (!ChunkRegionPartitionOperator.TryReadPartitionPayload(Stream, currentPartitionIndex, currentReadLength, out byte[] payloadBytes))
                {
                    GD.PushError("[ChunkRegionReader] ReadChunkCompressedBytes: 读取 chunk 分区 payload 失败。");
                    return null;
                }

                payloadBytes.AsSpan().CopyTo(compressedBytes.AsSpan(copiedLength, currentReadLength));
                copiedLength += currentReadLength;

                if (i == partitionCount - 1)
                {
                    // 最后一个分区必须以哨兵值结束，否则说明头记录声明的链长度与实际链尾不一致。
                    if (nextPartitionIndex != ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
                    {
                        GD.PushError("[ChunkRegionReader] ReadChunkCompressedBytes: chunk 分区链的最后一个分区未写入哨兵值。");
                        return null;
                    }

                    break;
                }

                if (nextPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
                {
                    // 非最后节点提前遇到哨兵，说明链条比头记录中声明的分区数量更短。
                    GD.PushError("[ChunkRegionReader] ReadChunkCompressedBytes: chunk 分区链在中途提前结束。");
                    return null;
                }

                currentPartitionIndex = nextPartitionIndex;
            }

            return compressedBytes;
        }
    }
}