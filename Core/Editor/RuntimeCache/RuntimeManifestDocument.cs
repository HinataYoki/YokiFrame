#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace YokiFrame.RuntimeCache
{
    /// <summary>
    /// 保存 Runtime manifest 的最小 JSON 值，只覆盖校验器实际读取的对象、数组和标量。
    /// </summary>
    internal sealed class RuntimeManifestValue
    {
        /// <summary>创建指定类型的 JSON 值。</summary>
        /// <param name="kind">值类型。</param>
        private RuntimeManifestValue(RuntimeManifestValueKind kind)
        {
            Kind = kind;
        }

        /// <summary>获取值类型。</summary>
        public RuntimeManifestValueKind Kind { get; }

        /// <summary>获取对象成员；非对象为空。</summary>
        public Dictionary<string, RuntimeManifestValue> Properties { get; private set; } = default!;

        /// <summary>获取数组元素；非数组为空。</summary>
        public List<RuntimeManifestValue> Items { get; private set; } = default!;

        /// <summary>获取字符串值。</summary>
        public string Text { get; private set; } = string.Empty;

        /// <summary>获取整数值。</summary>
        public long Integer { get; private set; }

        /// <summary>创建 JSON 对象。</summary>
        /// <returns>空对象。</returns>
        public static RuntimeManifestValue Object()
        {
            return new RuntimeManifestValue(RuntimeManifestValueKind.Object)
            {
                Properties = new Dictionary<string, RuntimeManifestValue>(StringComparer.Ordinal),
            };
        }

        /// <summary>创建 JSON 数组。</summary>
        /// <returns>空数组。</returns>
        public static RuntimeManifestValue Array()
        {
            return new RuntimeManifestValue(RuntimeManifestValueKind.Array)
            {
                Items = new List<RuntimeManifestValue>(),
            };
        }

        /// <summary>创建 JSON 字符串。</summary>
        /// <param name="text">已解码文本。</param>
        /// <returns>字符串值。</returns>
        public static RuntimeManifestValue String(string text)
        {
            return new RuntimeManifestValue(RuntimeManifestValueKind.String) { Text = text ?? string.Empty };
        }

        /// <summary>创建 JSON 整数。</summary>
        /// <param name="value">整数值。</param>
        /// <returns>整数值。</returns>
        public static RuntimeManifestValue IntegerValue(long value)
        {
            return new RuntimeManifestValue(RuntimeManifestValueKind.Integer) { Integer = value };
        }

        /// <summary>创建校验器不读取、只保留结构的 JSON 值。</summary>
        /// <returns>忽略值。</returns>
        public static RuntimeManifestValue Ignored()
        {
            return new RuntimeManifestValue(RuntimeManifestValueKind.Ignored);
        }
    }

    /// <summary>Runtime manifest JSON 值的最小类型集合。</summary>
    internal enum RuntimeManifestValueKind
    {
        /// <summary>JSON 对象。</summary>
        Object,

        /// <summary>JSON 数组。</summary>
        Array,

        /// <summary>JSON 字符串。</summary>
        String,

        /// <summary>不带小数和指数的 JSON 整数。</summary>
        Integer,

        /// <summary>布尔、null、小数或指数数字；校验器不读取这些字段。</summary>
        Ignored,
    }

    /// <summary>
    /// 解析 Runtime manifest 使用的 JSON 子集。
    /// Unity 2022.3 到 6000.4 的脚本 API 没有公开 System.Text.Json，因此不能把校验器绑定到该程序集。
    /// </summary>
    internal static class RuntimeManifestDocument
    {
        private const int MAX_DEPTH = 64;

        /// <summary>
        /// 解析完整 JSON 文本，并要求根节点是对象。
        /// </summary>
        /// <param name="json">manifest 文本。</param>
        /// <returns>根对象。</returns>
        /// <exception cref="FormatException">语法、深度或根类型不符合 manifest 契约时抛出。</exception>
        public static RuntimeManifestValue ParseObject(string json)
        {
            if (json == null)
            {
                throw new FormatException("Runtime manifest JSON is missing.");
            }

            var index = 0;
            SkipWhitespace(json, ref index);
            var root = ParseValue(json, ref index, 0);
            SkipWhitespace(json, ref index);
            if (index != json.Length)
            {
                throw new FormatException("Runtime manifest JSON contains trailing characters.");
            }

            if (root.Kind != RuntimeManifestValueKind.Object)
            {
                throw new FormatException("Runtime manifest root must be an object.");
            }

            return root;
        }

        /// <summary>解析一个 JSON 值；容器进入下一层前检查深度。</summary>
        /// <param name="json">完整文本。</param>
        /// <param name="index">当前解析位置。</param>
        /// <param name="depth">当前层数，顶层为 0。</param>
        /// <returns>解析出的值。</returns>
        private static RuntimeManifestValue ParseValue(string json, ref int index, int depth)
        {
            if (depth >= MAX_DEPTH)
            {
                throw new FormatException("Runtime manifest JSON nesting is too deep.");
            }

            if (index >= json.Length)
            {
                throw new FormatException("Runtime manifest JSON value is missing.");
            }

            switch (json[index])
            {
                case '{': return ParseObject(json, ref index, depth);
                case '[': return ParseArray(json, ref index, depth);
                case '"': return RuntimeManifestValue.String(ParseString(json, ref index));
                case 't': ParseLiteral(json, ref index, "true"); return RuntimeManifestValue.Ignored();
                case 'f': ParseLiteral(json, ref index, "false"); return RuntimeManifestValue.Ignored();
                case 'n': ParseLiteral(json, ref index, "null"); return RuntimeManifestValue.Ignored();
                default:
                    if (json[index] == '-' || IsDigit(json[index]))
                    {
                        return ParseNumber(json, ref index);
                    }

                    throw new FormatException("Runtime manifest JSON value is invalid.");
            }
        }

        /// <summary>解析 JSON 对象；重复键保留后者，与原 DOM 读取语义一致。</summary>
        /// <param name="json">完整文本。</param>
        /// <param name="index">当前解析位置。</param>
        /// <param name="depth">当前对象层数。</param>
        /// <returns>对象值。</returns>
        private static RuntimeManifestValue ParseObject(string json, ref int index, int depth)
        {
            var value = RuntimeManifestValue.Object();
            index++;
            SkipWhitespace(json, ref index);
            if (Consume(json, ref index, '}'))
            {
                return value;
            }

            while (true)
            {
                if (index >= json.Length || json[index] != '"')
                {
                    throw new FormatException("Runtime manifest JSON property name is missing.");
                }

                var name = ParseString(json, ref index);
                SkipWhitespace(json, ref index);
                if (!Consume(json, ref index, ':'))
                {
                    throw new FormatException("Runtime manifest JSON property separator is missing.");
                }

                SkipWhitespace(json, ref index);
                value.Properties[name] = ParseValue(json, ref index, depth + 1);
                SkipWhitespace(json, ref index);
                if (Consume(json, ref index, '}'))
                {
                    return value;
                }

                if (!Consume(json, ref index, ','))
                {
                    throw new FormatException("Runtime manifest JSON object comma is missing.");
                }

                SkipWhitespace(json, ref index);
            }
        }

        /// <summary>解析 JSON 数组。</summary>
        /// <param name="json">完整文本。</param>
        /// <param name="index">当前解析位置。</param>
        /// <param name="depth">当前数组层数。</param>
        /// <returns>数组值。</returns>
        private static RuntimeManifestValue ParseArray(string json, ref int index, int depth)
        {
            var value = RuntimeManifestValue.Array();
            index++;
            SkipWhitespace(json, ref index);
            if (Consume(json, ref index, ']'))
            {
                return value;
            }

            while (true)
            {
                value.Items.Add(ParseValue(json, ref index, depth + 1));
                SkipWhitespace(json, ref index);
                if (Consume(json, ref index, ']'))
                {
                    return value;
                }

                if (!Consume(json, ref index, ','))
                {
                    throw new FormatException("Runtime manifest JSON array comma is missing.");
                }

                SkipWhitespace(json, ref index);
            }
        }

        /// <summary>解析并解码 JSON 字符串，拒绝控制字符和未配对代理。</summary>
        /// <param name="json">完整文本。</param>
        /// <param name="index">当前解析位置。</param>
        /// <returns>解码后的文本。</returns>
        private static string ParseString(string json, ref int index)
        {
            index++;
            var builder = new StringBuilder();
            while (index < json.Length)
            {
                var current = json[index++];
                if (current == '"')
                {
                    return builder.ToString();
                }

                if (current < ' ')
                {
                    throw new FormatException("Runtime manifest JSON string contains a control character.");
                }

                if (current == '\\')
                {
                    AppendEscape(json, ref index, builder);
                    continue;
                }

                AppendRawCharacter(json, ref index, current, builder);
            }

            throw new FormatException("Runtime manifest JSON string is unterminated.");
        }

        /// <summary>解析 JSON 数字；整数保留数值，小数和指数只保留语法。</summary>
        /// <param name="json">完整文本。</param>
        /// <param name="index">当前解析位置。</param>
        /// <returns>整数或忽略值。</returns>
        private static RuntimeManifestValue ParseNumber(string json, ref int index)
        {
            var start = index;
            var isInteger = true;
            Consume(json, ref index, '-');
            if (Consume(json, ref index, '0'))
            {
                if (index < json.Length && IsDigit(json[index]))
                {
                    throw new FormatException("Runtime manifest JSON number contains a leading zero.");
                }
            }
            else
            {
                RequireDigit(json, ref index);
                while (index < json.Length && IsDigit(json[index]))
                {
                    index++;
                }
            }

            if (Consume(json, ref index, '.'))
            {
                isInteger = false;
                RequireDigit(json, ref index);
                while (index < json.Length && IsDigit(json[index]))
                {
                    index++;
                }
            }

            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                isInteger = false;
                index++;
                if (index < json.Length && (json[index] == '+' || json[index] == '-'))
                {
                    index++;
                }

                RequireDigit(json, ref index);
                while (index < json.Length && IsDigit(json[index]))
                {
                    index++;
                }
            }

            if (!isInteger)
            {
                return RuntimeManifestValue.Ignored();
            }

            var token = json.Substring(start, index - start);
            if (!long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            {
                throw new FormatException("Runtime manifest JSON integer is outside the supported range.");
            }

            return RuntimeManifestValue.IntegerValue(integer);
        }

        /// <summary>追加原始字符，并拒绝未配对的 UTF-16 代理。</summary>
        /// <param name="json">完整文本。</param>
        /// <param name="index">当前解析位置。</param>
        /// <param name="current">已消费的原始字符。</param>
        /// <param name="builder">解码缓冲区。</param>
        private static void AppendRawCharacter(string json, ref int index, char current, StringBuilder builder)
        {
            if (char.IsLowSurrogate(current)
                || (char.IsHighSurrogate(current)
                    && (index >= json.Length || !char.IsLowSurrogate(json[index]))))
            {
                throw new FormatException("Runtime manifest JSON string contains an unpaired surrogate.");
            }

            builder.Append(current);
            if (char.IsHighSurrogate(current))
            {
                builder.Append(json[index++]);
            }
        }

        /// <summary>解码一个标准 JSON 转义。</summary>
        /// <param name="json">完整文本。</param>
        /// <param name="index">转义字符位置。</param>
        /// <param name="builder">解码缓冲区。</param>
        private static void AppendEscape(string json, ref int index, StringBuilder builder)
        {
            if (index >= json.Length)
            {
                throw new FormatException("Runtime manifest JSON string escape is incomplete.");
            }

            var escaped = json[index++];
            switch (escaped)
            {
                case '"': builder.Append('"'); return;
                case '\\': builder.Append('\\'); return;
                case '/': builder.Append('/'); return;
                case 'b': builder.Append('\b'); return;
                case 'f': builder.Append('\f'); return;
                case 'n': builder.Append('\n'); return;
                case 'r': builder.Append('\r'); return;
                case 't': builder.Append('\t'); return;
                case 'u': AppendUnicode(json, ref index, builder); return;
                default: throw new FormatException("Runtime manifest JSON string escape is invalid.");
            }
        }

        /// <summary>解码 unicode escape；高代理必须紧跟低代理 escape。</summary>
        /// <param name="json">完整文本。</param>
        /// <param name="index">四个十六进制数字的位置。</param>
        /// <param name="builder">解码缓冲区。</param>
        private static void AppendUnicode(string json, ref int index, StringBuilder builder)
        {
            var value = ReadHexQuad(json, ref index);
            var current = (char)value;
            if (char.IsLowSurrogate(current))
            {
                throw new FormatException("Runtime manifest JSON unicode escape contains an unpaired surrogate.");
            }

            if (!char.IsHighSurrogate(current))
            {
                builder.Append(current);
                return;
            }

            if (index + 6 > json.Length || json[index] != '\\' || json[index + 1] != 'u')
            {
                throw new FormatException("Runtime manifest JSON unicode escape contains an unpaired surrogate.");
            }

            index += 2;
            var low = (char)ReadHexQuad(json, ref index);
            if (!char.IsLowSurrogate(low))
            {
                throw new FormatException("Runtime manifest JSON unicode escape contains an unpaired surrogate.");
            }

            builder.Append(current);
            builder.Append(low);
        }

        /// <summary>读取四个十六进制数字。</summary>
        /// <param name="json">完整文本。</param>
        /// <param name="index">四个十六进制数字的位置。</param>
        /// <returns>解析出的码元。</returns>
        private static int ReadHexQuad(string json, ref int index)
        {
            if (index + 4 > json.Length)
            {
                throw new FormatException("Runtime manifest JSON unicode escape is incomplete.");
            }

            var value = 0;
            for (var offset = 0; offset < 4; offset++)
            {
                var digit = ParseHex(json[index++]);
                if (digit < 0)
                {
                    throw new FormatException("Runtime manifest JSON unicode escape is invalid.");
                }

                value = (value << 4) | digit;
            }

            return value;
        }

        /// <summary>解析固定 JSON 字面量。</summary>
        /// <param name="json">完整文本。</param>
        /// <param name="index">字面量起始位置。</param>
        /// <param name="literal">期望字面量。</param>
        private static void ParseLiteral(string json, ref int index, string literal)
        {
            if (index + literal.Length > json.Length
                || string.CompareOrdinal(json, index, literal, 0, literal.Length) != 0)
            {
                throw new FormatException("Runtime manifest JSON literal is invalid.");
            }

            index += literal.Length;
        }

        /// <summary>要求当前位置是数字并消费它。</summary>
        /// <param name="json">完整文本。</param>
        /// <param name="index">当前解析位置。</param>
        private static void RequireDigit(string json, ref int index)
        {
            if (index >= json.Length || !IsDigit(json[index]))
            {
                throw new FormatException("Runtime manifest JSON number is invalid.");
            }

            index++;
        }

        /// <summary>当前位置匹配时消费指定字符。</summary>
        /// <param name="json">完整文本。</param>
        /// <param name="index">当前解析位置。</param>
        /// <param name="expected">期望字符。</param>
        /// <returns>成功消费时返回 true。</returns>
        private static bool Consume(string json, ref int index, char expected)
        {
            if (index >= json.Length || json[index] != expected)
            {
                return false;
            }

            index++;
            return true;
        }

        /// <summary>跳过 JSON 允许的四种空白。</summary>
        /// <param name="json">完整文本。</param>
        /// <param name="index">当前解析位置。</param>
        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length)
            {
                var current = json[index];
                if (current != ' ' && current != '\t' && current != '\r' && current != '\n')
                {
                    return;
                }

                index++;
            }
        }

        /// <summary>判断字符是否为十进制数字。</summary>
        /// <param name="value">待判断字符。</param>
        /// <returns>是数字时返回 true。</returns>
        private static bool IsDigit(char value)
        {
            return value >= '0' && value <= '9';
        }

        /// <summary>把十六进制字符转换为数字。</summary>
        /// <param name="value">待转换字符。</param>
        /// <returns>0 到 15；非法字符返回 -1。</returns>
        private static int ParseHex(char value)
        {
            if (value >= '0' && value <= '9') return value - '0';
            if (value >= 'a' && value <= 'f') return value - 'a' + 10;
            return value >= 'A' && value <= 'F' ? value - 'A' + 10 : -1;
        }
    }
}
#endif
