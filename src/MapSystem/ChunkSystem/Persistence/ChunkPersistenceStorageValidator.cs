using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// 区块持久化储存对象校验工具。
    /// <para>该类型只校验从 region 文件读取出的 <see cref="ChunkDataStorage"/> 是否能安全进入缓存表。</para>
    /// </summary>
    internal static class ChunkPersistenceStorageValidator
    {
        /// <summary>
        /// 校验读取到的储存对象是否与当前区块尺寸匹配。
        /// <para>storage 为 null 表示磁盘中没有该 chunk 的旧数据，属于成功结果。</para>
        /// </summary>
        /// <param name="storage">从 region 文件读取到的区块储存对象；可以为 null。</param>
        /// <param name="expectedChunkSize">当前地图层要求的 chunk 尺寸。</param>
        /// <returns>储存对象是否可以被缓存器接受。</returns>
        public static PersistenceRequestResult ValidateLoadedStorage(ChunkDataStorage storage, MapElementSize expectedChunkSize)
        {
            if (storage == null)
            {
                // null 是合法读取结果，表示文件里没有该 chunk 的历史储存数据。
                return PersistenceRequestResult.Success;
            }

            // 尺寸指数非法时不能继续构造 MapElementSize，否则可能掩盖坏文件数据。
            if (!MapElementSize.IsValidExp(storage.WidthExp, storage.HeightExp))
            {
                GD.PushError("[ChunkPersistenceStorageValidator] 已读取的 ChunkDataStorage 尺寸指数非法。");
                return PersistenceRequestResult.PermanentFailure;
            }

            // 读取到的数据必须与当前地图层 chunk 尺寸完全一致，否则不能用于恢复运行时 chunk。
            MapElementSize storageSize = new(storage.WidthExp, storage.HeightExp);
            if (storageSize != expectedChunkSize)
            {
                GD.PushError("[ChunkPersistenceStorageValidator] 已读取的 ChunkDataStorage 尺寸与当前区块尺寸不匹配。");
                return PersistenceRequestResult.PermanentFailure;
            }

            // Tiles 数组是 ChunkDataStorage 的主体数据，不能为空。
            if (storage.Tiles == null)
            {
                GD.PushError("[ChunkPersistenceStorageValidator] 已读取的 ChunkDataStorage.Tiles 不能为空。");
                return PersistenceRequestResult.PermanentFailure;
            }

            // 数组长度必须与 chunk 面积一致，防止后续恢复时越界或丢 tile。
            if (storage.Tiles.Length != expectedChunkSize.Area)
            {
                GD.PushError("[ChunkPersistenceStorageValidator] 已读取的 ChunkDataStorage.Tiles 长度与当前区块面积不匹配。");
                return PersistenceRequestResult.PermanentFailure;
            }

            // 所有结构约束都满足后，读取结果才允许进入缓存表。
            return PersistenceRequestResult.Success;
        }
    }
}
