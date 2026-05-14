// 该模块镜像 C# 中的 ChunkRegionFileLayout，
// 所有尺寸、偏移、哨兵值都必须与运行时实现保持一致。
export const FILE_EXTENSION = ".cr";
export const INTRODUCTION_SIGNATURE = "<ChunkRegion>\n";
export const CREATE_TIME_PREFIX = "CreateTime:";
export const PARTITION_INDEX_SENTINEL = 0xffffffff;

export const REGION_CHUNK_AXIS = 32;
export const REGION_CHUNK_COUNT = REGION_CHUNK_AXIS * REGION_CHUNK_AXIS;

export const FORMAT_AREA_SIZE = 2 * 1024;
export const INTRODUCTION_AREA_SIZE = 2 * 1024;
export const HEADER_AREA_SIZE = 24 * 1024;

export const FORMAT_AREA_OFFSET_IN_FILE = 0;
export const INTRODUCTION_AREA_OFFSET_IN_FILE = FORMAT_AREA_OFFSET_IN_FILE + FORMAT_AREA_SIZE;
export const HEADER_AREA_OFFSET_IN_FILE = INTRODUCTION_AREA_OFFSET_IN_FILE + INTRODUCTION_AREA_SIZE;
export const PARTITION_AREA_OFFSET_IN_FILE = HEADER_AREA_OFFSET_IN_FILE + HEADER_AREA_SIZE;

export const CHUNK_DATA_ENTRY_SIZE = 4 + 2 + 4 + 8;
export const CHUNK_DATA_OFFSET_IN_FILE = HEADER_AREA_OFFSET_IN_FILE;

export const HEAD_FREE_PARTITION_INDEX_OFFSET_IN_FILE =
  CHUNK_DATA_OFFSET_IN_FILE + REGION_CHUNK_COUNT * CHUNK_DATA_ENTRY_SIZE;
export const HEAD_FREE_PARTITION_INDEX_SIZE = 4;
export const FREE_PARTITION_COUNT_OFFSET_IN_FILE =
  HEAD_FREE_PARTITION_INDEX_OFFSET_IN_FILE + HEAD_FREE_PARTITION_INDEX_SIZE;
export const FREE_PARTITION_COUNT_SIZE = 4;

export const PARTITION_NEXT_INDEX_SIZE = 4;
export const PARTITION_PAYLOAD_SIZE = 4096 - PARTITION_NEXT_INDEX_SIZE;
export const PARTITION_ENTRY_SIZE = PARTITION_NEXT_INDEX_SIZE + PARTITION_PAYLOAD_SIZE;

export const PREFIX_INVALID_BYTES = PARTITION_AREA_OFFSET_IN_FILE;

// 标准格式定义直接用于校验 format 区 JSON，
// 脚本不会“宽松猜测”布局，只接受与运行时兼容的结构。
export const STANDARD_FORMAT = [
  {
    SIZE: 2048,
    FormatJson: {
      offset: 0,
      size: 2048,
    },
  },
  {
    SIZE: 2048,
    Signature: {
      offset: 0,
      size: Buffer.byteLength(INTRODUCTION_SIGNATURE, "utf8"),
    },
    CreateTime: {
      offset: Buffer.byteLength(INTRODUCTION_SIGNATURE, "utf8"),
      size: Buffer.byteLength(CREATE_TIME_PREFIX, "utf8") + 8,
    },
  },
  {
    SIZE: 24576,
    ChunkData: {
      offset: 0,
      size: CHUNK_DATA_ENTRY_SIZE,
    },
    HeadFreePartitionIndex: {
      offset: REGION_CHUNK_COUNT * CHUNK_DATA_ENTRY_SIZE,
      size: 4,
    },
    FreePartitionCount: {
      offset: REGION_CHUNK_COUNT * CHUNK_DATA_ENTRY_SIZE + 4,
      size: 4,
    },
  },
  {
    SIZE: 0,
    Next: {
      offset: 0,
      size: 4,
    },
    Partition: {
      offset: 4,
      size: PARTITION_PAYLOAD_SIZE,
    },
  },
] as const;

// 根据局部 chunk 索引定位 header 记录。
export function getChunkDataOffsetInFile(localChunkIndex: number): number {
  return CHUNK_DATA_OFFSET_IN_FILE + localChunkIndex * CHUNK_DATA_ENTRY_SIZE;
}

// 返回某个 partition 的物理起始偏移。
export function getPartitionOffsetInFile(partitionIndex: number): number {
  return PARTITION_AREA_OFFSET_IN_FILE + partitionIndex * PARTITION_ENTRY_SIZE;
}

// next 字段位于 partition 开头。
export function getPartitionNextOffsetInFile(partitionIndex: number): number {
  return getPartitionOffsetInFile(partitionIndex);
}

// payload 紧跟在 next 字段之后。
export function getPartitionPayloadOffsetInFile(partitionIndex: number): number {
  return getPartitionOffsetInFile(partitionIndex) + PARTITION_NEXT_INDEX_SIZE;
}

// 把 0..1023 的线性 chunk 索引还原成 32x32 区域内坐标。
export function chunkIndexToLocalPosition(index: number): { x: number; y: number } {
  return {
    x: index % REGION_CHUNK_AXIS,
    y: Math.floor(index / REGION_CHUNK_AXIS),
  };
}
