import { readdirSync, readFileSync, statSync } from "node:fs";
import { basename, extname, join, resolve } from "node:path";
import {
  CHUNK_DATA_ENTRY_SIZE,
  CREATE_TIME_PREFIX,
  FILE_EXTENSION,
  FORMAT_AREA_OFFSET_IN_FILE,
  FORMAT_AREA_SIZE,
  FREE_PARTITION_COUNT_OFFSET_IN_FILE,
  HEAD_FREE_PARTITION_INDEX_OFFSET_IN_FILE,
  INTRODUCTION_AREA_OFFSET_IN_FILE,
  INTRODUCTION_AREA_SIZE,
  INTRODUCTION_SIGNATURE,
  PARTITION_AREA_OFFSET_IN_FILE,
  PARTITION_ENTRY_SIZE,
  PARTITION_INDEX_SENTINEL,
  PARTITION_PAYLOAD_SIZE,
  PREFIX_INVALID_BYTES,
  REGION_CHUNK_COUNT,
  STANDARD_FORMAT,
  chunkIndexToLocalPosition,
  getChunkDataOffsetInFile,
  getPartitionNextOffsetInFile,
} from "./region_layout.ts";
import type {
  ChainTraversal,
  ChunkHeaderData,
  FreeChainInfo,
  FreePartitionState,
  ParsedChunkRecord,
  RegionIssue,
  RegionParseResult,
  ScanOptions,
} from "./region_types.ts";

function createIssue(
  code: string,
  params?: Record<string, string | number | boolean | null>,
  scope?: RegionIssue["scope"],
): RegionIssue {
  return { code, params, scope };
}

// region 文件整体按二进制读取，只有文本区段再显式按 UTF-8 解码。
const UTF8_DECODER = new TextDecoder("utf-8");

// format 区是固定长度预留区，末尾 0 只是填充，需要先裁掉。
function trimTrailingZeros(buffer: Buffer): Buffer {
  let end = buffer.length;
  while (end > 0 && buffer[end - 1] === 0) {
    end -= 1;
  }
  return buffer.subarray(0, end);
}

// 所有文本区段都显式按 UTF-8 解释，避免依赖默认编码。
function decodeUtf8(buffer: Buffer): string {
  return UTF8_DECODER.decode(buffer);
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

// 按“标准格式必须被完整覆盖”的规则递归校验 JSON 结构。
function matchStandardToken(standardToken: unknown, candidateToken: unknown, tokenPath: string): RegionIssue | null {
  if (Array.isArray(standardToken)) {
    if (!Array.isArray(candidateToken)) {
      return createIssue("format_type_mismatch", { path: tokenPath });
    }
    if (standardToken.length !== candidateToken.length) {
      return createIssue("format_array_length_mismatch", { path: tokenPath });
    }
    for (let index = 0; index < standardToken.length; index += 1) {
      const error = matchStandardToken(standardToken[index], candidateToken[index], `${tokenPath}[${index}]`);
      if (error) {
        return error;
      }
    }
    return null;
  }

  if (isPlainObject(standardToken)) {
    if (!isPlainObject(candidateToken)) {
      return createIssue("format_type_mismatch", { path: tokenPath });
    }
    for (const [key, value] of Object.entries(standardToken)) {
      if (!(key in candidateToken)) {
        return createIssue("format_missing_field", { path: `${tokenPath}.${key}` });
      }
      const error = matchStandardToken(value, candidateToken[key], `${tokenPath}.${key}`);
      if (error) {
        return error;
      }
    }
    return null;
  }

  return Object.is(standardToken, candidateToken) ? null : createIssue("format_value_mismatch", { path: tokenPath });
}

// 校验 format 区，确保文件布局语义与 C# 运行时一致。
function validateFormatArea(buffer: Buffer): RegionIssue | null {
  const trimmed = trimTrailingZeros(buffer);
  if (trimmed.length === 0) {
    return createIssue("format_area_empty");
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(decodeUtf8(trimmed));
  } catch (error) {
    return createIssue("format_json_parse_failed", { reason: (error as Error).message });
  }

  return matchStandardToken(STANDARD_FORMAT, parsed, "format");
}

function readU32(buffer: Buffer, offset: number): number {
  return buffer.readUInt32LE(offset);
}

function readU16(buffer: Buffer, offset: number): number {
  return buffer.readUInt16LE(offset);
}

function readI64(buffer: Buffer, offset: number): number {
  return Number(buffer.readBigInt64LE(offset));
}

// 按固定 18 字节结构读取单个 chunk header。
function readChunkHeaderData(fileBuffer: Buffer, chunkIndex: number): ChunkHeaderData {
  const offset = getChunkDataOffsetInFile(chunkIndex);
  const firstPartitionIndex = readU32(fileBuffer, offset);
  const lastPartitionDataLength = readU16(fileBuffer, offset + 4);
  const partitionCount = readU32(fileBuffer, offset + 6);
  const timestamp = readI64(fileBuffer, offset + 10);
  const isEmpty =
    firstPartitionIndex === PARTITION_INDEX_SENTINEL &&
    lastPartitionDataLength === 0 &&
    partitionCount === 0;

  return {
    firstPartitionIndex,
    lastPartitionDataLength,
    partitionCount,
    timestamp,
    isEmpty,
  };
}

// 这部分直接镜像 ChunkRegionHeaderOperator 的基本合法性约束。
function validateChunkHeaderData(header: ChunkHeaderData, allocatedPartitionCount: number): RegionIssue[] {
  if (header.isEmpty) {
    return [];
  }

  const errors: RegionIssue[] = [];
  if (header.firstPartitionIndex === PARTITION_INDEX_SENTINEL) {
    errors.push(createIssue("non_empty_first_partition_is_sentinel"));
  }
  if (header.partitionCount === 0) {
    errors.push(createIssue("non_empty_partition_count_zero"));
  }
  if (header.lastPartitionDataLength <= 0 || header.lastPartitionDataLength > PARTITION_PAYLOAD_SIZE) {
    errors.push(createIssue("last_partition_data_length_out_of_range"));
  }
  if (header.firstPartitionIndex >= allocatedPartitionCount) {
    errors.push(createIssue("first_partition_index_out_of_range"));
  }
  if (header.partitionCount > allocatedPartitionCount) {
    errors.push(createIssue("partition_count_exceeds_allocated"));
  }
  return errors;
}

// 空闲链头状态是否合法，只做头状态级别校验，不遍历链本身。
function isFreePartitionStateValid(state: FreePartitionState, allocatedPartitionCount: number): boolean {
  if (state.headFreePartitionIndex === PARTITION_INDEX_SENTINEL) {
    return state.freePartitionCount === 0;
  }
  if (state.freePartitionCount === 0) {
    return false;
  }
  if (state.freePartitionCount > allocatedPartitionCount) {
    return false;
  }
  return state.headFreePartitionIndex < allocatedPartitionCount;
}

// 已分配分区总数完全由文件长度反推。
function getAllocatedPartitionCount(fileSize: number): number {
  if (fileSize < PARTITION_AREA_OFFSET_IN_FILE) {
    return 0;
  }
  return Math.floor((fileSize - PARTITION_AREA_OFFSET_IN_FILE) / PARTITION_ENTRY_SIZE);
}

// 读取 partition.next 原始值；越界时返回 null。
function readRawPartitionNextIndex(fileBuffer: Buffer, fileSize: number, partitionIndex: number): number | null {
  const allocatedPartitionCount = getAllocatedPartitionCount(fileSize);
  if (partitionIndex < 0 || partitionIndex >= allocatedPartitionCount) {
    return null;
  }
  return readU32(fileBuffer, getPartitionNextOffsetInFile(partitionIndex));
}

// 依据 header 声明遍历一条活动 chunk 链，并统计有效数据、未写满空间与 next 开销。
function traverseChunkChain(fileBuffer: Buffer, fileSize: number, header: ChunkHeaderData): ChainTraversal {
  const errors: RegionIssue[] = [];
  const indices: number[] = [];
  const visited = new Set<number>();
  let currentPartitionIndex = header.firstPartitionIndex;
  let terminatedWithSentinel = false;

  for (let index = 0; index < header.partitionCount; index += 1) {
    if (visited.has(currentPartitionIndex)) {
      errors.push(createIssue("chunk_chain_duplicate_partition", { partitionIndex: currentPartitionIndex }));
      break;
    }
    visited.add(currentPartitionIndex);
    indices.push(currentPartitionIndex);

    const nextPartitionIndex = readRawPartitionNextIndex(fileBuffer, fileSize, currentPartitionIndex);
    if (nextPartitionIndex === null) {
      errors.push(createIssue("chunk_chain_cannot_read_next", { partitionIndex: currentPartitionIndex }));
      break;
    }

    const isLast = index === header.partitionCount - 1;
    if (isLast) {
      if (nextPartitionIndex !== PARTITION_INDEX_SENTINEL) {
        errors.push(createIssue("chunk_chain_not_terminated"));
      } else {
        terminatedWithSentinel = true;
      }
      break;
    }

    if (nextPartitionIndex === PARTITION_INDEX_SENTINEL) {
      errors.push(createIssue("chunk_chain_ended_early"));
      break;
    }

    currentPartitionIndex = nextPartitionIndex;
  }

  const actualDataBytes =
    header.partitionCount > 0
      ? (header.partitionCount - 1) * PARTITION_PAYLOAD_SIZE + header.lastPartitionDataLength
      : 0;
  const totalPayloadCapacity = header.partitionCount * PARTITION_PAYLOAD_SIZE;

  return {
    indices,
    actualDataBytes,
    unwrittenPayloadBytes: Math.max(0, totalPayloadCapacity - actualDataBytes),
    nextPointerBytes: header.partitionCount * 4,
    terminatedWithSentinel,
    errors,
  };
}

// 依据文件头记录遍历空闲链；这条链只统计索引关系，不解释 payload 内容。
function traverseFreeChain(fileBuffer: Buffer, fileSize: number, state: FreePartitionState): FreeChainInfo {
  const errors: RegionIssue[] = [];
  const indices: number[] = [];
  let terminatedWithSentinel = false;
  let cyclic = false;

  const allocatedPartitionCount = getAllocatedPartitionCount(fileSize);
  if (!isFreePartitionStateValid(state, allocatedPartitionCount)) {
    errors.push(createIssue("free_chain_head_invalid"));
    return {
      state,
      indices,
      errors,
      terminatedWithSentinel,
      cyclic,
    };
  }

  if (state.freePartitionCount === 0) {
    terminatedWithSentinel = true;
    return {
      state,
      indices,
      errors,
      terminatedWithSentinel,
      cyclic,
    };
  }

  const visited = new Set<number>();
  let currentPartitionIndex = state.headFreePartitionIndex;
  for (let index = 0; index < state.freePartitionCount; index += 1) {
    if (visited.has(currentPartitionIndex)) {
      errors.push(createIssue("free_chain_duplicate_partition", { partitionIndex: currentPartitionIndex }));
      cyclic = true;
      break;
    }
    visited.add(currentPartitionIndex);
    indices.push(currentPartitionIndex);

    const nextPartitionIndex = readRawPartitionNextIndex(fileBuffer, fileSize, currentPartitionIndex);
    if (nextPartitionIndex === null) {
      errors.push(createIssue("free_chain_cannot_read_next", { partitionIndex: currentPartitionIndex }));
      break;
    }

    const isLast = index === state.freePartitionCount - 1;
    if (isLast) {
      if (nextPartitionIndex !== PARTITION_INDEX_SENTINEL) {
        errors.push(createIssue("free_chain_not_terminated"));
      } else {
        terminatedWithSentinel = true;
      }
      break;
    }

    if (nextPartitionIndex === PARTITION_INDEX_SENTINEL) {
      errors.push(createIssue("free_chain_ended_early"));
      break;
    }

    currentPartitionIndex = nextPartitionIndex;
  }

  return {
    state,
    indices,
    errors,
    terminatedWithSentinel,
    cyclic,
  };
}

// introduction 区只有签名和 CreateTime 文本前缀需要按 UTF-8 解释。
function parseCreateTimestamp(fileBuffer: Buffer): number | null {
  const introBuffer = fileBuffer.subarray(
    INTRODUCTION_AREA_OFFSET_IN_FILE,
    INTRODUCTION_AREA_OFFSET_IN_FILE + INTRODUCTION_AREA_SIZE,
  );
  const signature = decodeUtf8(introBuffer.subarray(0, Buffer.byteLength(INTRODUCTION_SIGNATURE, "utf8")));
  if (signature !== INTRODUCTION_SIGNATURE) {
    return null;
  }

  const prefixOffset = Buffer.byteLength(INTRODUCTION_SIGNATURE, "utf8");
  const prefixLength = Buffer.byteLength(CREATE_TIME_PREFIX, "utf8");
  const createTimePrefix = decodeUtf8(introBuffer.subarray(prefixOffset, prefixOffset + prefixLength));
  if (createTimePrefix !== CREATE_TIME_PREFIX) {
    return null;
  }

  return Number(introBuffer.readBigInt64LE(prefixOffset + prefixLength));
}

// 解析单个 region 文件，产出结构化解析结果与发现项。
export function parseRegionFile(regionFilePath: string): RegionParseResult {
  const absolutePath = resolve(regionFilePath);
  const fileBuffer = readFileSync(absolutePath);
  const fileSize = fileBuffer.length;
  const findings: RegionIssue[] = [];

  const formatBuffer = fileBuffer.subarray(
    FORMAT_AREA_OFFSET_IN_FILE,
    Math.min(fileSize, FORMAT_AREA_OFFSET_IN_FILE + FORMAT_AREA_SIZE),
  );
  const formatError =
    formatBuffer.length === FORMAT_AREA_SIZE ? validateFormatArea(formatBuffer) : createIssue("format_area_truncated");
  const formatOk = formatError === null;
  if (formatError) {
    findings.push(formatError);
  }

  const signatureLength = Buffer.byteLength(INTRODUCTION_SIGNATURE, "utf8");
  const signatureText =
    INTRODUCTION_AREA_OFFSET_IN_FILE + signatureLength <= fileSize
      ? decodeUtf8(fileBuffer.subarray(INTRODUCTION_AREA_OFFSET_IN_FILE, INTRODUCTION_AREA_OFFSET_IN_FILE + signatureLength))
      : "";
  const signatureOk = signatureText === INTRODUCTION_SIGNATURE;
  if (!signatureOk) {
    findings.push(createIssue("introduction_signature_mismatch"));
  }

  const alignmentOk =
    fileSize >= PARTITION_AREA_OFFSET_IN_FILE &&
    (fileSize - PARTITION_AREA_OFFSET_IN_FILE) % PARTITION_ENTRY_SIZE === 0;
  if (!alignmentOk) {
    findings.push(createIssue("partition_area_misaligned"));
  }

  const allocatedPartitionCount = alignmentOk ? getAllocatedPartitionCount(fileSize) : 0;

  const freePartitionState: FreePartitionState =
    FREE_PARTITION_COUNT_OFFSET_IN_FILE + 4 <= fileSize
      ? {
          headFreePartitionIndex: readU32(fileBuffer, HEAD_FREE_PARTITION_INDEX_OFFSET_IN_FILE),
          freePartitionCount: readU32(fileBuffer, FREE_PARTITION_COUNT_OFFSET_IN_FILE),
        }
      : {
          headFreePartitionIndex: PARTITION_INDEX_SENTINEL,
          freePartitionCount: 0,
        };

  const freeChain = traverseFreeChain(fileBuffer, fileSize, freePartitionState);
  findings.push(...freeChain.errors);

  const chunks: ParsedChunkRecord[] = [];
  const partitionOwners = new Map<number, string>();
  for (let chunkIndex = 0; chunkIndex < REGION_CHUNK_COUNT; chunkIndex += 1) {
    const offset = getChunkDataOffsetInFile(chunkIndex);
    const { x, y } = chunkIndexToLocalPosition(chunkIndex);

    if (offset + CHUNK_DATA_ENTRY_SIZE > fileSize) {
      chunks.push({
        chunkIndex,
        localX: x,
        localY: y,
        header: {
          firstPartitionIndex: PARTITION_INDEX_SENTINEL,
          lastPartitionDataLength: 0,
          partitionCount: 0,
          timestamp: 0,
          isEmpty: true,
        },
        structuralErrors: [createIssue("chunk_header_truncated", undefined, { kind: "chunk", localX: x, localY: y })],
      });
      continue;
    }

    const header = readChunkHeaderData(fileBuffer, chunkIndex);
    const structuralErrors = validateChunkHeaderData(header, allocatedPartitionCount);
    const chunkRecord: ParsedChunkRecord = {
      chunkIndex,
      localX: x,
      localY: y,
      header,
      structuralErrors,
    };

    if (!header.isEmpty && structuralErrors.length === 0) {
      chunkRecord.chain = traverseChunkChain(fileBuffer, fileSize, header);
      for (const partitionIndex of chunkRecord.chain.indices) {
        const currentOwner = `chunk(${x},${y})`;
        const owner = partitionOwners.get(partitionIndex);
        if (owner && owner !== currentOwner) {
          const overlap = createIssue(
            "partition_overlap_between_chunks",
            { partitionIndex, leftOwner: owner, rightOwner: currentOwner },
            { kind: "chunk", localX: x, localY: y },
          );
          chunkRecord.chain.errors.push(overlap);
        } else {
          partitionOwners.set(partitionIndex, currentOwner);
        }
      }
      findings.push(
        ...chunkRecord.chain.errors.map((error) => ({
          ...error,
          scope: error.scope ?? { kind: "chunk", localX: x, localY: y },
        })),
      );
    } else if (!header.isEmpty) {
      findings.push(...structuralErrors.map((error) => ({ ...error, scope: { kind: "chunk", localX: x, localY: y } })));
    }

    chunks.push(chunkRecord);
  }

  for (const freePartitionIndex of freeChain.indices) {
    const owner = partitionOwners.get(freePartitionIndex);
    if (owner) {
      findings.push(createIssue("free_partition_overlaps_owner", { partitionIndex: freePartitionIndex, owner }));
    }
  }

  return {
    path: absolutePath,
    fileSize,
    allocatedPartitionCount,
    prefixInvalidBytes: PREFIX_INVALID_BYTES,
    formatOk,
    signatureOk,
    alignmentOk,
    createTimestamp: parseCreateTimestamp(fileBuffer),
    freeChain,
    chunks,
    findings,
  };
}

// 从文件或目录中收集 region 文件路径；目录扫描默认递归。
export function findRegionFiles(inputPath: string, options: ScanOptions): string[] {
  const absolutePath = resolve(inputPath);
  const stat = statSync(absolutePath);
  if (stat.isFile()) {
    return extname(absolutePath).toLowerCase() === FILE_EXTENSION ? [absolutePath] : [];
  }

  if (!stat.isDirectory()) {
    return [];
  }

  const results: string[] = [];
  const entries = readdirSync(absolutePath, { withFileTypes: true });
  for (const entry of entries) {
    const entryPath = join(absolutePath, entry.name);
    if (entry.isFile()) {
      if (extname(entry.name).toLowerCase() === FILE_EXTENSION) {
        results.push(entryPath);
      }
      continue;
    }
    if (entry.isDirectory() && options.recursive) {
      results.push(...findRegionFiles(entryPath, options));
    }
  }

  results.sort((left, right) => basename(left).localeCompare(basename(right)) || left.localeCompare(right));
  return results;
}
