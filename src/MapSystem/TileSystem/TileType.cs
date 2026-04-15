using System;
using Godot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WorldWeaver.MapSystem.TileSystem
{
    /// <summary>
    /// TileType数据结构，表示一种瓦片类型
    /// 有效的TileTypeRunId从1开始分配,0为无效TileTypeRunId
    /// </summary>
    public class TileType : Object
    {
        /// <summary>
        /// TileType名称
        /// </summary>
        public string TileTypeName { get; set; }

        /// <summary>
        /// TileType纹理坐标。
        /// <para>当前语义为 TileSet atlas 中的二维坐标。</para>
        /// <para>为了兼容旧数据，若 JSON 中仍使用单个整数，则会自动转换为 <c>Vector2I(x, 0)</c>。</para>
        /// </summary>
        [JsonConverter(typeof(TileTypeTextureIdConverter))]
        public Vector2I TileTypeTextureId { get; set; }

        /// <summary>
        /// 是否可通行
        /// </summary>
        public bool IsPassable { get; set; }

        /// <summary>
        /// TileType的运行ID（在初始化过程中分配，未分配时为0）
        /// </summary>
        public int TileTypeRunId { get; set; } = 0;

        /// <summary>
        /// 将TileType数据转换为字符串表示
        /// </summary>
        /// <returns>TileType数据的字符串表示</returns>
        public override string ToString()
        {
            return $"TileType [RunId: {TileTypeRunId}, Name: {TileTypeName}, TextureId: {TileTypeTextureId}, IsPassable: {IsPassable}]";
        }
    }

    /// <summary>
    /// TileType 纹理坐标转换器。
    /// <para>兼容两种 JSON 结构：</para>
    /// <para>1. 旧格式：单个整数，自动解释为 <c>Vector2I(x, 0)</c>；</para>
    /// <para>2. 新格式：显式的二维坐标对象或数组。</para>
    /// </summary>
    public sealed class TileTypeTextureIdConverter : JsonConverter<Vector2I>
    {
        /// <summary>
        /// 将 JSON 读取为二维纹理坐标。
        /// </summary>
        public override Vector2I ReadJson(JsonReader reader, Type objectType, Vector2I existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return Vector2I.Zero;
            }

            // 兼容旧格式：单个整数默认映射到第 0 行。
            if (reader.TokenType == JsonToken.Integer)
            {
                int x = Convert.ToInt32(reader.Value);
                return new Vector2I(x, 0);
            }

            JToken token = JToken.Load(reader);

            // 兼容对象格式：{ x: 1, y: 2 } 或 { X: 1, Y: 2 }。
            if (token.Type == JTokenType.Object)
            {
                int x = token["x"]?.Value<int?>()
                    ?? token["X"]?.Value<int?>()
                    ?? 0;
                int y = token["y"]?.Value<int?>()
                    ?? token["Y"]?.Value<int?>()
                    ?? 0;
                return new Vector2I(x, y);
            }

            // 兼容数组格式：[x, y]。
            if (token.Type == JTokenType.Array)
            {
                JArray array = (JArray)token;
                int x = array.Count > 0 ? array[0]?.Value<int>() ?? 0 : 0;
                int y = array.Count > 1 ? array[1]?.Value<int>() ?? 0 : 0;
                return new Vector2I(x, y);
            }

            throw new JsonSerializationException($"无法将 token '{token}' 解析为 TileTypeTextureId。");
        }

        /// <summary>
        /// 将二维纹理坐标写回 JSON。
        /// </summary>
        public override void WriteJson(JsonWriter writer, Vector2I value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.X);
            writer.WritePropertyName("y");
            writer.WriteValue(value.Y);
            writer.WriteEndObject();
        }
    }
}
