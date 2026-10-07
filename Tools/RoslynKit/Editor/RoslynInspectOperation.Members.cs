#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using YokiFrame.Json;

namespace YokiFrame
{
    public sealed partial class RoslynInspectOperation
    {
        /// <summary>按根名和选择器解析实例；未知根直接失败，不触发懒初始化。</summary>
        /// <param name="parsed">已校验的请求。</param>
        /// <param name="instance">解析到的实例；静态根可以为 null。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>根存在且解析成功时返回 true。</returns>
        private bool TryResolveRoot(InspectRequest parsed, out object instance, out string error)
        {
            if (mRoots.Find(parsed.Root) == null)
            {
                instance = null;
                error = "unknown root: " + parsed.Root + ". Known roots: " + mRoots.DescribeRoots() + ".";
                return false;
            }

            if (!mRoots.TryResolve(parsed.Root, parsed.Selector, out instance, out string resolveError))
            {
                error = resolveError;
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>从实例或静态类型沿路径逐段读取成员和索引。</summary>
        /// <param name="instance">当前实例；静态成员读取时为 null。</param>
        /// <param name="path">点号成员与方括号索引组成的路径。</param>
        /// <param name="limit">传给索引读取的上限参数；路径本身不另做截断。</param>
        /// <param name="value">解析成功时的最终值。</param>
        /// <param name="error">失败原因；成功时为空字符串。</param>
        /// <param name="resolvedPath">成功走过的路径文本。</param>
        /// <param name="staticType">静态根类型；实例路径省略。</param>
        /// <returns>路径完整解析时返回 true。任一段失败后不再继续。</returns>
        private static bool TryWalk(
            object instance,
            string path,
            int limit,
            out object value,
            out string error,
            out string resolvedPath,
            Type staticType = null)
        {
            value = instance;
            error = string.Empty;
            resolvedPath = string.Empty;
            Type currentType = staticType ?? (instance == null ? null : instance.GetType());
            int index = 0;
            while (index < path.Length)
            {
                char symbol = path[index];
                if (symbol == '.')
                {
                    index++;
                    continue;
                }

                if (symbol == '[')
                {
                    if (!TryWalkIndex(path, limit, ref index, ref value, ref error, ref resolvedPath, ref currentType))
                    {
                        return false;
                    }

                    continue;
                }

                if (!TryWalkMember(path, ref index, ref value, ref currentType, ref error, ref resolvedPath))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>读取同名字段，否则读取无参属性。属性 getter 的目标异常会转成失败原因。</summary>
        /// <param name="instance">实例；静态成员时可为 null。</param>
        /// <param name="type">当前值的类型。前一段为 null 时失败，不继续反射。</param>
        /// <param name="member">成员名。</param>
        /// <param name="value">读到的值。</param>
        /// <param name="error">找不到成员或 getter 抛出时的原因。</param>
        /// <returns>找到字段或成功读取属性时返回 true。</returns>
        private static bool TryReadMember(object instance, Type type, string member, out object value, out string error)
        {
            value = null;
            error = string.Empty;
            if (type == null)
            {
                error = "cannot read member '" + member + "': the previous value was null.";
                return false;
            }

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo field = type.GetField(member, Flags);
            if (field != null)
            {
                value = field.GetValue(field.IsStatic ? null : instance);
                return true;
            }

            PropertyInfo property = type.GetProperty(member, Flags);
            if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
            {
                try
                {
                    value = property.GetValue(property.GetGetMethod(nonPublic: true).IsStatic ? null : instance);
                    return true;
                }
                catch (TargetInvocationException exception)
                {
                    error = "property '" + member + "' threw: " + (exception.InnerException ?? exception).Message;
                    return false;
                }
            }

            error = "member was not found on " + type.FullName + ": " + member + ".";
            return false;
        }

        /// <summary>按字典、列表、字符串索引器的顺序用键取值。</summary>
        /// <param name="container">被索引的对象。</param>
        /// <param name="key">字典键、列表下标或索引器参数。</param>
        /// <param name="limit">保留的调用参数。索引逻辑不使用它做截断。</param>
        /// <param name="value">命中的元素。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>某一类容器命中时返回 true。</returns>
        private static bool TryIndex(object container, string key, int limit, out object value, out string error)
        {
            value = null;
            error = string.Empty;
            if (container == null)
            {
                error = "cannot index a null value with [" + key + "].";
                return false;
            }

            if (container is IDictionary dictionary)
            {
                return TryIndexDictionary(dictionary, key, out value, out error);
            }

            if (container is IList list)
            {
                return TryIndexList(list, key, out value, out error);
            }

            return TryIndexStringIndexer(container, key, out value, out error);
        }

        /// <summary>把字段和属性摘要写成 JSON 数组。先字段后属性，合计不超过 limit。</summary>
        /// <param name="builder">正在写入的 JSON。</param>
        /// <param name="instance">实例；静态摘要时为 null。</param>
        /// <param name="depth">剩余展开深度。</param>
        /// <param name="limit">字段与属性合计的写入上限。</param>
        /// <param name="staticType">静态类型；实例路径省略。</param>
        private static void WriteMemberSummary(
            RoslynJsonBuilder builder,
            object instance,
            int depth,
            int limit,
            Type staticType = null)
        {
            Type type = staticType ?? (instance == null ? null : instance.GetType());
            builder.StartArray();
            if (type == null)
            {
                builder.EndArray();
                return;
            }

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            int written = WriteFields(builder, instance, type, depth, limit, Flags);
            WriteProperties(builder, instance, type, depth, limit, Flags, written);
            builder.EndArray();
        }

        /// <summary>写一个成员的名称、类型和值。深度足够且可展开时递归写成员摘要。</summary>
        /// <param name="builder">正在写入的 JSON。</param>
        /// <param name="name">成员名。</param>
        /// <param name="value">成员值。</param>
        /// <param name="depth">剩余展开深度。</param>
        /// <param name="limit">嵌套摘要或集合的上限。</param>
        private static void WriteEntry(
            RoslynJsonBuilder builder,
            string name,
            object value,
            int depth,
            int limit)
        {
            builder.StartObject().Property("name", name).Property("type", value == null ? "null" : value.GetType().Name);
            if (depth > 1 && value != null && IsExpandable(value))
            {
                builder.Name("value");
                WriteMemberSummary(builder, value, depth - 1, limit);
            }
            else
            {
                builder.Name("value");
                WriteValue(builder, value, limit);
            }

            builder.EndObject();
        }

        /// <summary>判断值是否应按对象继续展开，而不是当作标量或序列。</summary>
        /// <param name="value">非 null 的值。</param>
        /// <returns>非基元、非枚举、非字符串、非 decimal 且非序列时返回 true。</returns>
        private static bool IsExpandable(object value)
        {
            Type type = value.GetType();
            return !type.IsPrimitive
                && !type.IsEnum
                && value is not string
                && value is not decimal
                && !(value is IEnumerable);
        }

        /// <summary>把运行时值写成 JSON。序列按 limit 截断，其余复杂对象写成截断后的文本。</summary>
        /// <param name="builder">正在写入的 JSON。</param>
        /// <param name="value">要写的值。</param>
        /// <param name="limit">序列元素上限。</param>
        private static void WriteValue(RoslynJsonBuilder builder, object value, int limit)
        {
            if (value == null)
            {
                builder.String("null");
                return;
            }

            if (value is string text)
            {
                builder.String(Truncate(text));
                return;
            }

            if (value is bool flag)
            {
                builder.Property("value", flag);
                return;
            }

            if (value is Enum)
            {
                builder.String(value.ToString());
                return;
            }

            Type type = value.GetType();
            if (type.IsPrimitive || value is decimal)
            {
                builder.String(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                return;
            }

            if (value is IEnumerable sequence)
            {
                WriteSequence(builder, sequence, limit);
                return;
            }

            builder.String(Truncate(Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.GetType().Name));
        }

        /// <summary>把文本限制在 <see cref="MAX_TEXT_CHARS"/> 以内。null 视为空字符串。</summary>
        /// <param name="text">原始文本。</param>
        /// <returns>未超过上限时返回原文，否则返回前缀。</returns>
        private static string Truncate(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            return text.Length <= MAX_TEXT_CHARS ? text : text.Substring(0, MAX_TEXT_CHARS);
        }

        /// <summary>先按程序集限定名解析类型，再扫描已加载程序集。单个程序集抛错时跳过。</summary>
        /// <param name="name">类型名。</param>
        /// <returns>找到的类型；都没有时返回 null。</returns>
        private static Type ResolveType(string name)
        {
            Type direct = Type.GetType(name);
            if (direct != null)
            {
                return direct;
            }

            foreach (Assembly assembly in LoadedAssemblies.Get())
            {
                Type match;
                try
                {
                    match = assembly.GetType(name);
                }
                catch (Exception)
                {
                    continue;
                }

                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        /// <summary>解析 inspect 载荷。空白载荷和非法 JSON 直接失败，不创建请求。</summary>
        /// <param name="payloadJson">命令载荷。</param>
        /// <param name="request">成功时的请求。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>载荷是带选择器的 JSON 对象时返回 true。</returns>
        private static bool TryParse(string payloadJson, out InspectRequest request, out string error)
        {
            request = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                error = "inspect requires at least a selector (type name).";
                return false;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(payloadJson);
            }
            catch (JsonException exception)
            {
                error = "inspect payload is not valid JSON: " + exception.Message;
                return false;
            }

            using (document)
            {
                return TryReadInspectRequest(document.RootElement, out request, out error);
            }
        }

        /// <summary>读取 JSON 字符串属性。缺失或非字符串时返回空，不抛异常。</summary>
        /// <param name="element">对象元素。</param>
        /// <param name="name">属性名。</param>
        /// <returns>字符串值；内容为 null 时视为空。</returns>
        private static string ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }

        private sealed class InspectRequest
        {
            internal string Root { get; set; } = ROOT_SERVICE;

            internal string Selector { get; set; } = string.Empty;

            internal string Path { get; set; } = string.Empty;

            internal int Depth { get; set; } = DEFAULT_DEPTH;

            internal int Limit { get; set; } = DEFAULT_LIMIT;
        }
    }

    /// <summary>
    /// 引擎/游戏侧提供 inspect 的根：默认根之外的自定义寻址。
    /// </summary>
    public interface IInspectRootSource
    {
        /// <summary>获取额外的根；可为空。</summary>
        IReadOnlyList<IInspectRoot> Roots { get; }
    }
}
#endif
