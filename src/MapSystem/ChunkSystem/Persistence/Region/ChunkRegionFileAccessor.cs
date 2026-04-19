using System;
using System.IO;
using System.Text;
using Godot;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence.Region
{
	/// <summary>
	/// ChunkRegion 文件访问器。
	/// <para>该类型负责 region 文件级别的创建、存在性检查、打开校验与基础文件属性维护。</para>
	/// </summary>
	public class ChunkRegionFileAccessor : IDisposable
	{
		/// <summary>
		/// 介绍区域签名的 UTF-8 字节缓存。
		/// </summary>
		private static readonly byte[] _INTRODUCTION_SIGNATURE_BYTES =
			Encoding.UTF8.GetBytes(ChunkRegionFileLayout.INTRODUCTION_SIGNATURE);

		/// <summary>
		/// 当前访问器绑定的 region 文件完整路径。
		/// </summary>
		private readonly string _regionFilePath;

		/// <summary>
		/// 当前访问器绑定的 region 坐标。
		/// </summary>
		private readonly Vector2I _regionPosition;

		/// <summary>
		/// 当前访问器持有的底层文件流。
		/// </summary>
		private readonly FileStream _stream;

		/// <summary>
		/// 当前访问器绑定的 region 文件完整路径。
		/// </summary>
		public string RegionFilePath => _regionFilePath;

		/// <summary>
		/// 当前访问器绑定的 region 坐标。
		/// </summary>
		public Vector2I RegionPosition => _regionPosition;

		/// <summary>
		/// 当前访问器持有的底层文件流。
		/// </summary>
		protected FileStream Stream => _stream;

		/// <summary>
		/// 仅允许当前类型及其派生类型在 Open 成功后创建实例。
		/// </summary>
		protected ChunkRegionFileAccessor(string regionFilePath, Vector2I regionPosition, FileStream stream)
		{
			if (string.IsNullOrWhiteSpace(regionFilePath)) GD.PushError("[ChunkRegionFileAccessor] ctor: regionFilePath 不能为空。");
			if (stream == null) GD.PushError("[ChunkRegionFileAccessor] ctor: stream 不能为空。");

			_regionFilePath = regionFilePath;
			_regionPosition = regionPosition;
			_stream = stream;
		}

		/// <summary>
		/// 打开指定 rootPath 下的 region 文件访问器。
		/// <para>若文件不存在、路径不匹配或格式校验失败，则返回 null 并输出错误。</para>
		/// </summary>
		public static ChunkRegionFileAccessor Open(string rootPath, Vector2I regionPosition)
		{
			if (!TryOpenValidatedStream(rootPath, regionPosition, System.IO.FileAccess.ReadWrite, out string regionFilePath, out FileStream stream))
			{
				return null;
			}

			return new ChunkRegionFileAccessor(regionFilePath, regionPosition, stream);
		}

		/// <summary>
		/// 创建指定 chunk 所属的 region 文件。
		/// </summary>
		public static bool CreateRegion(string rootPath, ChunkPosition chunkPosition)
		{
			return CreateRegion(rootPath, ChunkRegionPositionProcessor.GetRegionPosition(chunkPosition));
		}

		/// <summary>
		/// 创建指定 region 坐标对应的 region 文件。
		/// </summary>
		public static bool CreateRegion(string rootPath, Vector2I regionPosition)
		{
			if (!ChunkRegionFilePath.TryGetRegionFilePath(rootPath, regionPosition, out string regionFilePath))
			{
				return false;
			}

			if (File.Exists(regionFilePath))
			{
				return true;
			}

			return ChunkRegionCreater.Create(regionFilePath);
		}

		/// <summary>
		/// 检查指定 chunk 所属的 region 文件是否存在。
		/// </summary>
		public static bool IsRegionFileExists(string rootPath, ChunkPosition chunkPosition)
		{
			return IsRegionFileExists(rootPath, ChunkRegionPositionProcessor.GetRegionPosition(chunkPosition));
		}

		/// <summary>
		/// 检查指定 region 文件是否存在。
		/// </summary>
		public static bool IsRegionFileExists(string rootPath, Vector2I regionPosition)
		{
			return ChunkRegionFilePath.TryGetRegionFilePath(rootPath, regionPosition, out string regionFilePath) &&
				   File.Exists(regionFilePath);
		}

		/// <summary>
		/// 读取格式区域字节。
		/// </summary>
		public byte[] ReadFormatAreaBytes()
		{
			return TryReadBytes(_stream, ChunkRegionFileLayout.FORMAT_AREA_OFFSET_IN_FILE, ChunkRegionFileLayout.FORMAT_AREA_SIZE, out byte[] formatAreaBytes)
				? formatAreaBytes
				: null;
		}

		/// <summary>
		/// 判断当前 region 文件格式是否与标准格式一致。
		/// </summary>
		public bool IsFormatEqualToStandard()
		{
			byte[] formatAreaBytes = ReadFormatAreaBytes();
			return formatAreaBytes != null && ChunkRegionFileLayout.STANDARD_FORMAT.TryCheckRegionFormat(formatAreaBytes, out _);
		}

		/// <summary>
		/// 释放底层文件流。
		/// </summary>
		public void Dispose()
		{
			_stream.Dispose();
		}

		/// <summary>
		/// 为当前类型及其派生类型打开并校验指定 region 文件。
		/// </summary>
		protected static bool TryOpenValidatedStream(
			string rootPath,
			Vector2I regionPosition,
			System.IO.FileAccess fileAccess,
			out string regionFilePath,
			out FileStream stream)
		{
			regionFilePath = null;
			stream = null;

			// 先把逻辑坐标转换成标准路径，后续所有存在性与布局校验都围绕这个标准路径展开。
			if (!ChunkRegionFilePath.TryGetRegionFilePath(rootPath, regionPosition, out regionFilePath))
			{
				GD.PushError($"[ChunkRegionFileAccessor] Open: 无法为 region ({regionPosition.X}, {regionPosition.Y}) 生成有效路径。");
				return false;
			}

			if (!File.Exists(regionFilePath))
			{
				GD.PushError($"[ChunkRegionFileAccessor] Open: region 文件不存在: {regionFilePath}");
				return false;
			}

			if (!ChunkRegionFilePath.IsStandardRegionFilePath(regionFilePath, regionPosition))
			{
				GD.PushError($"[ChunkRegionFileAccessor] Open: region 文件路径与坐标不匹配: {regionFilePath}");
				return false;
			}

			try
			{
				stream = new FileStream(
					regionFilePath,
					new FileStreamOptions
					{
						Mode = FileMode.Open,
						Access = fileAccess,
						Share = FileShare.ReadWrite,
						BufferSize = ChunkRegionFileLayout.PARTITION_ENTRY_SIZE,
						Options = FileOptions.RandomAccess
					});

				// 打开成功并不代表文件可信，还需要确认格式区、介绍区和分区区长度都满足最小约束。
				if (!TryValidateExistingRegionFile(stream, out string errorMessage))
				{
					stream.Dispose();
					stream = null;
					GD.PushError($"[ChunkRegionFileAccessor] Open: region 文件 {regionFilePath} 校验失败: {errorMessage}");
					return false;
				}

				return true;
			}
			catch (Exception exception)
			{
				stream?.Dispose();
				stream = null;
				GD.PushError($"[ChunkRegionFileAccessor] Open: 打开 region 文件 {regionFilePath} 失败: {exception.Message}");
				return false;
			}
		}

		/// <summary>
		/// 校验已有 region 文件的格式与基本结构。
		/// </summary>
		private static bool TryValidateExistingRegionFile(FileStream stream, out string errorMessage)
		{
			if (stream.Length < ChunkRegionFileLayout.PARTITION_AREA_OFFSET_IN_FILE)
			{
				errorMessage = "文件长度小于最小 region 布局长度。";
				return false;
			}

			// 标准格式校验放在最前面，这样后面依赖布局常量的读取才有意义。
			if (!TryReadBytes(
					stream,
					ChunkRegionFileLayout.FORMAT_AREA_OFFSET_IN_FILE,
					ChunkRegionFileLayout.FORMAT_AREA_SIZE,
					out byte[] formatAreaBytes))
			{
				errorMessage = "格式区域读取失败。";
				return false;
			}

			if (!ChunkRegionFileLayout.STANDARD_FORMAT.TryCheckRegionFormat(formatAreaBytes, out errorMessage))
			{
				return false;
			}

			if (!TryReadBytes(
					stream,
					ChunkRegionFileLayout.INTRODUCTION_AREA_OFFSET_IN_FILE,
					_INTRODUCTION_SIGNATURE_BYTES.Length,
					out byte[] signatureBytes))
			{
				errorMessage = "介绍区域签名读取失败。";
				return false;
			}

			if (!signatureBytes.AsSpan().SequenceEqual(_INTRODUCTION_SIGNATURE_BYTES))
			{
				errorMessage = "介绍区域签名不匹配。";
				return false;
			}

			// 分区区域必须按固定分区大小对齐，否则后续任何分区索引换算都会失真。
			long partitionBytesLength = stream.Length - ChunkRegionFileLayout.PARTITION_AREA_OFFSET_IN_FILE;
			if (partitionBytesLength < 0)
			{
				errorMessage = "文件长度小于分区区域起始偏移。";
				return false;
			}

			if (partitionBytesLength % ChunkRegionFileLayout.PARTITION_ENTRY_SIZE != 0)
			{
				errorMessage = "分区区域长度与分区大小不对齐。";
				return false;
			}

			errorMessage = null;
			return true;
		}

		/// <summary>
		/// 从指定文件流中读取固定长度字节。
		/// </summary>
		internal static bool TryReadBytes(FileStream stream, long offsetInFile, int byteCount, out byte[] bytes)
		{
			bytes = null;
			if (stream == null)
			{
				GD.PushError("[ChunkRegionFileAccessor] TryReadBytes: stream 不能为空。");
				return false;
			}

			if (offsetInFile < 0)
			{
				GD.PushError($"[ChunkRegionFileAccessor] TryReadBytes: offsetInFile={offsetInFile} 非法。");
				return false;
			}

			if (byteCount < 0)
			{
				GD.PushError($"[ChunkRegionFileAccessor] TryReadBytes: byteCount={byteCount} 非法。");
				return false;
			}

			try
			{
				bytes = new byte[byteCount];
				stream.Position = offsetInFile;
				stream.ReadExactly(bytes, 0, byteCount);
				return true;
			}
			catch (Exception exception)
			{
				bytes = null;
				GD.PushError($"[ChunkRegionFileAccessor] TryReadBytes: 在偏移 {offsetInFile} 读取 {byteCount} 字节失败: {exception.Message}");
				return false;
			}
		}

		/// <summary>
		/// 将字节数组写入到指定文件偏移位置。
		/// </summary>
		internal static bool TryWriteBytes(FileStream stream, long offsetInFile, ReadOnlySpan<byte> bytes)
		{
			if (stream == null)
			{
				GD.PushError("[ChunkRegionFileAccessor] TryWriteBytes: stream 不能为空。");
				return false;
			}

			if (offsetInFile < 0)
			{
				GD.PushError($"[ChunkRegionFileAccessor] TryWriteBytes: offsetInFile={offsetInFile} 非法。");
				return false;
			}

			try
			{
				stream.Position = offsetInFile;
				stream.Write(bytes);
				return true;
			}
			catch (Exception exception)
			{
				GD.PushError($"[ChunkRegionFileAccessor] TryWriteBytes: 在偏移 {offsetInFile} 写入 {bytes.Length} 字节失败: {exception.Message}");
				return false;
			}
		}
	}
}
