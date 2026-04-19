// 单个 chunk header 的解析结果。
export interface ChunkHeaderData {
  firstPartitionIndex: number;
  lastPartitionDataLength: number;
  partitionCount: number;
  timestamp: number;
  isEmpty: boolean;
}

// 文件头记录的空闲链全局状态。
export interface FreePartitionState {
  headFreePartitionIndex: number;
  freePartitionCount: number;
}

export type RegionIssueScope = {
  kind: "chunk";
  localX: number;
  localY: number;
};

export type RegionIssueParamValue = string | number | boolean | null;

// 解析阶段统一产出的结构化告警，渲染层再按语言格式化。
export interface RegionIssue {
  code: string;
  params?: Record<string, RegionIssueParamValue>;
  scope?: RegionIssueScope;
}

// 一条 chunk 链遍历后的统计结果。
export interface ChainTraversal {
  indices: number[];
  actualDataBytes: number;
  unwrittenPayloadBytes: number;
  nextPointerBytes: number;
  terminatedWithSentinel: boolean;
  errors: RegionIssue[];
}

// 单个 chunk 槽位的完整解析结果。
export interface ParsedChunkRecord {
  chunkIndex: number;
  localX: number;
  localY: number;
  header: ChunkHeaderData;
  structuralErrors: RegionIssue[];
  chain?: ChainTraversal;
}

// 空闲链遍历结果与链条健康状态。
export interface FreeChainInfo {
  state: FreePartitionState;
  indices: number[];
  errors: RegionIssue[];
  terminatedWithSentinel: boolean;
  cyclic: boolean;
}

// region 文件解析层输出。
export interface RegionParseResult {
  path: string;
  fileSize: number;
  allocatedPartitionCount: number;
  prefixInvalidBytes: number;
  formatOk: boolean;
  signatureOk: boolean;
  alignmentOk: boolean;
  createTimestamp: number | null;
  freeChain: FreeChainInfo;
  chunks: ParsedChunkRecord[];
  findings: RegionIssue[];
}

// “partitionCount = N”的区块分布统计。
export interface PartitionCountDistributionEntry {
  partitionCount: number;
  chunkCount: number;
  ratioOfLoadedChunks: number;
}

// 面向用户展示的最终统计指标。
export interface RegionMetrics {
  loadedChunkCount: number;
  totalChunkCapacity: number;
  loadedChunkCapacityRatio: number;
  declaredNonEmptyChunkCount: number;
  totalOccupiedBytes: number;
  partitionAreaBytes: number;
  actualDataBytes: number;
  totalInvalidBytes: number;
  prefixInvalidBytes: number;
  partitionInvalidBytes: number;
  writtenPartitionUnwrittenBytes: number;
  freePartitionBytes: number;
  activeNextPointerBytes: number;
  orphanPartitionBytes: number;
  allocatedPartitionCount: number;
  activePartitionCount: number;
  freePartitionCount: number;
  orphanPartitionCount: number;
  invalidRatioOfTotal: number;
  actualRatioOfTotal: number;
  partitionInvalidRatio: number;
  partitionValidRatio: number;
  freeShareOfPartitionInvalid: number;
  unwrittenShareOfPartitionInvalid: number;
  partitionCountDistribution: PartitionCountDistributionEntry[];
}

// 解析结果 + 指标结果的组合对象。
export interface RegionAnalysis {
  parse: RegionParseResult;
  metrics: RegionMetrics;
}

// 文件扫描选项。
export interface ScanOptions {
  recursive: boolean;
}

// CLI 入口统一参数。
export interface CliOptions {
  inputPath: string;
  lang: string;
  recursive: boolean;
  json: boolean;
  maxChainPreview: number;
}
