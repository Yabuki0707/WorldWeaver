using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using Godot;
using Newtonsoft.Json;
namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// ChunkRegion 文件创建器。
    /// <para>该类型只负责生成 region 路径，并按标准布局创建新的 region 文件。</para>
    /// </summary>
    public static class ChunkRegionCreater
    {
        /// <summary>
        /// 介绍区域签名的 UTF-8 字节缓存。
        /// </summary>
        private static readonly byte[] _INTRODUCTION_SIGNATURE_BYTES =
            Encoding.UTF8.GetBytes(ChunkRegionFileLayout.INTRODUCTION_SIGNATURE);

        /// <summary>
        /// 创建时间字段前缀的 UTF-8 字节缓存。
        /// </summary>
        private static readonly byte[] _CREATE_TIME_PREFIX_BYTES =
            Encoding.UTF8.GetBytes(ChunkRegionFileLayout.CREATE_TIME_PREFIX);

        /// <summary>
        /// 预留格式区域的标准字节缓存。
        /// </summary>
        private static readonly byte[] _STANDARD_FORMAT_BYTES = CreateReservedFormatBytes();

        /// <summary>
        /// 空头数据区域的标准字节缓存。
        /// </summary>
        private static readonly byte[] _EMPTY_HEADER_AREA_BYTES = CreateEmptyHeaderAreaBytes();

        /// <summary>
        /// 创建指定路径的 region 文件。
        /// <para>若目标文件已存在，则抛出异常而不是覆盖旧文件。</para>
        /// </summary>
        public static bool Create(string regionFilePath)
        {
            if (string.IsNullOrWhiteSpace(regionFilePath))
            {
                GD.PushError("[ChunkRegionCreater] Create: regionFilePath 不能为空。");
                return false;
            }

            string fullRegionFilePath = Path.GetFullPath(regionFilePath);
            if (!string.Equals(Path.GetExtension(fullRegionFilePath), ChunkRegionFileLayout.FILE_EXTENSION, StringComparison.OrdinalIgnoreCase))
            {
                GD.PushError(
                    $"[ChunkRegionCreater] Create: region 文件后缀必须为 {ChunkRegionFileLayout.FILE_EXTENSION}，当前路径为 {fullRegionFilePath}。");
                return false;
            }

            if (_STANDARD_FORMAT_BYTES == null || _EMPTY_HEADER_AREA_BYTES == null)
            {
                GD.PushError("[ChunkRegionCreater] Create: 标准 region 缓存字节无效。");
                return false;
            }

            string regionDirectoryPath = Path.GetDirectoryName(fullRegionFilePath);
            if (!string.IsNullOrWhiteSpace(regionDirectoryPath))
            {
                Directory.CreateDirectory(regionDirectoryPath);
            }

            try
            {
                using FileStream stream = new(
                    fullRegionFilePath,
                    new FileStreamOptions
                    {
                        Mode = FileMode.CreateNew,
                        Access = System.IO.FileAccess.ReadWrite,
                        Share = FileShare.ReadWrite,
                        BufferSize = ChunkRegionFileLayout.PARTITION_ENTRY_SIZE,
                        Options = FileOptions.RandomAccess
                    });

                // 先把文件扩展到分区区开始位置，确保后续各固定区域都能按绝对偏移直接写入。
                stream.SetLength(ChunkRegionFileLayout.PARTITION_AREA_OFFSET_IN_FILE);
                WriteBytes(stream, ChunkRegionFileLayout.FORMAT_AREA_OFFSET_IN_FILE, _STANDARD_FORMAT_BYTES);
                WriteBytes(
                    stream,
                    ChunkRegionFileLayout.INTRODUCTION_AREA_OFFSET_IN_FILE,
                    CreateIntroductionAreaBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
                WriteBytes(stream, ChunkRegionFileLayout.HEADER_AREA_OFFSET_IN_FILE, _EMPTY_HEADER_AREA_BYTES);
                // 创建流程结束后强制落盘，避免后续打开时读到半初始化状态。
                stream.Flush(true);
                return true;
            }
            catch (Exception exception)
            {
                GD.PushError($"[ChunkRegionCreater] Create: 创建 region 文件 {fullRegionFilePath} 失败: {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// 创建并填充预留格式区域的标准字节。
        /// </summary>
        private static byte[] CreateReservedFormatBytes()
        {
            string formatJsonText = ChunkRegionFileLayout.STANDARD_FORMAT.ToJsonObject().ToString(Formatting.None);
            byte[] formatJsonBytes = Encoding.UTF8.GetBytes(formatJsonText);
            if (formatJsonBytes.Length > ChunkRegionFileLayout.FORMAT_AREA_SIZE)
            {
                GD.PushError("[ChunkRegionCreater] CreateReservedFormatBytes: 标准 ChunkRegion 格式 JSON 超出预留格式区域大小。");
                return null;
            }

            // 预留区域剩余字节保持为 0，便于后续按固定长度读取后再裁掉尾部填充。
            byte[] reservedFormatBytes = new byte[ChunkRegionFileLayout.FORMAT_AREA_SIZE];
            formatJsonBytes.CopyTo(reservedFormatBytes, 0);
            return reservedFormatBytes;
        }

        /// <summary>
        /// 创建介绍区域字节。
        /// </summary>
        private static byte[] CreateIntroductionAreaBytes(long createTimestamp)
        {
            byte[] introductionBytes = new byte[ChunkRegionFileLayout.INTRODUCTION_AREA_SIZE];
            _INTRODUCTION_SIGNATURE_BYTES.CopyTo(introductionBytes, 0);

            // CreateTime 字段的位置来自标准格式描述，而不是写死偏移，避免布局调整后遗漏同步。
            int createTimeOffset = ChunkRegionFileLayout.STANDARD_FORMAT.GetFieldOffset(
                ChunkRegionFileLayout.INTRODUCTION_AREA_DICTIONARY,
                "CreateTime");
            int createTimeSize = ChunkRegionFileLayout.STANDARD_FORMAT.GetFieldSize(
                ChunkRegionFileLayout.INTRODUCTION_AREA_DICTIONARY,
                "CreateTime");
            Span<byte> createTimeSpan = introductionBytes.AsSpan(createTimeOffset, createTimeSize);
            _CREATE_TIME_PREFIX_BYTES.CopyTo(createTimeSpan);
            BinaryPrimitives.WriteInt64LittleEndian(createTimeSpan.Slice(_CREATE_TIME_PREFIX_BYTES.Length, sizeof(long)), createTimestamp);
            return introductionBytes;
        }

        /// <summary>
        /// 创建空的头数据区域字节。
        /// </summary>
        private static byte[] CreateEmptyHeaderAreaBytes()
        {
            byte[] headerAreaBytes = new byte[ChunkRegionFileLayout.HEADER_AREA_SIZE];
            for (int i = 0; i < ChunkRegionFileLayout.REGION_CHUNK_COUNT; i++)
            {
                // 空头记录统一以首分区索引哨兵值开头，读取时即可快速判定该 chunk 尚未写入。
                BinaryPrimitives.WriteUInt32LittleEndian(
                    headerAreaBytes.AsSpan(i * ChunkRegionFileLayout.CHUNK_DATA_ENTRY_SIZE, sizeof(uint)),
                    ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL);
            }

            // 空闲分区头状态在新文件中必须显式初始化为空链，避免后续把 0 误判为有效分区索引。
            int headFreePartitionIndexOffset = ChunkRegionFileLayout.STANDARD_FORMAT.GetFieldOffset(
                ChunkRegionFileLayout.HEADER_AREA_DICTIONARY,
                "HeadFreePartitionIndex");
            int freePartitionCountOffset = ChunkRegionFileLayout.STANDARD_FORMAT.GetFieldOffset(
                ChunkRegionFileLayout.HEADER_AREA_DICTIONARY,
                "FreePartitionCount");
            BinaryPrimitives.WriteUInt32LittleEndian(
                headerAreaBytes.AsSpan(headFreePartitionIndexOffset, sizeof(uint)),
                ChunkRegionFileLayout.PARTITION_INDEX_SENTINEL);
            BinaryPrimitives.WriteUInt32LittleEndian(
                headerAreaBytes.AsSpan(freePartitionCountOffset, sizeof(uint)),
                0);
            return headerAreaBytes;
        }

        /// <summary>
        /// 将字节写入到指定文件偏移位置。
        /// </summary>
        private static void WriteBytes(FileStream stream, long offsetInFile, ReadOnlySpan<byte> bytes)
        {
            stream.Position = offsetInFile;
            stream.Write(bytes);
        }
    }
}
