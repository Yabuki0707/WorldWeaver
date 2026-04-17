using System;
using System.Text;
using Newtonsoft.Json;
using ZstdSharp;

namespace WorldWeaver.MapSystem.ChunkSystem.Data
{
    /// <summary>
    /// ChunkData 的持久化储存对象。
    /// <para>该类型只承载可序列化数据，不提供运行时 Tile 操作能力。</para>
    /// </summary>
    public sealed class ChunkDataStorage
    {
        /// <summary>
        /// ChunkDataStorage 统一 JSON 配置。
        /// </summary>
        private static readonly JsonSerializerSettings _JSON_SETTINGS = new()
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        /// <summary>
        /// 区块宽度指数。
        /// </summary>
        [JsonProperty("we")]
        public int WidthExp { get; set; }

        /// <summary>
        /// 区块高度指数。
        /// </summary>
        [JsonProperty("he")]
        public int HeightExp { get; set; }

        /// <summary>
        /// Tile 数据数组。
        /// </summary>
        [JsonProperty("ts")]
        public int[] Tiles { get; set; }

        /// <summary>
        /// 从运行时 ChunkData 创建储存对象。
        /// </summary>
        public static ChunkDataStorage FromData(ChunkData data)
        {
            if (data == null || data.ElementSize == null || data.Tiles == null)
            {
                return null;
            }

            if (data.Tiles.Length != data.ElementSize.Area)
            {
                return null;
            }

            return new ChunkDataStorage
            {
                WidthExp = data.ElementSize.WidthExp,
                HeightExp = data.ElementSize.HeightExp,
                Tiles = data.Clone()
            };
        }

        /// <summary>
        /// 转换为运行时 ChunkData。
        /// <para>储存对象结构非法时返回 <see langword="null"/>。</para>
        /// </summary>
        public ChunkData ToData()
        {
            if (!MapElementSize.IsValidExp(WidthExp, HeightExp))
            {
                return null;
            }

            MapElementSize elementSize = new(WidthExp, HeightExp);
            if (Tiles == null || Tiles.Length != elementSize.Area)
            {
                return null;
            }

            return new ChunkData(elementSize, Tiles);
        }

        /// <summary>
        /// 转换为不带缩进的 JSON 字符串。
        /// </summary>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.None, _JSON_SETTINGS);
        }

        /// <summary>
        /// 转换为 zstd 压缩后的 UTF-8 JSON 字节数组。
        /// </summary>
        public byte[] ToCompressedBytes()
        {
            byte[] jsonBytes = Encoding.UTF8.GetBytes(ToJson());
            using Compressor compressor = new();
            return compressor.Wrap(jsonBytes).ToArray();
        }

        /// <summary>
        /// 从 zstd 压缩字节数组构建储存对象。
        /// <para>解压或反序列化失败时返回 <see langword="null"/>。</para>
        /// </summary>
        public static ChunkDataStorage FromCompressedBytes(byte[] compressedBytes)
        {
            if (compressedBytes == null || compressedBytes.Length == 0)
            {
                return null;
            }

            try
            {
                using Decompressor decompressor = new();
                byte[] jsonBytes = decompressor.Unwrap(compressedBytes).ToArray();
                string json = Encoding.UTF8.GetString(jsonBytes);
                return JsonConvert.DeserializeObject<ChunkDataStorage>(json, _JSON_SETTINGS);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
