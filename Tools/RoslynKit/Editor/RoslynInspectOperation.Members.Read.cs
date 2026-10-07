#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using YokiFrame.Json;

namespace YokiFrame
{
    public sealed partial class RoslynInspectOperation
    {
        /// <summary>解析路径中的一段方括号索引，并前进到右括号之后。</summary>
        /// <param name="path">完整路径。</param>
        /// <param name="limit">传给索引读取的上限参数。</param>
        /// <param name="index">当前字符位置；成功时移到右括号后。</param>
        /// <param name="value">当前值；成功时变为索引结果。</param>
        /// <param name="error">失败原因。</param>
        /// <param name="resolvedPath">已解析路径；成功时追加索引文本。</param>
        /// <param name="currentType">当前类型；成功时改为结果类型。</param>
        /// <returns>索引段合法且取值成功时返回 true。失败时不改路径和位置。</returns>
        private static bool TryWalkIndex(
            string path,
            int limit,
            ref int index,
            ref object value,
            ref string error,
            ref string resolvedPath,
            ref Type currentType)
        {
            int close = path.IndexOf(']', index);
            if (close < 0)
            {
                error = "path is missing a closing ]: " + path;
                return false;
            }

            string key = path.Substring(index + 1, close - index - 1).Trim();
            if (!TryIndex(value, key, limit, out value, out error))
            {
                return false;
            }

            resolvedPath += "[" + key + "]";
            currentType = value == null ? null : value.GetType();
            index = close + 1;
            return true;
        }

        /// <summary>解析路径中的一段成员名。空白段只前进位置，不读取成员。</summary>
        /// <param name="path">完整路径。</param>
        /// <param name="index">当前字符位置；处理完后停在段末。</param>
        /// <param name="value">当前值；成功读取后变为成员值。</param>
        /// <param name="currentType">当前类型；成功读取后改为成员值类型。</param>
        /// <param name="error">失败原因。</param>
        /// <param name="resolvedPath">已解析路径；成功读取后追加成员名。</param>
        /// <returns>空白段或成员读取成功时返回 true。</returns>
        private static bool TryWalkMember(
            string path,
            ref int index,
            ref object value,
            ref Type currentType,
            ref string error,
            ref string resolvedPath)
        {
            int end = index;
            while (end < path.Length && path[end] != '.' && path[end] != '[')
            {
                end++;
            }

            string member = path.Substring(index, end - index).Trim();
            if (member.Length == 0)
            {
                index = end;
                return true;
            }

            if (!TryReadMember(value, currentType, member, out value, out error))
            {
                return false;
            }

            resolvedPath += (resolvedPath.Length == 0 ? string.Empty : ".") + member;
            currentType = value == null ? null : value.GetType();
            index = end;
            return true;
        }

        /// <summary>按不变文化的键文本查找字典项。</summary>
        /// <param name="dictionary">字典。</param>
        /// <param name="key">要匹配的键文本。</param>
        /// <param name="value">命中的值。</param>
        /// <param name="error">未命中时的原因。</param>
        /// <returns>找到相同键时返回 true。</returns>
        private static bool TryIndexDictionary(IDictionary dictionary, string key, out object value, out string error)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (string.Equals(Convert.ToString(entry.Key, CultureInfo.InvariantCulture), key, StringComparison.Ordinal))
                {
                    value = entry.Value;
                    error = string.Empty;
                    return true;
                }
            }

            value = null;
            error = "dictionary key was not found: " + key + ".";
            return false;
        }

        /// <summary>把键解析为不变文化整数后读取列表元素。</summary>
        /// <param name="list">列表。</param>
        /// <param name="key">下标文本。</param>
        /// <param name="value">命中的元素。</param>
        /// <param name="error">无法解析或越界时的原因。</param>
        /// <returns>下标落在范围内时返回 true。</returns>
        private static bool TryIndexList(IList list, string key, out object value, out string error)
        {
            value = null;
            error = string.Empty;
            if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int position)
                || position < 0
                || position >= list.Count)
            {
                error = "list index is out of range: " + key + " (count " + list.Count + ").";
                return false;
            }

            value = list[position];
            return true;
        }

        /// <summary>读取名为 Item 的字符串索引器。没有该索引器时报告类型不支持索引。</summary>
        /// <param name="container">非 null 的容器。</param>
        /// <param name="key">索引器参数。</param>
        /// <param name="value">索引结果。</param>
        /// <param name="error">索引器返回 null 或不存在时的原因。</param>
        /// <returns>索引器存在且返回非 null 时返回 true。</returns>
        private static bool TryIndexStringIndexer(object container, string key, out object value, out string error)
        {
            // 支持带字符串索引器的类型（例如自定义容器）。
            PropertyInfo indexer = container.GetType().GetProperty("Item", new[] { typeof(string) });
            if (indexer != null)
            {
                value = indexer.GetValue(container, new object[] { key });
                if (value == null)
                {
                    error = "indexer returned null for key: " + key + ".";
                    return false;
                }

                error = string.Empty;
                return true;
            }

            value = null;
            error = "value of type " + container.GetType().FullName + " does not support [" + key + "].";
            return false;
        }

        /// <summary>写入与实例静态性匹配、且非 initonly 的字段，直到达到上限。</summary>
        /// <param name="builder">正在写入的 JSON。</param>
        /// <param name="instance">实例；静态摘要时为 null。</param>
        /// <param name="type">声明这些字段的类型。</param>
        /// <param name="depth">剩余展开深度。</param>
        /// <param name="limit">字段与属性共用的写入上限。</param>
        /// <param name="flags">反射查找标记。</param>
        /// <returns>已经写入的成员数。</returns>
        private static int WriteFields(
            RoslynJsonBuilder builder,
            object instance,
            Type type,
            int depth,
            int limit,
            BindingFlags flags)
        {
            var written = 0;
            FieldInfo[] fields = type.GetFields(flags);
            for (var index = 0; index < fields.Length && written < limit; index++)
            {
                FieldInfo field = fields[index];
                if (field.IsStatic != (instance == null) || field.IsInitOnly)
                {
                    continue;
                }

                object value = field.GetValue(field.IsStatic ? null : instance);
                WriteEntry(builder, field.Name, value, depth, limit);
                written++;
            }

            return written;
        }

        /// <summary>接在字段之后写入可读、无索引参数且静态性匹配的属性。</summary>
        /// <param name="builder">正在写入的 JSON。</param>
        /// <param name="instance">实例；静态摘要时为 null。</param>
        /// <param name="type">声明这些属性的类型。</param>
        /// <param name="depth">剩余展开深度。</param>
        /// <param name="limit">字段与属性共用的写入上限。</param>
        /// <param name="flags">反射查找标记。</param>
        /// <param name="written">字段已经占用的数量。</param>
        private static void WriteProperties(
            RoslynJsonBuilder builder,
            object instance,
            Type type,
            int depth,
            int limit,
            BindingFlags flags,
            int written)
        {
            PropertyInfo[] properties = type.GetProperties(flags);
            for (var index = 0; index < properties.Length && written < limit; index++)
            {
                PropertyInfo property = properties[index];
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                MethodInfo getter = property.GetGetMethod(nonPublic: true);
                if (getter == null || getter.IsStatic != (instance == null))
                {
                    continue;
                }

                object value;
                try
                {
                    value = property.GetValue(instance);
                }
                catch (TargetInvocationException exception)
                {
                    value = "<threw " + (exception.InnerException ?? exception).GetType().Name + ">";
                }

                WriteEntry(builder, property.Name, value, depth, limit);
                written++;
            }
        }

        /// <summary>把序列写成 JSON 数组。达到 limit 时追加截断标记并停止。</summary>
        /// <param name="builder">正在写入的 JSON。</param>
        /// <param name="sequence">可枚举序列。</param>
        /// <param name="limit">元素上限。</param>
        private static void WriteSequence(RoslynJsonBuilder builder, IEnumerable sequence, int limit)
        {
            builder.StartArray();
            var written = 0;
            foreach (object item in sequence)
            {
                if (written >= limit)
                {
                    builder.String("<truncated at " + limit + ">");
                    break;
                }

                WriteValue(builder, item, limit);
                written++;
            }

            builder.EndArray();
        }

        /// <summary>从 JSON 对象读取 inspect 请求。选择器缺失时失败，深度和上限另做夹取。</summary>
        /// <param name="root">payload 根元素。</param>
        /// <param name="request">成功时的请求；失败时为 null。</param>
        /// <param name="error">失败原因；成功时为空字符串。</param>
        /// <returns>根是对象且选择器存在时返回 true。</returns>
        private static bool TryReadInspectRequest(JsonElement root, out InspectRequest request, out string error)
        {
            request = null;
            error = string.Empty;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "inspect payload must be a JSON object.";
                return false;
            }

            var parsed = new InspectRequest
            {
                Root = ReadString(root, "root"),
                Selector = ReadString(root, "selector"),
                Path = ReadString(root, "path"),
                Depth = DEFAULT_DEPTH,
                Limit = DEFAULT_LIMIT
            };
            if (parsed.Root.Length == 0)
            {
                parsed.Root = ROOT_SERVICE;
            }

            if (parsed.Selector.Length == 0)
            {
                parsed.Selector = ReadString(root, "type");
            }

            if (parsed.Selector.Length == 0)
            {
                error = "inspect requires 'selector' (or 'type'): the type or object name to read.";
                return false;
            }

            ApplyInspectBounds(root, parsed);
            request = parsed;
            return true;
        }

        /// <summary>夹取可选的 depth 与 limit。缺失或非数字时保留请求里的默认值。</summary>
        /// <param name="root">payload 对象。</param>
        /// <param name="parsed">正在填充的请求。</param>
        private static void ApplyInspectBounds(JsonElement root, InspectRequest parsed)
        {
            if (root.TryGetProperty("depth", out JsonElement depthValue) && depthValue.ValueKind == JsonValueKind.Number
                && depthValue.TryGetInt32(out int depth))
            {
                parsed.Depth = depth < 1 ? 1 : depth > MAX_DEPTH ? MAX_DEPTH : depth;
            }

            if (root.TryGetProperty("limit", out JsonElement limitValue) && limitValue.ValueKind == JsonValueKind.Number
                && limitValue.TryGetInt32(out int limit))
            {
                parsed.Limit = limit < 1 ? 1 : limit > MAX_LIMIT ? MAX_LIMIT : limit;
            }
        }
    }
}
#endif
