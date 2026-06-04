using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WorldWeaver.ModSystem
{
    /// <summary>
    /// mod.json 反序列化载体。
    /// </summary>
    public sealed class ModManifest
    {
        /// <summary>
        /// 模组名称，全局唯一。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// 版本号。
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; }

        /// <summary>
        /// 强制依赖。
        /// </summary>
        [JsonPropertyName("dependencies")]
        public List<string> Dependencies { get; set; }
    }
}
