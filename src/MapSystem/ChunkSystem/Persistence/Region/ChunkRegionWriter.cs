using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;
using WorldWeaver.MapSystem.ChunkSystem.Persistence.Region.InfoOperator;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence.Region
{
    /// <summary>
    /// ChunkRegion 写入器。唯一公开入口为 <see cref="StoreChunkStorageGroup"/>，
    /// 空闲分区链随 using 作用域自动释放。
    /// </summary>
    public sealed class ChunkRegionWriter : ChunkRegionFileAccessor
    {
        private ChunkRegionWriter(string regionFilePath, Vector2I regionPosition, FileStream stream)
            : base(regionFilePath, regionPosition, stream)
        {
        }

        public new static ChunkRegionWriter Open(string rootPath, Vector2I regionPosition)
        {
            if (!TryOpenValidatedStream(rootPath, regionPosition, System.IO.FileAccess.ReadWrite, out string regionFilePath, out FileStream stream))
            {
                return null;
            }

            return new ChunkRegionWriter(regionFilePath, regionPosition, stream);
        }

        /// <summary>
        /// 以组为单位储存多个 chunk。空闲分区链在 using 作用域内分配与回收，最后一次性 Flush。
        /// </summary>
        public bool StoreChunkStorageGroup(IReadOnlyList<(ChunkPosition ChunkPosition, ChunkDataStorage Storage)> writeItems)
        {
            if (writeItems == null)
            {
                GD.PushError("[ChunkRegionWriter] StoreChunkStorageGroup: writeItems 不能为空。");
                return false;
            }

            using ChunkRegionFreePartitionChain freePartitionChain = new(Stream);

            foreach ((ChunkPosition chunkPosition, ChunkDataStorage storage) in writeItems)
            {
                if (!WriteOneChunk(chunkPosition, storage, freePartitionChain))
                {
                    return false;
                }
            }

            return true;
        }

        // ================================================================================
        //                              单 chunk 写入
        // ================================================================================

        /// <summary>
        /// 写入单个 chunk：校验 → 压缩 → 分配分区 → 写新链 → 更新头 → 回收旧链。
        /// <paramref name="freePartitionChain"/> 由外层 StoreChunkStorageGroup 持有，此处不负责生命周期。
        /// </summary>
        private bool WriteOneChunk(ChunkPosition chunkPosition, ChunkDataStorage storage, ChunkRegionFreePartitionChain freePartitionChain)
        {
            if (storage == null)
            {
                GD.PushError("[ChunkRegionWriter] WriteOneChunk: storage 不能为空。");
                return false;
            }

            ChunkRegionPositionProcessor.GetRegionAndLocalChunkPosition(
                chunkPosition,
                out Vector2I regionPosition,
                out Vector2I localChunkPosition);
            if (regionPosition != RegionPosition)
            {
                GD.PushError(
                    $"[ChunkRegionWriter] WriteOneChunk: chunk {chunkPosition} 不属于当前 region ({RegionPosition.X}, {RegionPosition.Y})。");
                return false;
            }

            // 读旧头记录
            ChunkHeaderData? oldHeader =
                ChunkRegionHeaderOperator.ReadChunkHeaderData(Stream, localChunkPosition);
            if (!oldHeader.HasValue)
            {
                return false;
            }

            if (!ChunkRegionHeaderOperator.ValidateChunkHeaderData(Stream, oldHeader.Value))
            {
                GD.PushError($"[ChunkRegionWriter] WriteOneChunk: chunk {chunkPosition} 旧头数据非法。");
                return false;
            }

            // 压缩 → 分配分区（空闲链优先，不够则尾部追加）
            byte[] compressedBytes = storage.ToCompressedBytes();
            int requiredPartitionCount = ChunkRegionPartitionOperator.CalculateRequiredPartitionCount(compressedBytes.Length);
            if (requiredPartitionCount <= 0)
            {
                GD.PushError("[ChunkRegionWriter] WriteOneChunk: 压缩后的 chunk 数据为空。");
                return false;
            }

            uint[] newPartitionIndices = freePartitionChain.Count >= (uint)requiredPartitionCount
                ? freePartitionChain - requiredPartitionCount
                : ChunkRegionPartitionOperator.AppendTailPartitions(Stream, requiredPartitionCount);
            if (newPartitionIndices == null || newPartitionIndices.Length != requiredPartitionCount)
            {
                GD.PushError("[ChunkRegionWriter] WriteOneChunk: 分配新分区链失败。");
                return false;
            }

            // 写新分区链
            if (!WriteChunkPartitionChain(newPartitionIndices, compressedBytes, out ushort lastPartitionDataLength))
            {
                return false;
            }

            Stream.Flush(true);

            // 更新头记录
            if (!ChunkRegionHeaderOperator.WriteChunkHeaderData(
                    Stream,
                    localChunkPosition,
                    newPartitionIndices[0],
                    lastPartitionDataLength,
                    (uint)requiredPartitionCount,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
            {
                GD.PushError("[ChunkRegionWriter] WriteOneChunk: 写入 chunk 头数据失败。");
                return false;
            }

            Stream.Flush(true);

            // 回收旧链
            if (!oldHeader.Value.IsEmpty)
            {
                if (!RecycleChunkChain(oldHeader.Value, freePartitionChain))
                {
                    return false;
                }

                Stream.Flush(true);
            }

            return true;
        }

        // ================================================================================
        //                              分区链写入
        // ================================================================================

        /// <summary>
        /// 将压缩数据按分区索引数组写入新链，同时设置 next 指针。
        /// </summary>
        private bool WriteChunkPartitionChain(uint[] partitionIndices, byte[] compressedBytes, out ushort lastPartitionDataLength)
        {
            int writtenLength = 0;
            lastPartitionDataLength = 0;
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
                    GD.PushError("[ChunkRegionWriter] WriteChunkPartitionChain: 分区数量与压缩数据长度不匹配。");
                    return false;
                }

                if (!ChunkRegionPartitionOperator.WritePartition(
                        Stream,
                        partitionIndices[i],
                        nextPartitionIndex,
                        compressedBytes.AsSpan(writtenLength, currentWriteLength)))
                {
                    GD.PushError("[ChunkRegionWriter] WriteChunkPartitionChain: 写入 chunk 分区失败。");
                    return false;
                }

                writtenLength += currentWriteLength;
                lastPartitionDataLength = checked((ushort)currentWriteLength);
            }

            if (writtenLength != compressedBytes.Length)
            {
                GD.PushError("[ChunkRegionWriter] WriteChunkPartitionChain: 写入结束后仍有剩余字节未落盘。");
                return false;
            }

            return true;
        }

        // ================================================================================
        //                              旧链回收
        // ================================================================================

        /// <summary>
        /// 遍历旧链的每个分区，逐个注册到空闲分区链头部。
        /// </summary>
        private bool RecycleChunkChain(ChunkHeaderData oldHeader, ChunkRegionFreePartitionChain freePartitionChain)
        {
            int partitionCount = checked((int)oldHeader.PartitionCount);
            uint currentPartitionIndex = oldHeader.FirstPartitionIndex;
            HashSet<uint> visitedPartitionIndices = new(partitionCount);
            for (int i = 0; i < partitionCount; i++)
            {
                if (!visitedPartitionIndices.Add(currentPartitionIndex))
                {
                    GD.PushError("[ChunkRegionWriter] RecycleChunkChain: 旧分区链存在循环或重复节点。");
                    return false;
                }

                if (!ChunkRegionPartitionOperator.TryReadValidatedNextPartitionIndex(
                        Stream, currentPartitionIndex, out uint nextPartitionIndex))
                {
                    GD.PushError("[ChunkRegionWriter] RecycleChunkChain: 读取旧分区链 next 索引失败。");
                    return false;
                }

                _ = freePartitionChain + currentPartitionIndex;

                if (nextPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
                {
                    return true;
                }

                currentPartitionIndex = nextPartitionIndex;
            }

            GD.PushError("[ChunkRegionWriter] RecycleChunkChain: 旧分区链在达到记录的分区总数后仍未结束。");
            return false;
        }
    }
}
