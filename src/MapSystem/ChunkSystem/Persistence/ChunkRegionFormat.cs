using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using Newtonsoft.Json.Linq;

namespace WorldWeaver.MapSystem.ChunkSystem.Persistence
{
    /// <summary>
    /// ChunkRegion 格式对象。
    /// <para>该类型只负责维护区域格式列表、提供字段查询，以及校验文件中的格式数据是否满足标准格式。</para>
    /// </summary>
    public sealed class ChunkRegionFormat
    {
        /// <summary>
        /// 按区域顺序排列的区域信息字典列表。
        /// </summary>
        public IReadOnlyList<Dictionary<string, object>> AreaDictionaryList { get; }

        /// <summary>
        /// 构造完成后缓存的标准 JSON 对象。
        /// <para>后续格式校验会基于该缓存对象执行包含式比较。</para>
        /// </summary>
        private readonly JToken _cachedJsonObject;

        /// <summary>
        /// 使用按顺序排列的区域信息字典构造 ChunkRegion 格式对象。
        /// </summary>
        public ChunkRegionFormat(params Dictionary<string, object>[] areaDictionaryList)
        {
            if (areaDictionaryList == null)
            {
                GD.PushError("ChunkRegionFormat 构造失败：areaDictionaryList 不能为空。");
                areaDictionaryList = Array.Empty<Dictionary<string, object>>();
            }

            if (areaDictionaryList.Length == 0) GD.PushError("ChunkRegionFormat 构造失败：areaDictionaryList 不能为空。");

            List<Dictionary<string, object>> clonedAreaDictionaryList = new(areaDictionaryList.Length);
            foreach (Dictionary<string, object> areaDictionary in areaDictionaryList)
            {
                if (areaDictionary == null)
                {
                    GD.PushError("ChunkRegionFormat 构造失败：areaDictionaryList 中存在空区域字典。");
                    continue;
                }

                clonedAreaDictionaryList.Add(CloneDictionary(areaDictionary));
            }

            AreaDictionaryList = clonedAreaDictionaryList;
            _cachedJsonObject = JToken.FromObject(AreaDictionaryList);
        }

        /// <summary>
        /// 将格式字节数组转换为 JSON 对象。
        /// </summary>
        public static bool TryConvertBytesToJsonObject(byte[] formatBytes, out JToken jsonObject, out string errorMessage)
        {
            jsonObject = null;
            errorMessage = null;

            if (formatBytes == null || formatBytes.Length == 0)
            {
                errorMessage = "region 格式字节为空。";
                return false;
            }

            int validLength = formatBytes.Length;
            while (validLength > 0 && formatBytes[validLength - 1] == 0)
            {
                validLength--;
            }

            // 格式区域是固定长度保留区，尾部 0 只是填充，参与 JSON 解析反而会制造噪声。
            if (validLength <= 0)
            {
                errorMessage = "region 格式字节在移除尾部填充后为空。";
                return false;
            }

            try
            {
                string jsonText = Encoding.UTF8.GetString(formatBytes, 0, validLength);
                jsonObject = JToken.Parse(jsonText);
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = $"region 格式字节转换为 JSON 失败: {exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// 将当前格式对象转换为 JSON 对象。
        /// </summary>
        public JToken ToJsonObject()
        {
            return _cachedJsonObject.DeepClone();
        }

        /// <summary>
        /// 检查给定格式字节是否满足当前标准格式。
        /// <para>标准格式中存在的键值对必须全部匹配；文件格式允许额外键值对，但区域列表顺序必须一致。</para>
        /// </summary>
        public bool TryCheckRegionFormat(byte[] formatBytes, out string errorMessage)
        {
            if (!TryConvertBytesToJsonObject(formatBytes, out JToken candidateJsonObject, out errorMessage)) return false;

            // 这里采用“标准格式必须被候选格式完整包含”的规则，允许文件向前兼容地携带额外字段。
            if (!TryMatchToken(_cachedJsonObject, candidateJsonObject, "format", out errorMessage)) return false;

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 获取区域字典中的区域总大小。
        /// </summary>
        public int GetAreaSize(Dictionary<string, object> areaDictionary)
        {
            if (areaDictionary == null)
            {
                GD.PushError("获取区域总大小失败：areaDictionary 不能为空。");
                return 0;
            }

            if (!areaDictionary.TryGetValue("SIZE", out object sizeValue) || sizeValue is not int size)
            {
                GD.PushError("获取区域总大小失败：区域字典缺少合法的 SIZE 键。");
                return 0;
            }

            return size;
        }

        /// <summary>
        /// 获取字段属性字典。
        /// </summary>
        public Dictionary<string, object> GetFieldDictionary(Dictionary<string, object> areaDictionary, string fieldName)
        {
            if (areaDictionary == null)
            {
                GD.PushError("获取字段属性字典失败：areaDictionary 不能为空。");
                return null;
            }

            if (string.IsNullOrWhiteSpace(fieldName))
            {
                GD.PushError("获取字段属性字典失败：fieldName 不能为空。");
                return null;
            }

            if (!areaDictionary.TryGetValue(fieldName, out object fieldValue) || fieldValue is not Dictionary<string, object> fieldDictionary)
            {
                GD.PushError($"获取字段属性字典失败：区域字典缺少字段 {fieldName} 的属性字典。");
                return null;
            }

            return fieldDictionary;
        }

        /// <summary>
        /// 获取字段相对区域起始的偏移量。
        /// </summary>
        public int GetFieldOffset(Dictionary<string, object> areaDictionary, string fieldName)
        {
            return GetFieldIntAttribute(areaDictionary, fieldName, "offset");
        }

        /// <summary>
        /// 获取字段长度。
        /// </summary>
        public int GetFieldSize(Dictionary<string, object> areaDictionary, string fieldName)
        {
            return GetFieldIntAttribute(areaDictionary, fieldName, "size");
        }

        /// <summary>
        /// 获取字段属性字典中的整型属性。
        /// </summary>
        public int GetFieldIntAttribute(Dictionary<string, object> areaDictionary, string fieldName, string attributeName)
        {
            Dictionary<string, object> fieldDictionary = GetFieldDictionary(areaDictionary, fieldName);
            if (fieldDictionary == null)
            {
                GD.PushError($"获取字段 {fieldName} 的 {attributeName} 属性失败：字段属性字典为空。");
                return 0;
            }

            if (!fieldDictionary.TryGetValue(attributeName, out object attributeValue) || attributeValue is not int intValue)
            {
                GD.PushError($"获取字段 {fieldName} 的 {attributeName} 属性失败：字段缺少合法的 {attributeName} 属性。");
                return 0;
            }

            return intValue;
        }

        /// <summary>
        /// 按“标准格式包含于比较对象”的规则递归匹配 JSON 节点。
        /// </summary>
        private static bool TryMatchToken(JToken standardToken, JToken candidateToken, string tokenPath, out string errorMessage)
        {
            if (standardToken == null || candidateToken == null)
            {
                if (JToken.DeepEquals(standardToken, candidateToken))
                {
                    errorMessage = null;
                    return true;
                }

                errorMessage = $"{tokenPath} 节点为空且不匹配。";
                return false;
            }

            if (standardToken.Type != candidateToken.Type)
            {
                errorMessage = $"{tokenPath} 节点类型不匹配，标准类型为 {standardToken.Type}，实际类型为 {candidateToken.Type}。";
                return false;
            }

            if (standardToken is JObject standardObject)
            {
                JObject candidateObject = (JObject)candidateToken;
                foreach (JProperty property in standardObject.Properties())
                {
                    // 对象比较只要求候选对象覆盖标准对象已有的键，不要求二者完全相同。
                    if (!candidateObject.TryGetValue(property.Name, out JToken candidateValue))
                    {
                        errorMessage = $"{tokenPath} 缺少键 {property.Name}。";
                        return false;
                    }

                    if (!TryMatchToken(property.Value, candidateValue, $"{tokenPath}.{property.Name}", out errorMessage)) return false;
                }

                errorMessage = null;
                return true;
            }

            if (standardToken is JArray standardArray)
            {
                JArray candidateArray = (JArray)candidateToken;
                // 数组顺序直接决定布局语义，因此这里不能像对象那样容忍多余项或乱序。
                if (standardArray.Count != candidateArray.Count)
                {
                    errorMessage = $"{tokenPath} 数组长度不匹配，标准长度为 {standardArray.Count}，实际长度为 {candidateArray.Count}。";
                    return false;
                }

                for (int i = 0; i < standardArray.Count; i++)
                {
                    if (!TryMatchToken(standardArray[i], candidateArray[i], $"{tokenPath}[{i}]", out errorMessage)) return false;
                }

                errorMessage = null;
                return true;
            }

            if (!JToken.DeepEquals(standardToken, candidateToken))
            {
                errorMessage = $"{tokenPath} 节点值不匹配。";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 深拷贝区域字典，避免外部修改影响当前格式对象。
        /// </summary>
        private static Dictionary<string, object> CloneDictionary(Dictionary<string, object> sourceDictionary)
        {
            // 构造时立即深拷贝，避免外部继续修改输入字典后把标准格式对象污染掉。
            Dictionary<string, object> clonedDictionary = new(sourceDictionary.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> pair in sourceDictionary)
            {
                clonedDictionary[pair.Key] = CloneValue(pair.Value);
            }

            return clonedDictionary;
        }

        /// <summary>
        /// 深拷贝字典值。
        /// </summary>
        private static object CloneValue(object value)
        {
            if (value is Dictionary<string, object> nestedDictionary) return CloneDictionary(nestedDictionary);

            return value;
        }
    }
}