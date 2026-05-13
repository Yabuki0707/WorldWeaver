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
        /// 入口类全限定名。无此字段即为香草。
        /// </summary>
        [JsonPropertyName("entry_class")]
        public string EntryClass { get; set; }

        /// <summary>
        /// 强制依赖。
        /// </summary>
        [JsonPropertyName("dependencies")]
        public List<string> Dependencies { get; set; }

        /// <summary>
        /// 软依赖——存在则确保先于本模组加载，不存在则跳过。
        /// </summary>
        [JsonPropertyName("soft_dependencies")]
        public List<string> SoftDependencies { get; set; }

        /// <summary>
        /// 是否有 entry_class——无则为香草。
        /// </summary>
        [JsonIgnore]
        public bool IsVanilla => string.IsNullOrEmpty(EntryClass);
    }
}
