import { PARTITION_ENTRY_SIZE, REGION_CHUNK_COUNT } from "./region_layout.ts";
import type {
  PartitionCountDistributionEntry,
  RegionAnalysis,
  RegionMetrics,
  RegionParseResult,
} from "./region_types.ts";

// 避免分母为 0 时出现 NaN，便于渲染层稳定展示。
function safeRatio(numerator: number, denominator: number): number {
  if (denominator <= 0) {
    return 0;
  }
  return numerator / denominator;
}

// 把解析层结果折叠成最终统计指标。
// 这里的口径与 README 中定义保持一致：
// 1. 实际数据只统计健康活动链
// 2. 前缀无效数据固定为 32 KiB
// 3. 分区无效数据 = 分区区总大小 - 实际数据
export function analyzeRegion(parse: RegionParseResult): RegionAnalysis {
  const declaredNonEmptyChunkCount = parse.chunks.filter((chunk) => !chunk.header.isEmpty).length;
  const loadedChunks = parse.chunks.filter(
    (chunk) => !chunk.header.isEmpty && chunk.structuralErrors.length === 0 && chunk.chain && chunk.chain.errors.length === 0,
  );
  const partitionCountDistributionMap = new Map<number, number>();
  for (const chunk of loadedChunks) {
    partitionCountDistributionMap.set(
      chunk.header.partitionCount,
      (partitionCountDistributionMap.get(chunk.header.partitionCount) ?? 0) + 1,
    );
  }
  const partitionCountDistribution: PartitionCountDistributionEntry[] = [...partitionCountDistributionMap.entries()]
    .sort((left, right) => left[0] - right[0])
    .map(([partitionCount, chunkCount]) => ({
      partitionCount,
      chunkCount,
      ratioOfLoadedChunks: safeRatio(chunkCount, loadedChunks.length),
    }));

  const actualDataBytes = loadedChunks.reduce((sum, chunk) => sum + (chunk.chain?.actualDataBytes ?? 0), 0);
  const writtenPartitionUnwrittenBytes = loadedChunks.reduce(
    (sum, chunk) => sum + (chunk.chain?.unwrittenPayloadBytes ?? 0),
    0,
  );
  const activeNextPointerBytes = loadedChunks.reduce((sum, chunk) => sum + (chunk.chain?.nextPointerBytes ?? 0), 0);
  const activePartitionCount = loadedChunks.reduce((sum, chunk) => sum + chunk.header.partitionCount, 0);

  const freePartitionCount = parse.freeChain.indices.length;
  const freePartitionBytes = freePartitionCount * PARTITION_ENTRY_SIZE;

  const totalOccupiedBytes = parse.fileSize;
  const partitionAreaBytes = Math.max(0, totalOccupiedBytes - parse.prefixInvalidBytes);
  const partitionInvalidBytes = Math.max(0, partitionAreaBytes - actualDataBytes);
  const totalInvalidBytes = Math.max(0, totalOccupiedBytes - actualDataBytes);

  // 如果健康文件下这部分不为 0，通常意味着孤儿分区、泄漏分区或结构重叠。
  const orphanPartitionBytes = Math.max(
    0,
    partitionInvalidBytes - freePartitionBytes - writtenPartitionUnwrittenBytes - activeNextPointerBytes,
  );
  const orphanPartitionCount = Math.floor(orphanPartitionBytes / PARTITION_ENTRY_SIZE);

  const metrics: RegionMetrics = {
    loadedChunkCount: loadedChunks.length,
    totalChunkCapacity: REGION_CHUNK_COUNT,
    loadedChunkCapacityRatio: safeRatio(loadedChunks.length, REGION_CHUNK_COUNT),
    declaredNonEmptyChunkCount,
    totalOccupiedBytes,
    partitionAreaBytes,
    actualDataBytes,
    totalInvalidBytes,
    prefixInvalidBytes: parse.prefixInvalidBytes,
    partitionInvalidBytes,
    writtenPartitionUnwrittenBytes,
    freePartitionBytes,
    activeNextPointerBytes,
    orphanPartitionBytes,
    allocatedPartitionCount: parse.allocatedPartitionCount,
    activePartitionCount,
    freePartitionCount,
    orphanPartitionCount,
    invalidRatioOfTotal: safeRatio(totalInvalidBytes, totalOccupiedBytes),
    actualRatioOfTotal: safeRatio(actualDataBytes, totalOccupiedBytes),
    partitionInvalidRatio: safeRatio(partitionInvalidBytes, partitionAreaBytes),
    partitionValidRatio: safeRatio(actualDataBytes, partitionAreaBytes),
    freeShareOfPartitionInvalid: safeRatio(freePartitionBytes, partitionInvalidBytes),
    unwrittenShareOfPartitionInvalid: safeRatio(writtenPartitionUnwrittenBytes, partitionInvalidBytes),
    partitionCountDistribution,
  };

  return {
    parse,
    metrics,
  };
}
