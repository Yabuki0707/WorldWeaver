using System;
using System.Collections.Generic;
using Godot;
using System.Runtime.CompilerServices;

namespace rasu.Map.Chunk
{
    /// <summary>
    /// Tile 变化类型
    /// </summary>
    public enum TileChangeType
    {
        /// <summary>设置Tile</summary>
        Set,
        /// <summary>移除Tile（转为负数，可恢复）</summary>
        Remove,
        /// <summary>恢复Tile</summary>
        Restore,
        /// <summary>删除Tile（设为0，不可恢复）</summary>
        Delete
    }

    /// <summary>
    /// 区块数据容器，为区块数据的持有者。
    /// <para>存储 Tile信息。</para>
    /// <para>创建时必须有对应的区块大小并生成对应规模的数据数组。</para>
    /// <para>注意：Tile的移除操作会将对应位置的Tile类型ID转为负数，表示该Tile处于可恢复状态</para>
    /// </summary>
    public class ChunkData : IDisposable
    {
        /*******************************
                  Tile数据
        ********************************/

        /// <summary>
        /// 所属区块，作为传递tile设置、移除等讯息的主体
        /// </summary>
        public Chunk OwnerChunk { get; private set; }

        /// <summary>
        /// Tile类型ID数组，索引 = y * width + x
        /// 数据按行优先排序(按行依次存储)
        /// 数据复杂后，可独立为只具有数据意义的ChunkStorage类
        /// </summary>
        public int[] Tiles { get; private set; }


        /// <summary>
        /// 获取区块宽度（Tile数量）
        /// </summary>
        public readonly int Width ;

        /// <summary>
        /// 宽度掩码，通过位运算快速检查局部坐标是否在区块范围内
        /// </summary>
        private readonly int _widthMask ;


        /// <summary>
        /// 获取区块高度（Tile数量）
        /// </summary>
        public readonly int Height ;

        /// <summary>
        /// 高度掩码，通过位运算快速检查局部坐标是否在区块范围内
        /// </summary>
        private readonly int _heightMask ;


        /// <summary>
        /// 获取区块大小（Tile数量）
        /// </summary>
        public Vector2I Size => new(Width, Height);

        /// <summary>
        /// 获取Tile总数
        /// </summary>
        public int TileCount => Size.X * Size.Y;


        /*******************************
                  构造与方法
        ********************************/


        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="sizeExp">区块大小指数(区块大小为2^WidthExp * 2^HeightExp)</param>
        /// <param name="referenceTiles">TileType(int类型)数组,尽可能参照内容进行初始化</param>
        public ChunkData(Chunk ownerChunk, Vector2I sizeExp, int[] referenceTiles = null)
        {
            if (sizeExp.X <= 0 || sizeExp.Y <= 0)
            {
                GD.PushError("区块大小指数必须为正整数。");
                throw new ArgumentException("Chunk size exponent must be non-negative.");
            }
            // 初始化所属区块
            OwnerChunk = ownerChunk;
            // 初始化大小
            Width = 1 << sizeExp.X;
            _widthMask = Width - 1;
            
            Height = 1 << sizeExp.Y;
            _heightMask = Height - 1;

            // 初始化Tile数组(默认值为0)，尽可能根据referenceTiles内容进行初始化
            Tiles = new int[Width * Height];
            int countOfReferenceTiles = referenceTiles?.Length ?? 0;
            int referenceCount = (countOfReferenceTiles < TileCount) ? countOfReferenceTiles : TileCount;
            for (int referenceTileIndex = 0; referenceTileIndex < referenceCount; referenceTileIndex++)
            {
                Tiles[referenceTileIndex] = referenceTiles[referenceTileIndex];
            }
            
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="widthExp">区块宽度指数(宽度大小为2^WidthExp)</param>
        /// <param name="heightExp">区块高度指数(高度大小为2^HeightExp)</param>
        /// <param name="referenceTiles">TileType(int类型)数组,尽可能参照内容进行初始化,数据的分配是行优先且从左到右,从上到下</param>
        public ChunkData(Chunk ownerChunk, int widthExp, int heightExp, int[] referenceTiles = null)
        {
            if (widthExp <= 0 || heightExp <= 0)
            {
                GD.PushError("区块大小指数必须为正整数。");
                throw new ArgumentException("Chunk size exponent must be non-negative.");
            }
            // 初始化所属区块
            OwnerChunk = ownerChunk;
            // 初始化大小
            Width = 1 << widthExp;
            _widthMask = Width - 1;
            
            Height = 1 << heightExp;
            _heightMask = Height - 1;
            // 初始化Tile数组(默认值为0)，尽可能根据referenceTiles内容进行初始化
            Tiles = new int[Width * Height];
            int countOfReferenceTiles = referenceTiles?.Length ?? 0;
            int referenceCount = (countOfReferenceTiles < TileCount) ? countOfReferenceTiles : TileCount;
            for (int referenceTileIndex = 0; referenceTileIndex < referenceCount; referenceTileIndex++)
            {
                Tiles[referenceTileIndex] = referenceTiles[referenceTileIndex];
            }
        }


        /*******************************
                  Tile相关
        ********************************/


        /// <summary>
        /// 获取指定位置的Tile类型ID
        /// </summary>
        /// <param name="localX">区块内X坐标</param>
        /// <param name="localY">区块内Y坐标</param>
        /// <returns>Tile类型ID，若坐标越界返回0(无效Tile类型ID)</returns>
        public int GetTile(int localX, int localY)
        {
            if (!IsInBounds(localX, localY))
                return 0;
            return Tiles[localY * Width + localX];
        }

        /// <summary>
        /// 获取指定位置的Tile类型ID
        /// </summary>
        /// <param name="localPosition">区块内局部坐标</param>
        /// <returns>Tile类型ID，若坐标越界返回0(无效Tile类型ID)</returns>
        public int GetTile(Vector2I localPosition)
        {
            return GetTile(localPosition.X, localPosition.Y);
        }


        /// <summary>
        /// 设置指定位置的Tile类型ID
        /// </summary>
        /// <param name="localX">区块内X坐标</param>
        /// <param name="localY">区块内Y坐标</param>
        /// <param name="tileRunId">Tile类型运行ID</param>
        /// <returns>是否设置成功,若坐标越界则返回false</returns>
        public bool SetTile(int localX, int localY, int tileRunId)
        {
            if (!IsInBounds(localX, localY))
                return false;
            int index = localY * Width + localX;
            int oldTileId = Tiles[index];
            Tiles[index] = tileRunId;
            OwnerChunk?.NotifyTileChanged(new TileChangedEventArgs(OwnerChunk.CPosition, new Vector2I(localX, localY), oldTileId, tileRunId, TileChangeType.Set));
            return true;
        }

        /// <summary>
        /// 设置指定位置的Tile类型ID
        /// </summary>
        /// <param name="localPosition">区块内局部坐标</param>
        /// <param name="tileRunId">Tile类型运行ID</param>
        /// <returns>是否设置成功,若坐标越界则返回false</returns>
        public bool SetTile(Vector2I localPosition, int tileRunId)
        {
            return SetTile(localPosition.X, localPosition.Y, tileRunId);
        }


        /// <summary>
        /// 移除指定位置的Tile（将Tile类型ID转为负数，表示该Tile处于可恢复状态）
        /// </summary>
        /// <param name="localX">区块内X坐标</param>
        /// <param name="localY">区块内Y坐标</param>
        /// <returns>是否移除成功,若Tile已处于移除状态或坐标越界则返回false</returns>
        public bool RemoveTile(int localX, int localY)
        {
            if (!IsInBounds(localX, localY))
                return false;
            int index = localY * Width + localX;
            if (Tiles[index] < 0)
                return false;
            int oldTileId = Tiles[index];
            Tiles[index] = -Tiles[index];
            OwnerChunk?.NotifyTileChanged(new TileChangedEventArgs(OwnerChunk.CPosition, new Vector2I(localX, localY), oldTileId, Tiles[index], TileChangeType.Remove));
            return true;
        }

        /// <summary>
        /// 移除指定位置的Tile（将Tile类型ID转为负数，表示该Tile处于可恢复状态）
        /// </summary>
        /// <param name="localPosition">区块内局部坐标</param>
        /// <returns>是否移除成功,若Tile已处于移除状态或坐标越界则返回false</returns>
        public bool RemoveTile(Vector2I localPosition)
        {
            return RemoveTile(localPosition.X, localPosition.Y);
        }


        /// <summary>
        /// 恢复指定位置的Tile（仅当Tile类型ID为负数时有效）
        /// </summary>
        /// <param name="localX">区块内X坐标</param>
        /// <param name="localY">区块内Y坐标</param>
        /// <returns>是否恢复成功,若Tile已处于恢复状态或坐标越界则返回false</returns>
        public bool RestoreTile(int localX, int localY)
        {
            if (!IsInBounds(localX, localY))
                return false;
            int index = localY * Width + localX;
            if (Tiles[index] >= 0)
                return false;
            int oldTileId = Tiles[index];
            Tiles[index] = -Tiles[index];
            OwnerChunk?.NotifyTileChanged(new TileChangedEventArgs(OwnerChunk.CPosition, new Vector2I(localX, localY), oldTileId, Tiles[index], TileChangeType.Restore));
            return true;
        }

        /// <summary>
        /// 恢复指定位置的Tile（仅当Tile类型ID为负数时有效）
        /// </summary>
        /// <param name="localPosition">区块内局部坐标</param>
        /// <returns>是否恢复成功,若Tile已处于恢复状态或坐标越界则返回false</returns>
        public bool RestoreTile(Vector2I localPosition)
        {
            return RestoreTile(localPosition.X, localPosition.Y);
        }


        /// <summary>
        /// 删除指定位置的Tile（将Tile类型ID设为0，不可恢复）
        /// </summary>
        /// <param name="localX">区块内X坐标</param>
        /// <param name="localY">区块内Y坐标</param>
        /// <returns>是否删除成功,若坐标越界则返回false</returns>
        public bool DeleteTile(int localX, int localY)
        {
            if (!IsInBounds(localX, localY))
                return false;
            int index = localY * Width + localX;
            int oldTileId = Tiles[index];
            Tiles[index] = 0;
            OwnerChunk?.NotifyTileChanged(new TileChangedEventArgs(OwnerChunk.CPosition, new Vector2I(localX, localY), oldTileId, 0, TileChangeType.Delete));
            return true;
        }

        /// <summary>
        /// 删除指定位置的Tile（将Tile类型ID设为0，不可恢复）
        /// </summary>
        /// <param name="localPosition">区块内局部坐标</param>
        /// <returns>是否删除成功,若坐标越界则返回false</returns>
        public bool DeleteTile(Vector2I localPosition)
        {
            return DeleteTile(localPosition.X, localPosition.Y);
        }


        /// <summary>
        /// 检查指定区块内的局部坐标(localX, localY)是否在区块范围内
        /// </summary>
        /// <param name="localX">区块内X坐标</param>
        /// <param name="localY">区块内Y坐标</param>
        /// <returns>是否在范围内</returns>
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsInBounds(int localX, int localY)
        {
            return ((localX & _widthMask) == localX) && ((localY & _heightMask) == localY);
        }

        /// <summary>
        /// 检查指定区块内的局部坐标是否在区块范围内
        /// </summary>
        /// <param name="localPosition">区块内局部坐标</param>
        /// <returns>是否在范围内</returns>
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsInBounds(Vector2I localPosition)
        {
            return IsInBounds(localPosition.X, localPosition.Y);
        }


        /// <summary>
        /// 检查指定位置的Tile是否可恢复（Tile类型ID为负数）
        /// </summary>
        /// <param name="localX">区块内X坐标</param>
        /// <param name="localY">区块内Y坐标</param>
        /// <returns>是否可恢复</returns>
        public bool IsTileRestorable(int localX, int localY)
        {
            if (!IsInBounds(localX, localY))
            {
                return false;
            }
            return Tiles[localY * Width + localX] < 0;
        }

        /// <summary>
        /// 检查指定位置的Tile是否可恢复（Tile类型ID为负数）
        /// </summary>
        /// <param name="localPosition">区块内局部坐标</param>
        /// <returns>是否可恢复</returns>
        public bool IsTileRestorable(Vector2I localPosition)
        {
            return IsTileRestorable(localPosition.X, localPosition.Y);
        }


        /// <summary>
        /// 移除所有Tile数据（将所有Tile类型ID转为负数，表示所有Tile处于可恢复状态）
        /// 注意：如果需要彻底清除Tile数据，请使用FillTiles()方法
        /// </summary>
        public void RemoveAllTiles()
        {
            for (int tileIndex = 0; tileIndex < Tiles.Length; tileIndex++)
            {
                if (Tiles[tileIndex] > 0)
                {
                    Tiles[tileIndex] = -Tiles[tileIndex];
                }
            }
        }

        /// <summary>
        /// 填充所有Tile数据（设置为指定值）
        /// </summary>
        public void FillTiles(int fillValue = 0)
        {
            for (int tileIndex = 0; tileIndex < Tiles.Length; tileIndex++)
            {
                Tiles[tileIndex] = fillValue;
            }
        }



        /*******************************
                  工具方法
        ********************************/

        /// <summary>
        /// 克隆当前区块数据（深拷贝）
        /// <para>用于多线程保存时的快照，防止保存过程中数据被主线程修改</para>
        /// </summary>
        public int[] Clone()
        {
            // 额外Tiles正确性检查(Tiles基本上不可能为null)
            if (Tiles == null)
                return null;
            // 创建新实例
            int[] clone = (int[])Tiles.Clone();
            return clone;
        }


        /*******************************
                  IDisposable 实现
        ********************************/


        /// <summary>
        /// 释放 ChunkData 资源
        /// </summary>
        public void Dispose()
        {
            // 清理托管资源
            Tiles = null;
            GC.SuppressFinalize(this);
        }

    }
}
