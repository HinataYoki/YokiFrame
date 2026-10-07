#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YokiFrame.Json
{
    /// <summary>
    /// JSON 值类型；取值与 System.Text.Json 保持一致，便于调用点无差别迁移。
    /// </summary>
    public enum JsonValueKind
    {
        /// <summary>未初始化或缺失的值。</summary>
        Undefined = 0,

        /// <summary>对象。</summary>
        Object,

        /// <summary>数组。</summary>
        Array,

        /// <summary>字符串。</summary>
        String,

        /// <summary>数字。</summary>
        Number,

        /// <summary>true。</summary>
        True,

        /// <summary>false。</summary>
        False,

        /// <summary>null。</summary>
        Null
    }

    /// <summary>
    /// JSON 解析选项；<see cref="MaxDepth"/> 为 0 时使用与 System.Text.Json 一致的默认上限 64。
    /// </summary>
    public struct JsonDocumentOptions
    {
        /// <summary>获取或设置最大嵌套深度。</summary>
        public int MaxDepth { get; set; }
    }

    /// <summary>
    /// JSON 文本无效时抛出的异常；调用方按可恢复的校验失败处理。
    /// </summary>
    public sealed class JsonException : Exception
    {
        /// <summary>创建 JSON 异常。</summary>
        /// <param name="message">失败说明。</param>
        public JsonException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// 只读 JSON 文档。
    /// </summary>
    /// <remarks>
    /// 通用只读 JSON DOM，用于替代 System.Text.Json：Unity 2022.3 不自带该程序集，
    /// 而工程中的其它包可能把 System.Text.Json 以 internal 形式合并进自己的程序集
    /// （例如 Code Coverage 内嵌的 ReportGenerator），使 <c>using System.Text.Json;</c>
    /// 解析到不可访问的类型并报 CS0122。本实现不依赖任何外部程序集，Unity 与工具构建共用同一路径。
    /// </remarks>
    public sealed class JsonDocument : IDisposable
    {
        private const int DEFAULT_MAX_DEPTH = 64;

        private JsonDocument(string text, JsonElement root)
        {
            Text = text;
            RootElement = root;
        }

        /// <summary>获取根元素。</summary>
        public JsonElement RootElement { get; }

        /// <summary>获取原始 JSON 文本。</summary>
        internal string Text { get; }

        /// <summary>
        /// 解析 JSON 文本。
        /// </summary>
        /// <param name="json">JSON 文本。</param>
        /// <param name="options">解析选项。</param>
        /// <returns>只读文档。</returns>
        public static JsonDocument Parse(string json, JsonDocumentOptions options = default)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            JsonNode root = new JsonReader(json, ResolveMaxDepth(options)).ReadDocument();
            var document = new JsonDocument(json, default);
            return new JsonDocument(json, new JsonElement(document, root));
        }

        /// <summary>
        /// 按 UTF-8 读取流并解析 JSON。
        /// </summary>
        /// <param name="stream">JSON 数据流。</param>
        /// <param name="options">解析选项。</param>
        /// <returns>只读文档。</returns>
        public static JsonDocument Parse(Stream stream, JsonDocumentOptions options = default)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
            return Parse(reader.ReadToEnd(), options);
        }

        /// <summary>释放文档；当前实现不持有非托管资源。</summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// 解析最大深度，0 视为默认上限。
        /// </summary>
        /// <param name="options">解析选项。</param>
        /// <returns>生效的最大深度。</returns>
        private static int ResolveMaxDepth(JsonDocumentOptions options)
        {
            return options.MaxDepth > 0 ? options.MaxDepth : DEFAULT_MAX_DEPTH;
        }
    }

    /// <summary>
    /// 只读 JSON 元素；对象、数组与标量共用同一类型。
    /// </summary>
    public readonly struct JsonElement
    {
        private readonly JsonDocument mDocument;
        private readonly JsonNode mNode;

        /// <summary>创建 JSON 元素。</summary>
        /// <param name="document">所属文档。</param>
        /// <param name="node">底层节点。</param>
        internal JsonElement(JsonDocument document, JsonNode node)
        {
            mDocument = document;
            mNode = node;
        }

        /// <summary>获取值类型。</summary>
        public JsonValueKind ValueKind
        {
            get { return mNode == null ? JsonValueKind.Undefined : mNode.Kind; }
        }

        /// <summary>
        /// 按名称读取对象属性；重复属性在解析阶段已被拒绝，因此结果唯一。
        /// </summary>
        /// <param name="propertyName">属性名，区分大小写。</param>
        /// <param name="value">属性值。</param>
        /// <returns>属性存在时返回 true。</returns>
        public bool TryGetProperty(string propertyName, out JsonElement value)
        {
            value = default;
            if (mNode == null || mNode.Kind != JsonValueKind.Object || string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            List<JsonMember> members = mNode.Members;
            for (var index = 0; index < members.Count; index++)
            {
                if (string.Equals(members[index].Name, propertyName, StringComparison.Ordinal))
                {
                    value = new JsonElement(mDocument, members[index].Value);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 读取必需属性；属性缺失时抛出异常，便于契约测试快速失败。
        /// </summary>
        /// <param name="propertyName">属性名。</param>
        /// <returns>属性值。</returns>
        public JsonElement GetProperty(string propertyName)
        {
            if (!TryGetProperty(propertyName, out JsonElement value))
            {
                throw new InvalidOperationException("The JSON object does not contain property '" + propertyName + "'.");
            }

            return value;
        }

        /// <summary>Returns the number of properties; requires a JSON object.</summary>
        public int GetPropertyCount()
        {
            if (mNode == null || mNode.Kind != JsonValueKind.Object)
                throw new InvalidOperationException("The JSON value is not an object.");
            return mNode.Members.Count;
        }

        /// <summary>
        /// 读取数组长度；非数组抛出异常。
        /// </summary>
        /// <returns>元素个数。</returns>
        public int GetArrayLength()
        {
            if (mNode == null || mNode.Kind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("The JSON value is not an array.");
            }

            return mNode.Items.Count;
        }

        /// <summary>
        /// 按索引读取数组元素。
        /// </summary>
        /// <param name="index">元素下标。</param>
        /// <returns>元素。</returns>
        public JsonElement this[int index]
        {
            get
            {
                if (mNode == null || mNode.Kind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException("The JSON value is not an array.");
                }

                if (index < 0 || index >= mNode.Items.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return new JsonElement(mDocument, mNode.Items[index]);
            }
        }

        /// <summary>
        /// 读取布尔值；非布尔抛出异常。
        /// </summary>
        /// <returns>布尔值。</returns>
        public bool GetBoolean()
        {
            if (mNode != null && mNode.Kind == JsonValueKind.True)
            {
                return true;
            }

            if (mNode != null && mNode.Kind == JsonValueKind.False)
            {
                return false;
            }

            throw new InvalidOperationException("The JSON value is not a boolean.");
        }

        /// <summary>
        /// 枚举数组元素；非数组返回空集合。
        /// </summary>
        /// <returns>数组元素集合。</returns>
        public IReadOnlyList<JsonElement> EnumerateArray()
        {
            if (mNode == null || mNode.Kind != JsonValueKind.Array)
            {
                return Array.Empty<JsonElement>();
            }

            List<JsonNode> items = mNode.Items;
            var elements = new JsonElement[items.Count];
            for (var index = 0; index < items.Count; index++)
            {
                elements[index] = new JsonElement(mDocument, items[index]);
            }

            return elements;
        }

        /// <summary>
        /// 读取字符串值。
        /// </summary>
        /// <returns>字符串值；值为 null 时返回 null。</returns>
        public string GetString()
        {
            if (mNode == null || mNode.Kind == JsonValueKind.Null)
            {
                return null;
            }

            if (mNode.Kind != JsonValueKind.String)
            {
                throw new InvalidOperationException("The JSON value is not a string.");
            }

            return mNode.Text;
        }

        /// <summary>
        /// 读取 32 位整数。
        /// </summary>
        /// <param name="value">解析结果。</param>
        /// <returns>值为可表示整数时返回 true。</returns>
        public bool TryGetInt32(out int value)
        {
            value = 0;
            return TryReadIntegerText(out string text)
                && int.TryParse(text, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// 读取 64 位整数。
        /// </summary>
        /// <param name="value">解析结果。</param>
        /// <returns>值为可表示整数时返回 true。</returns>
        public bool TryGetInt64(out long value)
        {
            value = 0L;
            return TryReadIntegerText(out string text)
                && long.TryParse(text, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// 返回原始 JSON 片段；字符串返回其解码后的值。
        /// </summary>
        /// <returns>文本表示。</returns>
        public override string ToString()
        {
            if (mNode == null)
            {
                return string.Empty;
            }

            if (mNode.Kind == JsonValueKind.String)
            {
                return mNode.Text;
            }

            return mDocument != null && mNode.Start >= 0 && mNode.End <= mDocument.Text.Length
                ? mDocument.Text.Substring(mNode.Start, mNode.End - mNode.Start)
                : mNode.Text;
        }

        /// <summary>
        /// 判断当前值是否为可解析的整数形式。
        /// </summary>
        /// <param name="text">数字原始文本。</param>
        /// <returns>是整数形式时返回 true。</returns>
        private bool TryReadIntegerText(out string text)
        {
            text = null;
            if (mNode == null || mNode.Kind != JsonValueKind.Number)
            {
                return false;
            }

            string raw = mNode.Text;
            for (var index = 0; index < raw.Length; index++)
            {
                char c = raw[index];
                if (c != '-' && c != '+' && (c < '0' || c > '9'))
                {
                    return false;
                }
            }

            text = raw;
            return raw.Length > 0;
        }
    }

    /// <summary>
    /// DOM 节点；对象成员按出现顺序保存，重复属性在解析阶段被拒绝。
    /// </summary>
    internal sealed class JsonNode
    {
        internal JsonValueKind Kind;
        internal string Text;
        internal int Start = -1;
        internal int End = -1;
        internal List<JsonMember> Members;
        internal List<JsonNode> Items;
    }

    /// <summary>
    /// 对象成员。
    /// </summary>
    internal struct JsonMember
    {
        internal string Name;
        internal JsonNode Value;
    }
}
#endif
