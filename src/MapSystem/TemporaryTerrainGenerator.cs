using System;
using Godot;
using WorldWeaver.MapSystem.TileSystem;

namespace WorldWeaver.MapSystem
{
    /// <summary>
    /// 临时地形生成器。
    /// <para>该类型当前只是一个简陋临时实现，用于根据全局闭区间范围与种子批量生成 TileRunId 数组。</para>
    /// <para>当前实现直接使用 <see cref="FastNoiseLite"/> 采样，并通过固定阈值映射到浅海、沙地、草地三种 TileType。</para>
    /// <para>数组顺序固定为：从上到下、从左到右。</para>
    /// </summary>
    public static class TemporaryTerrainGenerator
    {
        // ================================================================================
        //                                  临时常量
        // ================================================================================

        /// <summary>
        /// 当前临时实现使用的噪声频率。
        /// </summary>
        private const float TEMP_NOISE_FREQUENCY = 0.02f;

        /// <summary>
        /// 浅海阈值。
        /// <para>归一化噪声值小于该阈值时，生成浅海 Tile。</para>
        /// </summary>
        private const float SHALLOW_SEA_THRESHOLD = 0.57f;

        /// <summary>
        /// 沙地阈值。
        /// <para>归一化噪声值小于该阈值且不小于浅海阈值时，生成沙地 Tile。</para>
        /// </summary>
        private const float SAND_THRESHOLD = 0.67f;

        /// <summary>
        /// 当前临时实现依赖的 TileType 名称数组。
        /// <para>索引 0=shallow_sea，1=sand，2=grass。</para>
        /// </summary>
        private static readonly string[] _TEMP_TILE_TYPE_NAMES = ["shallow_sea", "sand", "grass"];

        /// <summary>
        /// 当前临时实现依赖的 TileType 缓存。
        /// <para>该缓存会在首次生成时按名称解析，并在后续生成中重复使用，以避免频繁访问 <see cref="TileTypeManager"/>。</para>
        /// </summary>
        private static TileType[] tempTerrainTileTypes;


        // ================================================================================
        //                                  对外生成方法
        // ================================================================================

        /// <summary>
        /// 根据全局闭区间范围与随机种子生成对应的 TileRunId 数组。
        /// <para>该方法当前属于简陋临时实现：仅支持浅海、沙地、草地三档阈值映射。</para>
        /// <para>返回数组顺序固定为从上到下、从左到右。</para>
        /// </summary>
        /// <param name="minGlobalPosition">闭区间最小全局点坐标。</param>
        /// <param name="maxGlobalPosition">闭区间最大全局点坐标。</param>
        /// <param name="seed">噪声种子。</param>
        /// <returns>与输入闭区间逐点对应的 TileRunId 数组。</returns>
        public static int[] GenerateTileRunIds(Vector2I minGlobalPosition, Vector2I maxGlobalPosition, int seed)
        {
            if (minGlobalPosition.X > maxGlobalPosition.X || minGlobalPosition.Y > maxGlobalPosition.Y)
            {
                GD.PushError($"[TemporaryTerrainGenerator]: 输入范围非法，min={minGlobalPosition}, max={maxGlobalPosition}。该方法要求闭区间最小点不大于最大点。");
                return Array.Empty<int>();
            }

            // 闭区间宽度包含最小/最大端点，因此需要 +1。
            int width = maxGlobalPosition.X - minGlobalPosition.X + 1;

            // 闭区间高度同样包含最小/最大端点，因此需要 +1。
            int height = maxGlobalPosition.Y - minGlobalPosition.Y + 1;

            // 解析当前临时实现依赖的三种 TileType，并在首次使用后缓存结果。
            TileType[] terrainTileTypes = ResolveTemporaryTerrainTileTypes();
            if (terrainTileTypes == null)
            {
                return Array.Empty<int>();
            }

            // 创建 Godot 的 FastNoiseLite，并使用固定频率做临时地形采样。
            FastNoiseLite fastNoise = new()
            {
                Seed = seed,
                Frequency = TEMP_NOISE_FREQUENCY
            };

            int[] generatedTileRunIds = new int[width * height];
            int tileIndex = 0;

            for (int globalY = minGlobalPosition.Y; globalY <= maxGlobalPosition.Y; globalY++)
            {
                for (int globalX = minGlobalPosition.X; globalX <= maxGlobalPosition.X; globalX++)
                {
                    // FastNoiseLite 原始输出范围通常为 [-1, 1]，这里先归一化到 [0, 1]，再做临时阈值映射。
                    float normalizedNoiseValue = (fastNoise.GetNoise2D(0.5f*globalX, 0.5f*globalY) + 1.0f) * 0.5f;

                    // 根据临时阈值选择地形类型，并写入对应的 TileRunId。
                    generatedTileRunIds[tileIndex] = SelectTemporaryTerrainTileRunId(normalizedNoiseValue, terrainTileTypes);
                    tileIndex++;
                }
            }

            return generatedTileRunIds;
        }


        // ================================================================================
        //                                  私有辅助方法
        // ================================================================================

        /// <summary>
        /// 解析当前临时实现依赖的三种 TileType。
        /// </summary>
        /// <returns>索引 0=浅海，1=沙地，2=草地的 TileType 数组；若任一 TileType 缺失则返回 <see langword="null"/>。</returns>
        private static TileType[] ResolveTemporaryTerrainTileTypes()
        {
            if (tempTerrainTileTypes != null)
            {
                return tempTerrainTileTypes;
            }

            TileType[] terrainTileTypes = new TileType[_TEMP_TILE_TYPE_NAMES.Length];

            for (int tileTypeIndex = 0; tileTypeIndex < _TEMP_TILE_TYPE_NAMES.Length; tileTypeIndex++)
            {
                // 当前临时地形类型名称。
                string tileTypeName = _TEMP_TILE_TYPE_NAMES[tileTypeIndex];
                TileType tileType = TileTypeManager.GetTypeByName(tileTypeName);

                if (tileType == null || tileType.TileTypeRunId <= 0)
                {
                    GD.PushError($"[TemporaryTerrainGenerator]: 无法生成临时地形，TileType '{tileTypeName}' 未找到或其 RunId 非法。请先确保已初始化并存在 shallow_sea/sand/grass。"
                    );
                    return null;
                }

                terrainTileTypes[tileTypeIndex] = tileType;
            }

            tempTerrainTileTypes = terrainTileTypes;
            return tempTerrainTileTypes;
        }

        /// <summary>
        /// 根据归一化噪声值选择临时地形对应的 TileRunId。
        /// </summary>
        private static int SelectTemporaryTerrainTileRunId(float normalizedNoiseValue, TileType[] terrainTileTypes)
        {
            if (normalizedNoiseValue < SHALLOW_SEA_THRESHOLD)
            {
                return terrainTileTypes[0].TileTypeRunId;
            }

            if (normalizedNoiseValue < SAND_THRESHOLD)
            {
                return terrainTileTypes[1].TileTypeRunId;
            }

            return terrainTileTypes[2].TileTypeRunId;
        }
    }
}
