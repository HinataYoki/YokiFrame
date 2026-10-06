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

    /// <summary>
    /// 最小递归下降 JSON 解析器；任何语法问题都抛出 <see cref="JsonException"/>。
    /// </summary>
    internal sealed class JsonReader
    {
        private readonly string mText;
        private readonly int mMaxDepth;
        private int mIndex;

        internal JsonReader(string text, int maxDepth)
        {
            mText = text;
            mMaxDepth = maxDepth;
        }

        /// <summary>
        /// 读取完整文档并要求没有多余内容。
        /// </summary>
        /// <returns>根节点。</returns>
        internal JsonNode ReadDocument()
        {
            SkipWhitespace();
            if (mIndex >= mText.Length)
            {
                throw new JsonException("JSON document is empty.");
            }

            JsonNode root = ReadValue(0);
            SkipWhitespace();
            if (mIndex != mText.Length)
            {
                throw new JsonException("JSON document contains trailing content at position " + mIndex + ".");
            }

            return root;
        }

        /// <summary>
        /// 读取任意值。
        /// </summary>
        /// <param name="depth">当前嵌套深度。</param>
        /// <returns>节点。</returns>
        private JsonNode ReadValue(int depth)
        {
            if (depth > mMaxDepth)
            {
                throw new JsonException("JSON document exceeds the maximum depth of " + mMaxDepth + ".");
            }

            SkipWhitespace();
            if (mIndex >= mText.Length)
            {
                throw new JsonException("JSON document ended unexpectedly.");
            }

            switch (mText[mIndex])
            {
                case '{':
                    return ReadObject(depth);
                case '[':
                    return ReadArray(depth);
                case '"':
                    return ReadStringNode();
                case 't':
                    return ReadLiteral("true", JsonValueKind.True);
                case 'f':
                    return ReadLiteral("false", JsonValueKind.False);
                case 'n':
                    return ReadLiteral("null", JsonValueKind.Null);
                default:
                    return ReadNumberNode();
            }
        }

        /// <summary>
        /// 读取对象。
        /// </summary>
        /// <param name="depth">当前嵌套深度。</param>
        /// <returns>对象节点。</returns>
        private JsonNode ReadObject(int depth)
        {
            int start = mIndex;
            mIndex++;
            var node = new JsonNode { Kind = JsonValueKind.Object, Members = new List<JsonMember>() };
            SkipWhitespace();
            if (mIndex < mText.Length && mText[mIndex] == '}')
            {
                mIndex++;
                node.Start = start;
                node.End = mIndex;
                return node;
            }

            while (true)
            {
                SkipWhitespace();
                if (mIndex >= mText.Length || mText[mIndex] != '"')
                {
                    throw new JsonException("JSON object property name is missing at position " + mIndex + ".");
                }

                string name = ReadString();
                SkipWhitespace();
                if (mIndex >= mText.Length || mText[mIndex] != ':')
                {
                    throw new JsonException("JSON object property '" + name + "' is missing a value at position " + mIndex + ".");
                }

                mIndex++;
                JsonNode value = ReadValue(depth + 1);
                for (var index = 0; index < node.Members.Count; index++)
                {
                    if (string.Equals(node.Members[index].Name, name, StringComparison.Ordinal))
                    {
                        throw new JsonException("JSON object contains duplicate property '" + name + "'.");
                    }
                }

                node.Members.Add(new JsonMember { Name = name, Value = value });
                SkipWhitespace();
                if (mIndex >= mText.Length)
                {
                    throw new JsonException("JSON object is not terminated.");
                }

                if (mText[mIndex] == ',')
                {
                    mIndex++;
                    continue;
                }

                if (mText[mIndex] == '}')
                {
                    mIndex++;
                    node.Start = start;
                    node.End = mIndex;
                    return node;
                }

                throw new JsonException("JSON object contains an unexpected character at position " + mIndex + ".");
            }
        }

        /// <summary>
        /// 读取数组。
        /// </summary>
        /// <param name="depth">当前嵌套深度。</param>
        /// <returns>数组节点。</returns>
        private JsonNode ReadArray(int depth)
        {
            int start = mIndex;
            mIndex++;
            var node = new JsonNode { Kind = JsonValueKind.Array, Items = new List<JsonNode>() };
            SkipWhitespace();
            if (mIndex < mText.Length && mText[mIndex] == ']')
            {
                mIndex++;
                node.Start = start;
                node.End = mIndex;
                return node;
            }

            while (true)
            {
                node.Items.Add(ReadValue(depth + 1));
                SkipWhitespace();
                if (mIndex >= mText.Length)
                {
                    throw new JsonException("JSON array is not terminated.");
                }

                if (mText[mIndex] == ',')
                {
                    mIndex++;
                    continue;
                }

                if (mText[mIndex] == ']')
                {
                    mIndex++;
                    node.Start = start;
                    node.End = mIndex;
                    return node;
                }

                throw new JsonException("JSON array contains an unexpected character at position " + mIndex + ".");
            }
        }

        /// <summary>
        /// 读取字符串节点。
        /// </summary>
        /// <returns>字符串节点。</returns>
        private JsonNode ReadStringNode()
        {
            int start = mIndex;
            string value = ReadString();
            return new JsonNode { Kind = JsonValueKind.String, Text = value, Start = start, End = mIndex };
        }

        /// <summary>
        /// 读取并解码字符串字面量。
        /// </summary>
        /// <returns>解码后的字符串。</returns>
        private string ReadString()
        {
            mIndex++;
            var builder = new StringBuilder();
            while (mIndex < mText.Length)
            {
                char c = mText[mIndex];
                if (c == '"')
                {
                    mIndex++;
                    return builder.ToString();
                }

                if (c != '\\')
                {
                    if (c < ' ')
                    {
                        throw new JsonException("JSON string contains an unescaped control character at position " + mIndex + ".");
                    }

                    builder.Append(c);
                    mIndex++;
                    continue;
                }

                mIndex++;
                if (mIndex >= mText.Length)
                {
                    throw new JsonException("JSON string escape is incomplete.");
                }

                char escape = mText[mIndex];
                mIndex++;
                switch (escape)
                {
                    case '"':
                        builder.Append('"');
                        break;
                    case '\\':
                        builder.Append('\\');
                        break;
                    case '/':
                        builder.Append('/');
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        builder.Append(ReadUnicodeEscape());
                        break;
                    default:
                        throw new JsonException("JSON string contains an invalid escape at position " + (mIndex - 1) + ".");
                }
            }

            throw new JsonException("JSON string is not terminated.");
        }

        /// <summary>
        /// 读取 unicode 转义并处理代理对。
        /// </summary>
        /// <returns>解码后的字符串。</returns>
        private string ReadUnicodeEscape()
        {
            int code = ReadHex4();
            if (code >= 0xD800 && code <= 0xDBFF
                && mIndex + 1 < mText.Length
                && mText[mIndex] == '\\'
                && mText[mIndex + 1] == 'u')
            {
                int saved = mIndex;
                mIndex += 2;
                int low = ReadHex4();
                if (low >= 0xDC00 && low <= 0xDFFF)
                {
                    return char.ConvertFromUtf32(0x10000 + ((code - 0xD800) << 10) + (low - 0xDC00));
                }

                mIndex = saved;
            }

            return ((char)code).ToString();
        }

        /// <summary>
        /// 读取 4 位十六进制。
        /// </summary>
        /// <returns>码位。</returns>
        private int ReadHex4()
        {
            if (mIndex + 4 > mText.Length)
            {
                throw new JsonException("JSON unicode escape is incomplete.");
            }

            int value = 0;
            for (var index = 0; index < 4; index++)
            {
                char c = mText[mIndex + index];
                int digit;
                if (c >= '0' && c <= '9')
                {
                    digit = c - '0';
                }
                else if (c >= 'a' && c <= 'f')
                {
                    digit = c - 'a' + 10;
                }
                else if (c >= 'A' && c <= 'F')
                {
                    digit = c - 'A' + 10;
                }
                else
                {
                    throw new JsonException("JSON unicode escape contains a non-hex character at position " + (mIndex + index) + ".");
                }

                value = (value << 4) + digit;
            }

            mIndex += 4;
            return value;
        }

        /// <summary>
        /// 读取 true/false/null。
        /// </summary>
        /// <param name="literal">期望字面量。</param>
        /// <param name="kind">对应值类型。</param>
        /// <returns>字面量节点。</returns>
        private JsonNode ReadLiteral(string literal, JsonValueKind kind)
        {
            int start = mIndex;
            if (mIndex + literal.Length > mText.Length
                || string.CompareOrdinal(mText, mIndex, literal, 0, literal.Length) != 0)
            {
                throw new JsonException("JSON literal is invalid at position " + mIndex + ".");
            }

            mIndex += literal.Length;
            return new JsonNode { Kind = kind, Text = literal, Start = start, End = mIndex };
        }

        /// <summary>
        /// 读取数字。
        /// </summary>
        /// <returns>数字节点。</returns>
        private JsonNode ReadNumberNode()
        {
            int start = mIndex;
            if (mIndex < mText.Length && mText[mIndex] == '-')
            {
                mIndex++;
            }

            int integerStart = mIndex;
            if (mIndex < mText.Length && mText[mIndex] == '0')
            {
                mIndex++;
            }
            else
            {
                while (mIndex < mText.Length && mText[mIndex] >= '0' && mText[mIndex] <= '9')
                {
                    mIndex++;
                }
            }

            if (mIndex == integerStart)
            {
                throw new JsonException("JSON number is invalid at position " + start + ".");
            }

            if (mIndex < mText.Length && mText[mIndex] == '.')
            {
                mIndex++;
                int fractionStart = mIndex;
                while (mIndex < mText.Length && mText[mIndex] >= '0' && mText[mIndex] <= '9')
                {
                    mIndex++;
                }

                if (mIndex == fractionStart)
                {
                    throw new JsonException("JSON number fraction is invalid at position " + start + ".");
                }
            }

            if (mIndex < mText.Length && (mText[mIndex] == 'e' || mText[mIndex] == 'E'))
            {
                mIndex++;
                if (mIndex < mText.Length && (mText[mIndex] == '+' || mText[mIndex] == '-'))
                {
                    mIndex++;
                }

                int exponentStart = mIndex;
                while (mIndex < mText.Length && mText[mIndex] >= '0' && mText[mIndex] <= '9')
                {
                    mIndex++;
                }

                if (mIndex == exponentStart)
                {
                    throw new JsonException("JSON number exponent is invalid at position " + start + ".");
                }
            }

            if (mIndex < mText.Length && !IsValueTerminator(mText[mIndex]))
            {
                throw new JsonException("JSON number is followed by an invalid character at position " + mIndex + ".");
            }

            return new JsonNode
            {
                Kind = JsonValueKind.Number,
                Text = mText.Substring(start, mIndex - start),
                Start = start,
                End = mIndex
            };
        }

        /// <summary>
        /// 判断字符能否结束一个值。
        /// </summary>
        /// <param name="value">待判断字符。</param>
        /// <returns>是分隔符时返回 true。</returns>
        private static bool IsValueTerminator(char value)
        {
            return value == ' ' || value == '\t' || value == '\r' || value == '\n'
                || value == ',' || value == '}' || value == ']';
        }

        /// <summary>
        /// 跳过空白字符。
        /// </summary>
        private void SkipWhitespace()
        {
            while (mIndex < mText.Length)
            {
                char c = mText[mIndex];
                if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
                {
                    return;
                }

                mIndex++;
            }
        }
    }
}
#endif