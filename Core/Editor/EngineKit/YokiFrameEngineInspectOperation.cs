#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// inspect：按「根 + 选择器 + 路径」反射读取**活动对象上的真实值**，纯读、零编译、不落盘。
    /// </summary>
    /// <remarks>
    /// 为什么需要它（而不是 eval）：eval 要靠工程编译，只能在编辑器、且每次读都要写盘 + 编译；
    /// 而"查看运行时状态"是高频只读操作，必须是反射直读。
    /// 通用性来自两点：① 路径求值（成员 / 字典键 / 索引，可嵌套）；
    /// ② **可插拔的根**（<see cref="IYokiFrameInspectRootProvider"/>）——任何框架或游戏都能把自己的寻址方式注册进来，
    /// 核心不需要认识任何具体框架类型。
    /// </remarks>
    public sealed class YokiFrameEngineInspectOperation : IYokiFrameEngineOperation
    {
        /// <summary>动作名。</summary>
        public const string ACTION = "inspect";

        /// <summary>根名：已注册的服务/模型（由宿主通过 roots 提供）。</summary>
        public const string ROOT_SERVICE = "service";

        /// <summary>根名：类型上的静态成员。</summary>
        public const string ROOT_STATIC = "static";

        /// <summary>成员/集合默认返回上限。</summary>
        public const int DEFAULT_LIMIT = 50;

        /// <summary>成员/集合返回上限的最大值。</summary>
        public const int MAX_LIMIT = 200;

        /// <summary>无路径时展开的默认深度。</summary>
        public const int DEFAULT_DEPTH = 1;

        /// <summary>无路径时展开的最大深度。</summary>
        public const int MAX_DEPTH = 3;

        /// <summary>文本值最大长度。</summary>
        public const int MAX_TEXT_CHARS = 256;

        private readonly YokiFrameInspectRootRegistry mRoots;

        /// <summary>创建 inspect 操作。</summary>
        /// <param name="roots">根的注册表；为空时使用内置根。</param>
        public YokiFrameEngineInspectOperation(YokiFrameInspectRootRegistry roots = null)
        {
            mRoots = roots ?? YokiFrameInspectRootRegistry.CreateDefault();
            Descriptor = new YokiFrameEngineOperationDescriptor(
                ACTION,
                YokiFrameCommandKind.ReadOnly,
                isDiagnostic: true,
                isCancellation: false,
                targets: YokiFrameEngineExecutionTargets.ALL,
                isTargetAgnostic: true);
        }

        /// <summary>获取操作描述。</summary>
        public YokiFrameEngineOperationDescriptor Descriptor { get; }

        /// <summary>执行 inspect。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令结果。</returns>
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            if (!TryParse(request.PayloadJson, out InspectRequest parsed, out string parseError))
            {
                return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, parseError);
            }

            if (!TryResolveRoot(parsed, out object instance, out string rootError))
            {
                return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.INSPECT_TARGET_NOT_FOUND, rootError);
            }

            var builder = new YokiFrameEngineJsonBuilder()
                .StartObject()
                .Property("operation", ACTION)
                .Property("root", parsed.Root)
                .Property("selector", parsed.Selector);
            if (instance == null)
            {
                // 静态根：直接读类型上的静态成员。
                return WriteStatic(parsed, builder);
            }

            builder.Property("instanceType", instance.GetType().FullName);
            if (parsed.Path.Length == 0)
            {
                builder.Property("path", string.Empty).Name("members");
                WriteMemberSummary(builder, instance, parsed.Depth, parsed.Limit);
                return YokiFrameCommandResult.Success(builder.EndObject().ToString());
            }

            if (!TryWalk(instance, parsed.Path, parsed.Limit, out object value, out string memberError, out string resolvedPath))
            {
                return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.INSPECT_MEMBER_NOT_FOUND, memberError);
            }

            builder.Property("path", resolvedPath)
                .Property("valueType", value == null ? "null" : value.GetType().FullName)
                .Name("value");
            WriteValue(builder, value, parsed.Limit);
            return YokiFrameCommandResult.Success(builder.EndObject().ToString());
        }

        private YokiFrameCommandResult WriteStatic(InspectRequest parsed, YokiFrameEngineJsonBuilder builder)
        {
            Type type = ResolveType(parsed.Selector);
            if (type == null)
            {
                return YokiFrameCommandResult.Error(
                    YokiFrameEngineErrorCodes.INSPECT_TARGET_NOT_FOUND,
                    "type was not found: " + parsed.Selector);
            }

            builder.Property("instanceType", type.FullName);
            if (parsed.Path.Length == 0)
            {
                builder.Property("path", string.Empty).Name("members");
                WriteMemberSummary(builder, null, parsed.Depth, parsed.Limit, type);
                return YokiFrameCommandResult.Success(builder.EndObject().ToString());
            }

            if (!TryWalk(null, parsed.Path, parsed.Limit, out object value, out string memberError, out string resolvedPath, type))
            {
                return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.INSPECT_MEMBER_NOT_FOUND, memberError);
            }

            builder.Property("path", resolvedPath)
                .Property("valueType", value == null ? "null" : value.GetType().FullName)
                .Name("value");
            WriteValue(builder, value, parsed.Limit);
            return YokiFrameCommandResult.Success(builder.EndObject().ToString());
        }

        /// <summary>
        /// 解析根：**完全委托给注册表**。高层只认识「根名 + 选择器 → 实例」这一个契约。
        /// </summary>
        /// <param name="parsed">请求。</param>
        /// <param name="instance">解析到的实例；静态根为 null 且返回 true。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>成功时返回 true。</returns>
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
                    continue;
                }

                int end = index;
                while (end < path.Length && path[end] != '.' && path[end] != '[')
                {
                    end++;
                }

                string member = path.Substring(index, end - index).Trim();
                if (member.Length == 0)
                {
                    index = end;
                    continue;
                }

                if (!TryReadMember(value, currentType, member, out value, out error))
                {
                    return false;
                }

                resolvedPath += (resolvedPath.Length == 0 ? string.Empty : ".") + member;
                currentType = value == null ? null : value.GetType();
                index = end;
            }

            return true;
        }

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
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (string.Equals(Convert.ToString(entry.Key, CultureInfo.InvariantCulture), key, StringComparison.Ordinal))
                    {
                        value = entry.Value;
                        return true;
                    }
                }

                error = "dictionary key was not found: " + key + ".";
                return false;
            }

            if (container is IList list)
            {
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

                return true;
            }

            error = "value of type " + container.GetType().FullName + " does not support [" + key + "].";
            return false;
        }

        private static void WriteMemberSummary(
            YokiFrameEngineJsonBuilder builder,
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
            var written = 0;
            FieldInfo[] fields = type.GetFields(Flags);
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

            PropertyInfo[] properties = type.GetProperties(Flags);
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

            builder.EndArray();
        }

        private static void WriteEntry(
            YokiFrameEngineJsonBuilder builder,
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

        private static bool IsExpandable(object value)
        {
            Type type = value.GetType();
            return !type.IsPrimitive
                && !type.IsEnum
                && value is not string
                && value is not decimal
                && !(value is IEnumerable);
        }

        private static void WriteValue(YokiFrameEngineJsonBuilder builder, object value, int limit)
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
                return;
            }

            builder.String(Truncate(Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.GetType().Name));
        }

        private static string Truncate(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            return text.Length <= MAX_TEXT_CHARS ? text : text.Substring(0, MAX_TEXT_CHARS);
        }

        private static Type ResolveType(string name)
        {
            Type direct = Type.GetType(name);
            if (direct != null)
            {
                return direct;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                Type match;
                try
                {
                    match = assemblies[index].GetType(name);
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
                JsonElement root = document.RootElement;
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

                request = parsed;
                return true;
            }
        }

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
    public interface IYokiFrameInspectRootSource
    {
        /// <summary>获取额外的根；可为空。</summary>
        IReadOnlyList<IYokiFrameInspectRoot> Roots { get; }
    }
}
#endif