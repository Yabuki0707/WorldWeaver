using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence.Region
{
    /// <summary>
    /// ChunkRegion 文件布局。
    /// <para>该静态类负责定义标准格式，并将格式查询结果缓存为程序直接使用的布局常量。</para>
    /// </summary>
    public static class ChunkRegionFileLayout
    {
        /// <summary>
        /// ChunkRegion 文件后缀名。
        /// </summary>
        public const string FILE_EXTENSION = ".cr";

        /// <summary>
        /// 介绍区域开头的固定签名字符串。
        /// </summary>
        public const string INTRODUCTION_SIGNATURE = "<ChunkRegion>\n";

        /// <summary>
        /// 介绍区域创建时间字段的文本前缀。
        /// </summary>
        public const string CREATE_TIME_PREFIX = "CreateTime:";

        /// <summary>
        /// 表示“当前分区不存在下一跳”的哨兵值。
        /// </summary>
        public const uint PARTITION_INDEX_SENTINEL = uint.MaxValue;

        /// <summary>
        /// 单个 region 在 X/Y 轴方向包含的 chunk 数量。
        /// </summary>
        public const int REGION_CHUNK_AXIS = 32;

        /// <summary>
        /// 单个 region 包含的 chunk 总数。
        /// </summary>
        public const int REGION_CHUNK_COUNT = REGION_CHUNK_AXIS * REGION_CHUNK_AXIS;

        /// <summary>
        /// 格式区域的标准定义。
        /// </summary>
        public static readonly Dictionary<string, object> FORMAT_AREA_DICTIONARY = new(StringComparer.Ordinal)
        {
            ["SIZE"] = 2 * 1024,
            ["FormatJson"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["offset"] = 0,
                ["size"] = 2 * 1024
            }
        };

        /// <summary>
        /// 介绍区域的标准定义。
        /// </summary>
        public static readonly Dictionary<string, object> INTRODUCTION_AREA_DICTIONARY = new(StringComparer.Ordinal)
        {
            ["SIZE"] = 2 * 1024,
            ["Signature"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["offset"] = 0,
                ["size"] = Encoding.UTF8.GetByteCount(INTRODUCTION_SIGNATURE)
            },
            ["CreateTime"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["offset"] = Encoding.UTF8.GetByteCount(INTRODUCTION_SIGNATURE),
                ["size"] = Encoding.UTF8.GetByteCount(CREATE_TIME_PREFIX) + sizeof(long)
            }
        };

        /// <summary>
        /// 头数据区域的标准定义。
        /// </summary>
        public static readonly Dictionary<string, object> HEADER_AREA_DICTIONARY = new(StringComparer.Ordinal)
        {
            ["SIZE"] = 24 * 1024,
            ["ChunkData"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["offset"] = 0,
                ["size"] = sizeof(uint) + sizeof(ushort) + sizeof(uint) + sizeof(long)
            },
            ["HeadFreePartitionIndex"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["offset"] = REGION_CHUNK_COUNT * (sizeof(uint) + sizeof(ushort) + sizeof(uint) + sizeof(long)),
                ["size"] = sizeof(uint)
            },
            ["FreePartitionCount"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["offset"] = REGION_CHUNK_COUNT * (sizeof(uint) + sizeof(ushort) + sizeof(uint) + sizeof(long)) + sizeof(uint),
                ["size"] = sizeof(uint)
            }
        };

        /// <summary>
        /// 分区区域的标准定义。
        /// </summary>
        public static readonly Dictionary<string, object> PARTITION_AREA_DICTIONARY = new(StringComparer.Ordinal)
        {
            ["SIZE"] = 0,
            ["Next"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["offset"] = 0,
                ["size"] = sizeof(uint)
            },
            ["Partition"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["offset"] = sizeof(uint),
                ["size"] = 4 * 1024 - sizeof(uint)
            }
        };

        /// <summary>
        /// 标准 ChunkRegion 格式。
        /// </summary>
        public static readonly ChunkRegionFormat STANDARD_FORMAT;

        /// <summary>
        /// 格式区域在整个文件中的起始偏移。
        /// </summary>
        public static readonly long FORMAT_AREA_OFFSET_IN_FILE;

        /// <summary>
        /// 格式区域总大小。
        /// </summary>
        public static readonly int FORMAT_AREA_SIZE;

        /// <summary>
        /// 介绍区域在整个文件中的起始偏移。
        /// </summary>
        public static readonly long INTRODUCTION_AREA_OFFSET_IN_FILE;

        /// <summary>
        /// 介绍区域总大小。
        /// </summary>
        public static readonly int INTRODUCTION_AREA_SIZE;

        /// <summary>
        /// 头数据区域在整个文件中的起始偏移。
        /// </summary>
        public static readonly long HEADER_AREA_OFFSET_IN_FILE;

        /// <summary>
        /// 头数据区域总大小。
        /// </summary>
        public static readonly int HEADER_AREA_SIZE;

        /// <summary>
        /// 区块头数据区域在整个文件中的起始偏移。
        /// </summary>
        public static readonly long CHUNK_DATA_OFFSET_IN_FILE;

        /// <summary>
        /// 分区区域在整个文件中的起始偏移。
        /// </summary>
        public static readonly long PARTITION_AREA_OFFSET_IN_FILE;

        /// <summary>
        /// 单条 chunk 头数据记录的字节大小。
        /// </summary>
        public static readonly int CHUNK_DATA_ENTRY_SIZE;

        /// <summary>
        /// 头空闲分区索引在整个文件中的偏移。
        /// </summary>
        public static readonly long HEAD_FREE_PARTITION_INDEX_OFFSET_IN_FILE;

        /// <summary>
        /// 头空闲分区索引字段大小。
        /// </summary>
        public static readonly int HEAD_FREE_PARTITION_INDEX_SIZE;

        /// <summary>
        /// 空闲分区数量在整个文件中的偏移。
        /// </summary>
        public static readonly long FREE_PARTITION_COUNT_OFFSET_IN_FILE;

        /// <summary>
        /// 空闲分区数量字段大小。
        /// </summary>
        public static readonly int FREE_PARTITION_COUNT_SIZE;

        /// <summary>
        /// 分区 next 索引字段大小。
        /// </summary>
        public static readonly int PARTITION_NEXT_INDEX_SIZE;

        /// <summary>
        /// 分区有效数据区大小。
        /// </summary>
        public static readonly int PARTITION_PAYLOAD_SIZE;

        /// <summary>
        /// 单个分区占用的总字节大小。
        /// </summary>
        public static readonly int PARTITION_ENTRY_SIZE;

        /// <summary>
        /// 分区 next 字段相对于分区开头的偏移。
        /// </summary>
        private static readonly int _PARTITION_NEXT_OFFSET_IN_PARTITION;

        /// <summary>
        /// 分区有效数据区相对于分区开头的偏移。
        /// </summary>
        private static readonly int _PARTITION_PAYLOAD_OFFSET_IN_PARTITION;

        static ChunkRegionFileLayout()
        {
            if (!ChunkRegionFormat.TryCreate(
                    new[]
                    {
                        FORMAT_AREA_DICTIONARY,
                        INTRODUCTION_AREA_DICTIONARY,
                        HEADER_AREA_DICTIONARY,
                        PARTITION_AREA_DICTIONARY
                    },
                    out ChunkRegionFormat standardFormat))
            {
                throw new InvalidOperationException("ChunkRegionFileLayout 初始化失败：无法创建标准 ChunkRegion 格式。");
            }

            STANDARD_FORMAT = standardFormat;
            FORMAT_AREA_OFFSET_IN_FILE = 0;
            FORMAT_AREA_SIZE = GetRequiredAreaSize(FORMAT_AREA_DICTIONARY, "FORMAT_AREA_DICTIONARY");
            INTRODUCTION_AREA_OFFSET_IN_FILE = FORMAT_AREA_OFFSET_IN_FILE + FORMAT_AREA_SIZE;
            INTRODUCTION_AREA_SIZE = GetRequiredAreaSize(INTRODUCTION_AREA_DICTIONARY, "INTRODUCTION_AREA_DICTIONARY");
            HEADER_AREA_OFFSET_IN_FILE = INTRODUCTION_AREA_OFFSET_IN_FILE + INTRODUCTION_AREA_SIZE;
            HEADER_AREA_SIZE = GetRequiredAreaSize(HEADER_AREA_DICTIONARY, "HEADER_AREA_DICTIONARY");
            CHUNK_DATA_OFFSET_IN_FILE = HEADER_AREA_OFFSET_IN_FILE + GetRequiredFieldOffset(HEADER_AREA_DICTIONARY, "ChunkData");
            PARTITION_AREA_OFFSET_IN_FILE = HEADER_AREA_OFFSET_IN_FILE + HEADER_AREA_SIZE;
            CHUNK_DATA_ENTRY_SIZE = GetRequiredFieldSize(HEADER_AREA_DICTIONARY, "ChunkData");
            HEAD_FREE_PARTITION_INDEX_OFFSET_IN_FILE = HEADER_AREA_OFFSET_IN_FILE + GetRequiredFieldOffset(HEADER_AREA_DICTIONARY, "HeadFreePartitionIndex");
            HEAD_FREE_PARTITION_INDEX_SIZE = GetRequiredFieldSize(HEADER_AREA_DICTIONARY, "HeadFreePartitionIndex");
            FREE_PARTITION_COUNT_OFFSET_IN_FILE = HEADER_AREA_OFFSET_IN_FILE + GetRequiredFieldOffset(HEADER_AREA_DICTIONARY, "FreePartitionCount");
            FREE_PARTITION_COUNT_SIZE = GetRequiredFieldSize(HEADER_AREA_DICTIONARY, "FreePartitionCount");
            PARTITION_NEXT_INDEX_SIZE = GetRequiredFieldSize(PARTITION_AREA_DICTIONARY, "Next");
            PARTITION_PAYLOAD_SIZE = GetRequiredFieldSize(PARTITION_AREA_DICTIONARY, "Partition");
            PARTITION_ENTRY_SIZE = PARTITION_NEXT_INDEX_SIZE + PARTITION_PAYLOAD_SIZE;
            _PARTITION_NEXT_OFFSET_IN_PARTITION = GetRequiredFieldOffset(PARTITION_AREA_DICTIONARY, "Next");
            _PARTITION_PAYLOAD_OFFSET_IN_PARTITION = GetRequiredFieldOffset(PARTITION_AREA_DICTIONARY, "Partition");
        }

        /// <summary>
        /// 尝试获取指定局部 chunk 坐标对应的头数据偏移。
        /// </summary>
        public static bool TryGetChunkDataOffsetInFile(Vector2I localChunkPosition, out long offsetInFile)
        {
            offsetInFile = 0;
            if (!ChunkRegionPositionProcessor.ValidateLocalChunkPosition(localChunkPosition))
            {
                return false;
            }

            int localChunkIndex = localChunkPosition.Y * REGION_CHUNK_AXIS + localChunkPosition.X;
            offsetInFile = CHUNK_DATA_OFFSET_IN_FILE + localChunkIndex * (long)CHUNK_DATA_ENTRY_SIZE;
            return true;
        }

        /// <summary>
        /// 获取指定分区的 next 索引字段在整个文件中的偏移。
        /// </summary>
        public static long GetPartitionNextOffsetInFile(uint partitionIndex)
        {
            return PARTITION_AREA_OFFSET_IN_FILE + partitionIndex * (long)PARTITION_ENTRY_SIZE + _PARTITION_NEXT_OFFSET_IN_PARTITION;
        }

        /// <summary>
        /// 获取指定分区的有效数据区在整个文件中的偏移。
        /// </summary>
        public static long GetPartitionPayloadOffsetInFile(uint partitionIndex)
        {
            return PARTITION_AREA_OFFSET_IN_FILE + partitionIndex * (long)PARTITION_ENTRY_SIZE + _PARTITION_PAYLOAD_OFFSET_IN_PARTITION;
        }

        /// <summary>
        /// 获取指定分区在整个文件中的起始偏移。
        /// </summary>
        public static long GetPartitionOffsetInFile(uint partitionIndex)
        {
            return PARTITION_AREA_OFFSET_IN_FILE + partitionIndex * (long)PARTITION_ENTRY_SIZE;
        }

        /// <summary>
        /// 获取区域总大小，并在布局初始化失败时直接终止。
        /// </summary>
        private static int GetRequiredAreaSize(Dictionary<string, object> areaDictionary, string areaName)
        {
            int size = STANDARD_FORMAT.GetAreaSize(areaDictionary);
            if (size <= 0)
            {
                throw new InvalidOperationException($"ChunkRegionFileLayout 初始化失败：{areaName} 的 SIZE 非法。");
            }

            return size;
        }

        /// <summary>
        /// 获取字段偏移，并在布局初始化失败时直接终止。
        /// </summary>
        private static int GetRequiredFieldOffset(Dictionary<string, object> areaDictionary, string fieldName)
        {
            Dictionary<string, object> fieldDictionary = STANDARD_FORMAT.GetFieldDictionary(areaDictionary, fieldName);
            if (fieldDictionary == null || !fieldDictionary.TryGetValue("offset", out object offsetValue) || offsetValue is not int offset || offset < 0)
            {
                throw new InvalidOperationException($"ChunkRegionFileLayout 初始化失败：字段 {fieldName} 的 offset 非法。");
            }

            return offset;
        }

        /// <summary>
        /// 获取字段大小，并在布局初始化失败时直接终止。
        /// </summary>
        private static int GetRequiredFieldSize(Dictionary<string, object> areaDictionary, string fieldName)
        {
            Dictionary<string, object> fieldDictionary = STANDARD_FORMAT.GetFieldDictionary(areaDictionary, fieldName);
            if (fieldDictionary == null || !fieldDictionary.TryGetValue("size", out object sizeValue) || sizeValue is not int size || size <= 0)
            {
                throw new InvalidOperationException($"ChunkRegionFileLayout 初始化失败：字段 {fieldName} 的 size 非法。");
            }

            return size;
        }
    }
}
