using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence.Region
{
    /// <summary>
    /// ChunkRegion 写入器。
    /// <para>该类型基于 ChunkRegionFileAccessor 与静态操作工具，实现区块数据的新链写入、头数据更新与旧链回收流程。</para>
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
        /// 保存指定 chunk 的储存对象。
        /// </summary>
        public bool SaveChunkStorage(ChunkPosition chunkPosition, ChunkDataStorage storage)
        {
            if (storage == null)
            {
                GD.PushError("[ChunkRegionWriter] SaveChunkStorage: storage 不能为空。");
                return false;
            }

            ChunkRegionPositionProcessor.GetRegionAndLocalChunkPosition(
                chunkPosition,
                out Vector2I regionPosition,
                out Vector2I localChunkPosition);
            // 写入器只允许操作自己绑定的 region，避免把局部坐标写进错误文件后造成静默损坏。
            if (regionPosition != RegionPosition)
            {
                GD.PushError(
                    $"[ChunkRegionWriter] SaveChunkStorage: chunkPosition {chunkPosition} 不属于当前 region ({RegionPosition.X}, {RegionPosition.Y})。");
                return false;
            }

            ChunkRegionHeaderOperator.ChunkHeaderData? oldChunkHeaderData =
                ChunkRegionHeaderOperator.ReadChunkHeaderData(Stream, localChunkPosition);
            if (!oldChunkHeaderData.HasValue)
            {
                return false;
            }

            if (!ChunkRegionHeaderOperator.ValidateChunkHeaderData(Stream, oldChunkHeaderData.Value))
            {
                GD.PushError($"[ChunkRegionWriter] SaveChunkStorage: region 文件 {RegionFilePath} 中 chunk {chunkPosition} 旧头数据非法。");
                return false;
            }

            // 先序列化出完整压缩数据，再一次性决定分区需求，避免边写边算导致链结构难以回滚。
            byte[] compressedBytes = storage.ToCompressedBytes();
            int requiredPartitionCount = ChunkRegionPartitionOperator.CalculateRequiredPartitionCount(compressedBytes.Length);
            if (requiredPartitionCount <= 0)
            {
                GD.PushError("[ChunkRegionWriter] SaveChunkStorage: 压缩后的 chunk 数据为空，无法写入 region。");
                return false;
            }

            uint[] newPartitionIndices = AllocatePartitionIndices(requiredPartitionCount);
            if (newPartitionIndices == null || newPartitionIndices.Length != requiredPartitionCount)
            {
                GD.PushError("[ChunkRegionWriter] SaveChunkStorage: 分配新分区链失败。");
                return false;
            }

            if (!WriteNewChunkChain(newPartitionIndices, compressedBytes, out ushort lastPartitionDataLength))
            {
                return false;
            }

            Stream.Flush(true);

            if (!ChunkRegionHeaderOperator.WriteChunkHeaderData(
                    Stream,
                    localChunkPosition,
                    newPartitionIndices[0],
                    lastPartitionDataLength,
                    (uint)requiredPartitionCount,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
            {
                GD.PushError("[ChunkRegionWriter] SaveChunkStorage: 写入 chunk 头数据失败。");
                return false;
            }

            Stream.Flush(true);

            if (!RecycleOldChunkChain(oldChunkHeaderData.Value))
            {
                return false;
            }

            Stream.Flush(true);
            return true;
        }

        /// <summary>
        /// 为新链分配分区索引。
        /// <para>当空闲分区数量足够时，只使用空闲分区；否则全部在文件尾部新开分区。</para>
        /// </summary>
        private uint[] AllocatePartitionIndices(int requiredPartitionCount)
        {
            using ChunkRegionFreePartitionChain freePartitionChain = ChunkRegionFreePartitionChain.Create(Stream);
            if (freePartitionChain == null)
            {
                GD.PushError("[ChunkRegionWriter] AllocatePartitionIndices: region 文件中的空闲分区状态非法。");
                return null;
            }

            if (freePartitionChain.FreePartitionCount < (uint)requiredPartitionCount)
            {
                // 空闲链不够时直接走尾部分配，避免混合“取一部分空闲 + 追加一部分尾部”带来的状态复杂化。
                return ChunkRegionPartitionOperator.AppendTailPartitions(Stream, requiredPartitionCount);
            }

            uint[] partitionIndices = freePartitionChain.GetFreePartitionList(requiredPartitionCount);
            if (partitionIndices == null)
            {
                GD.PushError("[ChunkRegionWriter] AllocatePartitionIndices: 获取空闲分区列表失败。");
                return null;
            }

            if (!freePartitionChain.FlushStateToFile())
            {
                GD.PushError("[ChunkRegionWriter] AllocatePartitionIndices: 写回空闲分区头状态失败。");
                return null;
            }

            return partitionIndices;
        }

        /// <summary>
        /// 将压缩后的区块数据写入新的分区链。
        /// </summary>
        private bool WriteNewChunkChain(uint[] partitionIndices, byte[] compressedBytes, out ushort lastPartitionDataLength)
        {
            int writtenLength = 0;
            lastPartitionDataLength = 0;
            for (int i = 0; i < partitionIndices.Length; i++)
            {
                // 链中的 next 关系在写每个分区时一次性确定，避免后续再做第二轮补链。
                uint nextPartitionIndex = i == partitionIndices.Length - 1
                    ? ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL
                    : partitionIndices[i + 1];
                int currentWriteLength = Math.Min(
                    ChunkRegionFileLayout.PARTITION_PAYLOAD_SIZE,
                    compressedBytes.Length - writtenLength);
                if (currentWriteLength <= 0)
                {
                    GD.PushError("[ChunkRegionWriter] WriteNewChunkChain: 分区数量与压缩数据长度不匹配。");
                    return false;
                }

                if (!ChunkRegionPartitionOperator.WritePartition(
                        Stream,
                        partitionIndices[i],
                        nextPartitionIndex,
                        compressedBytes.AsSpan(writtenLength, currentWriteLength)))
                {
                    GD.PushError("[ChunkRegionWriter] WriteNewChunkChain: 写入 chunk 分区失败。");
                    return false;
                }

                writtenLength += currentWriteLength;
                // 这里只持续覆盖最后一次写入长度，循环结束后自然就是最后分区的有效字节数。
                lastPartitionDataLength = checked((ushort)currentWriteLength);
            }

            if (writtenLength != compressedBytes.Length)
            {
                GD.PushError("[ChunkRegionWriter] WriteNewChunkChain: chunk 数据写入结束后仍有剩余字节未落盘。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 回收旧的区块分区链，并将其加入空闲分区链。
        /// </summary>
        private bool RecycleOldChunkChain(ChunkRegionHeaderOperator.ChunkHeaderData oldChunkHeaderData)
        {
            if (oldChunkHeaderData.IsEmpty)
            {
                return true;
            }

            if (!ChunkRegionHeaderOperator.ValidateChunkHeaderData(Stream, oldChunkHeaderData))
            {
                GD.PushError("[ChunkRegionWriter] RecycleOldChunkChain: 旧 chunk 头数据非法。");
                return false;
            }

            using ChunkRegionFreePartitionChain freePartitionChain = ChunkRegionFreePartitionChain.Create(Stream);
            if (freePartitionChain == null)
            {
                GD.PushError("[ChunkRegionWriter] RecycleOldChunkChain: region 文件中的空闲分区状态非法，无法回收旧链。");
                return false;
            }

            int partitionCount = checked((int)oldChunkHeaderData.PartitionCount);
            uint currentPartitionIndex = oldChunkHeaderData.FirstPartitionIndex;
            HashSet<uint> visitedPartitionIndices = new(partitionCount);
            for (int i = 0; i < partitionCount; i++)
            {
                // 回收流程同样要防环，否则损坏旧链会把空闲链写坏得更严重。
                if (!visitedPartitionIndices.Add(currentPartitionIndex))
                {
                    GD.PushError("[ChunkRegionWriter] RecycleOldChunkChain: 旧 chunk 分区链存在循环或重复节点。");
                    return false;
                }

                // 先读出旧 next，再把当前分区挂入空闲链，否则当前分区的 next 会被注册逻辑覆盖掉。
                if (!ChunkRegionPartitionOperator.TryReadValidatedNextPartitionIndex(Stream, currentPartitionIndex, out uint nextPartitionIndex))
                {
                    GD.PushError("[ChunkRegionWriter] RecycleOldChunkChain: 读取旧 chunk 分区链 next 索引失败。");
                    return false;
                }

                ChunkRegionHeaderOperator.FreePartitionState oldFreePartitionState = freePartitionChain.FreePartitionState;
                ChunkRegionHeaderOperator.FreePartitionState newFreePartitionState = freePartitionChain.RegisterHeadPartition(currentPartitionIndex);
                if (newFreePartitionState.HeadFreePartitionIndex == oldFreePartitionState.HeadFreePartitionIndex &&
                    newFreePartitionState.FreePartitionCount == oldFreePartitionState.FreePartitionCount)
                {
                    GD.PushError("[ChunkRegionWriter] RecycleOldChunkChain: 将旧分区注册到空闲分区链失败。");
                    return false;
                }

                if (nextPartitionIndex == ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL)
                {
                    return freePartitionChain.FlushStateToFile();
                }

                currentPartitionIndex = nextPartitionIndex;
            }

            GD.PushError("[ChunkRegionWriter] RecycleOldChunkChain: 旧 chunk 分区链在达到记录的分区总数后仍未结束。");
            return false;
        }
    }
}