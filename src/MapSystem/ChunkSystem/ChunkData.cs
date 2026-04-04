using System;
using System.Runtime.CompilerServices;
using Godot;
using WorldWeaver.MapSystem.TileSystem;

namespace WorldWeaver.MapSystem.ChunkSystem
{
    /// <summary>
    /// Tile 变更类型。
    /// </summary>
    public enum TileChangeType
    {
        /// <summary>
        /// 将目标 Tile 设置为指定值。
        /// </summary>
        Set,

        /// <summary>
        /// 逻辑移除 Tile。
        /// <para>实现方式为将正数 TileRunId 取反，表示该 Tile 仍可恢复。</para>
        /// </summary>
        Remove,

        /// <summary>
        /// 恢复此前被逻辑移除的 Tile。
        /// </summary>
        Restore,

        /// <summary>
        /// 彻底删除 Tile。
        /// <para>实现方式为将 TileRunId 置为 0，且删除后不可恢复。</para>
        /// </summary>
        Delete
    }

    /// <summary>
    /// Chunk 的 Tile 纯数据容器。
    /// <para>该类仅负责本 Chunk 内的 Tile 数组存储、局部坐标访问与索引级原子操作。</para>
    /// <para>所有基于全局 shape 的切片与分发由 ChunkManager.ChunkDataOperator 负责。</para>
    /// </summary>
    public class ChunkData : IDisposable
    {
        // ================================================================================
        //                                  基础属性
        // ================================================================================
        

        /// <summary>
        /// Tile 数据数组。
        /// <para>索引规则：<c>tileIndex = localY * ElementSize.Width + localX</c>。</para>
        /// </summary>
        public int[] Tiles { get; private set; }

        /// <summary>
        /// Chunk 的尺寸对象。
        /// </summary>
        public MapElementSize ElementSize { get; }

        /// <summary>
        /// Chunk 的二维尺寸。
        /// </summary>
        public Vector2I Size => ElementSize.Size;

        /// <summary>
        /// Chunk 的宽度（Tile 数）。
        /// </summary>
        public int Width => ElementSize.Width;

        /// <summary>
        /// Chunk 的高度（Tile 数）。
        /// </summary>
        public int Height => ElementSize.Height;

        /// <summary>
        /// 当前 Tile 数组长度。
        /// </summary>
        public int TileCount => Tiles?.Length ?? 0;


        // ================================================================================
        //                                  构造与释放
        // ================================================================================

        /// <summary>
        /// 创建 ChunkData。
        /// </summary>
        public ChunkData(MapElementSize elementSize, int[] referenceTiles = null)
        {
            ElementSize = elementSize ?? throw new ArgumentNullException(nameof(elementSize));
            Tiles = new int[ElementSize.Area];

            if (referenceTiles != null)
            {
                Array.Copy(referenceTiles, Tiles, Math.Min(referenceTiles.Length, Tiles.Length));
            }
        }


        // ================================================================================
        //                                  原子 Tile 操作
        // ================================================================================

        /// <summary>
        /// 单点设置指定索引处的 Tile。
        /// <para>该方法不校验索引合法性，调用方必须保证 <paramref name="tileIndex"/> 已经有效。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int SetTileSingleUnchecked(int tileIndex, int tileRunId)
        {
            Tiles[tileIndex] = tileRunId;
            return tileRunId;
        }

        /// <summary>
        /// 单点逻辑移除指定索引处的 Tile。
        /// <para>该方法不校验索引合法性，调用方必须保证 <paramref name="tileIndex"/> 已经有效。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int RemoveTileSingleUnchecked(int tileIndex)
        {
            ref int tileRunId = ref Tiles[tileIndex];
            if (tileRunId > 0)
            {
                tileRunId = -tileRunId;
            }
            
            return tileRunId;
        }

        /// <summary>
        /// 单点恢复指定索引处的 Tile。
        /// <para>该方法不校验索引合法性，调用方必须保证 <paramref name="tileIndex"/> 已经有效。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int RestoreTileSingleUnchecked(int tileIndex)
        {
            ref int tileRunId = ref Tiles[tileIndex];
            if (tileRunId < 0)
            {
                tileRunId = -tileRunId;
            }
            
            return tileRunId;
        }

        /// <summary>
        /// 单点彻底删除指定索引处的 Tile。
        /// <para>该方法不校验索引合法性，调用方必须保证 <paramref name="tileIndex"/> 已经有效。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int DeleteTileSingleUnchecked(int tileIndex)
        {
            Tiles[tileIndex] = 0;
            return 0;
        }

        /// <summary>
        /// 单点读取指定索引处的 Tile。
        /// <para>读取同样不做边界检查，调用方必须保证索引有效。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetTileSingleUnchecked(int tileIndex)
        {
            return Tiles[tileIndex];
        }


        // ================================================================================
        //                                  局部读取接口
        // ================================================================================

        /// <summary>
        /// 读取指定局部坐标的 TileRunId。
        /// <para>越界时返回 <see langword="null"/>。</para>
        /// </summary>
        public int? GetTile(int localX, int localY)
        {
            return TryGetTileIndex(localX, localY, out int tileIndex) ? Tiles[tileIndex] : null;
        }

        /// <summary>
        /// 读取指定局部坐标的 TileRunId。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int? GetTile(Vector2I localPosition)
        {
            return GetTile(localPosition.X, localPosition.Y);
        }


        // ================================================================================
        //                                  整块批量操作
        // ================================================================================

        /// <summary>
        /// 将所有可见 Tile 标记为已移除状态。
        /// </summary>
        public void RemoveAllTiles()
        {
            for (int tileIndex = 0; tileIndex < Tiles.Length; tileIndex++)
            {
                if (Tiles[tileIndex] <= 0)
                {
                    continue;
                }

                RemoveTileSingleUnchecked(tileIndex);
            }
        }

        /// <summary>
        /// 将所有可恢复 Tile 恢复为可见状态。
        /// </summary>
        public void RestoreAllTiles()
        {
            for (int tileIndex = 0; tileIndex < Tiles.Length; tileIndex++)
            {
                if (Tiles[tileIndex] >= 0)
                {
                    continue;
                }

                RestoreTileSingleUnchecked(tileIndex);
            }
        }

        /// <summary>
        /// 使用指定值填充全部 Tile。
        /// </summary>
        public void FillTiles(int fillValue = 0)
        {
            for (int tileIndex = 0; tileIndex < Tiles.Length; tileIndex++)
            {
                if (Tiles[tileIndex] == fillValue)
                {
                    continue;
                }

                SetTileSingleUnchecked(tileIndex, fillValue);
            }
        }

        /// <summary>
        /// 深拷贝当前 Tile 数组。
        /// </summary>
        public int[] Clone()
        {
            return Tiles == null ? null : (int[])Tiles.Clone();
        }


        // ================================================================================
        //                                  边界与索引转换
        // ================================================================================

        /// <summary>
        /// 判断局部坐标是否位于当前 Chunk 范围内。
        /// </summary>
        public bool IsInBounds(int localX, int localY)
        {
            return ElementSize.IsValidLocalPosition(localX, localY);
        }

        /// <summary>
        /// 判断局部坐标是否位于当前 Chunk 范围内。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsInBounds(Vector2I localPosition)
        {
            return IsInBounds(localPosition.X, localPosition.Y);
        }

        /// <summary>
        /// 将局部坐标转换为 Tile 索引。
        /// <para>越界时返回 <see langword="false"/>。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetTileIndex(int localX, int localY, out int tileIndex)
        {
            if (!IsInBounds(localX, localY))
            {
                tileIndex = -1;
                return false;
            }

            tileIndex = LocalTilePositionConverter.ToTileIndex(new Vector2I(localX, localY), ElementSize);
            return true;
        }


        // ================================================================================
        //                                  释放资源
        // ================================================================================

        /// <summary>
        /// 释放 ChunkData 持有的托管资源。
        /// </summary>
        public void Dispose()
        {
            Tiles = null;
            GC.SuppressFinalize(this);
        }
    }
}
