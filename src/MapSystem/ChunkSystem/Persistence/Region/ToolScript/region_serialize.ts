import type { RegionAnalysis } from "./region_types.ts";

// JSON 导出时主动裁掉 1024 个空 chunk 槽位，只保留非空 chunk，避免输出失控。
export function toSerializableAnalysis(analysis: RegionAnalysis): object {
  return {
    parse: {
      path: analysis.parse.path,
      fileSize: analysis.parse.fileSize,
      allocatedPartitionCount: analysis.parse.allocatedPartitionCount,
      prefixInvalidBytes: analysis.parse.prefixInvalidBytes,
      formatOk: analysis.parse.formatOk,
      signatureOk: analysis.parse.signatureOk,
      alignmentOk: analysis.parse.alignmentOk,
      createTimestamp: analysis.parse.createTimestamp,
      freeChain: analysis.parse.freeChain,
      nonEmptyChunks: analysis.parse.chunks.filter((chunk) => !chunk.header.isEmpty),
      findings: analysis.parse.findings,
    },
    metrics: analysis.metrics,
  };
}
