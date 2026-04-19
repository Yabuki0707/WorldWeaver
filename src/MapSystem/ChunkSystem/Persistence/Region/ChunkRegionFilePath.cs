using System;
using System.IO;
using Godot;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence.Region
{
    /// <summary>
    /// ChunkRegion 文件路径工具。
    /// <para>该静态类负责根据根目录与区块/region 坐标生成标准 region 文件路径，并校验路径是否符合标准布局。</para>
    /// </summary>
    public static class ChunkRegionFilePath
    {
        /// <summary>
        /// 根据根目录与区块坐标生成对应的 region 文件完整路径。
        /// </summary>
        public static bool TryGetRegionFilePath(string rootPath, ChunkPosition chunkPosition, out string regionFilePath)
        {
            return TryGetRegionFilePath(rootPath, ChunkRegionPositionProcessor.GetRegionPosition(chunkPosition), out regionFilePath);
        }

        /// <summary>
        /// 根据根目录与 region 坐标生成对应的 region 文件完整路径。
        /// </summary>
        public static bool TryGetRegionFilePath(string rootPath, Vector2I regionPosition, out string regionFilePath)
        {
            regionFilePath = null;
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                GD.PushError("[ChunkRegionFilePath] TryGetRegionFilePath: rootPath 不能为空。");
                return false;
            }

            string absoluteRootPath = ProjectSettings.GlobalizePath(rootPath);
            if (string.IsNullOrWhiteSpace(absoluteRootPath))
            {
                GD.PushError($"[ChunkRegionFilePath] TryGetRegionFilePath: rootPath {rootPath} 无法转换为有效绝对路径。");
                return false;
            }

            regionFilePath = Path.Combine(
                absoluteRootPath,
                GetRangeDirectoryName(regionPosition),
                GetExpectedRegionFileName(regionPosition));
            return true;
        }

        /// <summary>
        /// 检查指定路径是否符合给定 region 坐标的标准路径布局。
        /// </summary>
        public static bool IsStandardRegionFilePath(string regionFilePath, Vector2I regionPosition)
        {
            if (string.IsNullOrWhiteSpace(regionFilePath))
            {
                return false;
            }

            string normalizedRegionFilePath = Path.GetFullPath(regionFilePath);
            if (!string.Equals(
                    Path.GetExtension(normalizedRegionFilePath),
                    ChunkRegionFileLayout.FILE_EXTENSION,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string rangeDirectoryPath = Path.GetDirectoryName(normalizedRegionFilePath);
            if (string.IsNullOrWhiteSpace(rangeDirectoryPath))
            {
                return false;
            }

            return string.Equals(
                       Path.GetFileName(rangeDirectoryPath),
                       GetRangeDirectoryName(regionPosition),
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       Path.GetFileName(normalizedRegionFilePath),
                       GetExpectedRegionFileName(regionPosition),
                       StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取指定 region 所属的 range 坐标。
        /// </summary>
        private static Vector2I GetRangePosition(Vector2I regionPosition)
        {
            return new Vector2I(regionPosition.X >> 4, regionPosition.Y >> 4);
        }

        /// <summary>
        /// 获取指定 region 所属的 range 目录名。
        /// </summary>
        private static string GetRangeDirectoryName(Vector2I regionPosition)
        {
            Vector2I rangePosition = GetRangePosition(regionPosition);
            return $"range_{rangePosition.X}_{rangePosition.Y}";
        }

        /// <summary>
        /// 获取指定 region 的标准文件名。
        /// </summary>
        private static string GetExpectedRegionFileName(Vector2I regionPosition)
        {
            return $"{regionPosition.ToKey()}{ChunkRegionFileLayout.FILE_EXTENSION}";
        }
    }
}
