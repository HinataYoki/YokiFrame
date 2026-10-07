#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// scene_query：按路径与深度读取当前场景层级的有界快照。
    /// </summary>
    /// <remarks>
    /// 遍历有硬上限（深度 8、节点 2000、每节点组件 24），超限时返回 truncated=true，
    /// 避免大场景把命令响应撑爆或阻塞编辑器 tick（C3）。整个载荷由共享 JSON 写出器构建。
    /// </remarks>
    internal sealed partial class UnitySceneQueryOperation : IRoslynOperation
    {
        internal const string ACTION = "scene_query";

        private const string PATH_FIELD = "path";
        private const string DEPTH_FIELD = "depth";
        private const string INCLUDE_INACTIVE_FIELD = "includeInactive";

        private const string INCLUDE_VALUES_FIELD = "includeValues";

        private const int MAX_FIELDS = 32;

        private const int MAX_STRING_CHARS = 256;

        private const int DEFAULT_DEPTH = 2;
        private const int MAX_DEPTH = 8;
        private const int MAX_NODES = 2000;
        private const int MAX_COMPONENTS = 24;

        /// <summary>创建 scene_query 操作。</summary>
        internal UnitySceneQueryOperation()
        {
            Descriptor = new RoslynOperationDescriptor(
                ACTION,
                YokiFrameCommandKind.ReadOnly,
                isDiagnostic: false,
                isCancellation: false,
                targets: RoslynExecutionTarget.Editor | RoslynExecutionTarget.Play);
        }

        /// <summary>获取操作描述。</summary>
        public RoslynOperationDescriptor Descriptor { get; }

        /// <summary>执行场景查询。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令结果。</returns>
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            if (!TryReadOptions(request.PayloadJson, out string path, out int depth, out bool includeInactive, out bool includeValues, out string error))
            {
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, error);
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.UNAVAILABLE,
                    "No active scene is available for scene_query.");
            }

            GameObject[] roots = scene.GetRootGameObjects();
            var contexts = new List<NodeContext>();

            // "/" 或只由分隔符组成的路径表示"整棵场景"，与空路径等价（文档示例用的就是 "/"）。
            string normalizedPath = path == null ? string.Empty : path.Trim();
            if (normalizedPath.Length > 0 && normalizedPath.Trim('/').Length == 0)
            {
                normalizedPath = string.Empty;
            }

            if (string.IsNullOrEmpty(normalizedPath))
            {
                for (var index = 0; index < roots.Length; index++)
                {
                    contexts.Add(new NodeContext(roots[index], roots[index].name));
                }
            }
            else
            {
                if (!TryResolvePath(roots, normalizedPath, out GameObject target, out string pathError))
                {
                    return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, pathError);
                }

                contexts.Add(new NodeContext(target, normalizedPath));
            }

            var state = new TraversalState { Remaining = MAX_NODES };
            RoslynJsonBuilder builder = new RoslynJsonBuilder()
                .StartObject()
                .Property("operation", ACTION)
                .Name("scene")
                .StartObject()
                .Property("name", scene.name)
                .Property("path", scene.path)
                .Property("isDirty", scene.isDirty)
                .Property("rootCount", scene.rootCount)
                .EndObject()
                .Property("requestedPath", path)
                .Property("depth", depth)
                .Property("includeInactive", includeInactive)
                .Name("nodes")
                .StartArray();
            for (var index = 0; index < contexts.Count; index++)
            {
                if (!WriteNode(builder, contexts[index], depth, includeInactive, includeValues, state))
                {
                    break;
                }
            }

            return YokiFrameCommandResult.Success(builder
                .EndArray()
                .Property("nodeCount", state.Written)
                .Property("truncated", state.Truncated)
                .EndObject()
                .ToString());
        }

        private readonly struct NodeContext
        {
            internal NodeContext(GameObject gameObject, string path)
            {
                GameObject = gameObject;
                Path = path;
            }

            internal GameObject GameObject { get; }

            internal string Path { get; }
        }

        private sealed class TraversalState
        {
            internal int Remaining;
            internal int Written;
            internal bool Truncated;
        }

        /// <summary>
        /// 递归写出单个节点；节点预算用尽时返回 false 让上层停止遍历。
        /// </summary>
        /// <param name="builder">JSON 写出器。</param>
        /// <param name="context">节点上下文。</param>
        /// <param name="depth">剩余深度。</param>
        /// <param name="includeInactive">是否包含未激活子物体。</param>
        /// <param name="state">遍历状态。</param>
        /// <returns>写出成功时返回 true。</returns>
        /// <summary>
        /// 写出组件字段值（opt-in，payload 传 includeValues=true）。
        /// 覆盖公开字段与标了 [SerializeField] 的私有字段——**读的是活动对象上的真实值**，
        /// 而不是脚本文件里的初值；只输出可稳定序列化的类型，避免把整个对象图塞进命令结果。
        /// </summary>
        /// <param name="builder">目标 builder。</param>
        /// <param name="components">组件数组。</param>
        private static void AppendComponentValues(RoslynJsonBuilder builder, Component[] components)
        {
            builder.Name("componentValues").StartArray();
            int limit = components.Length < MAX_COMPONENTS ? components.Length : MAX_COMPONENTS;
            for (var index = 0; index < limit; index++)
            {
                Component component = components[index];
                if (component == null)
                {
                    continue;
                }

                Type type = component.GetType();
                builder.StartObject().Property("type", type.Name).Name("fields").StartObject();
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var written = 0;
                for (var fieldIndex = 0; fieldIndex < fields.Length && written < MAX_FIELDS; fieldIndex++)
                {
                    FieldInfo field = fields[fieldIndex];
                    bool serialized = field.IsPublic || field.IsDefined(typeof(SerializeField), inherit: true);
                    if (!serialized || field.IsInitOnly || field.IsStatic)
                    {
                        continue;
                    }

                    if (!TryFormatValue(field.GetValue(component), out string formatted))
                    {
                        continue;
                    }

                    builder.Property(field.Name, formatted);
                    written++;
                }

                builder.EndObject().EndObject();
            }

            builder.EndArray();
        }



        private static bool WriteNode(
            RoslynJsonBuilder builder,
            NodeContext context,
            int depth,
            bool includeInactive,
            bool includeValues,
            TraversalState state)
        {
            if (state.Remaining <= 0)
            {
                state.Truncated = true;
                return false;
            }

            GameObject gameObject = context.GameObject;
            state.Remaining--;
            state.Written++;

            Component[] components = gameObject.GetComponents<Component>();
            builder.StartObject()
                .Property("name", gameObject.name)
                .Property("path", context.Path)
                .Property("active", gameObject.activeSelf)
                .Property("activeInHierarchy", gameObject.activeInHierarchy)
                .Property("childCount", gameObject.transform.childCount)
                .Property("componentCount", components.Length);
            AppendTransform(builder, gameObject.transform);
            if (includeValues)
            {
                AppendComponentValues(builder, components);
            }

            builder.Name("components")
                .StartArray();
            int limit = components.Length < MAX_COMPONENTS ? components.Length : MAX_COMPONENTS;
            for (var index = 0; index < limit; index++)
            {
                Component component = components[index];
                builder.String(component == null ? "MissingScript" : component.GetType().Name);
            }

            builder.EndArray();

            if (depth > 0)
            {
                builder.Name("children").StartArray();
                for (var index = 0; index < gameObject.transform.childCount; index++)
                {
                    Transform child = gameObject.transform.GetChild(index);
                    if (!includeInactive && !child.gameObject.activeSelf)
                    {
                        continue;
                    }

                    if (!WriteNode(
                            builder,
                            new NodeContext(child.gameObject, context.Path + "/" + child.gameObject.name),
                            depth - 1,
                            includeInactive,
                            includeValues,
                            state))
                    {
                        break;
                    }
                }

                builder.EndArray();
            }

            builder.EndObject();
            return true;
        }

        /// <summary>
        /// 写出节点变换；键名与 Godot 侧契约一致（kind / position / rotationDegrees / scale），
        /// 另外补 localPosition——Unity 里"世界坐标"与"本地坐标"的差别在排查层级偏移时是必需的。
        /// </summary>
        /// <param name="builder">目标 builder。</param>
        /// <param name="transform">Unity 变换。</param>
        private static void AppendTransform(RoslynJsonBuilder builder, Transform transform)
        {
            builder.Name("transform")
                .StartObject()
                .Property("kind", "3d")
                .Name("position")
                .StartArray()
                .String(Format(transform.position.x))
                .String(Format(transform.position.y))
                .String(Format(transform.position.z))
                .EndArray()
                .Name("localPosition")
                .StartArray()
                .String(Format(transform.localPosition.x))
                .String(Format(transform.localPosition.y))
                .String(Format(transform.localPosition.z))
                .EndArray()
                .Name("rotationDegrees")
                .StartArray()
                .String(Format(transform.eulerAngles.x))
                .String(Format(transform.eulerAngles.y))
                .String(Format(transform.eulerAngles.z))
                .EndArray()
                .Name("scale")
                .StartArray()
                .String(Format(transform.localScale.x))
                .String(Format(transform.localScale.y))
                .String(Format(transform.localScale.z))
                .EndArray()
                .EndObject();
        }

        /// <summary>以不变文化格式化浮点，保证跨区域设置下输出稳定。</summary>
        /// <param name="value">数值。</param>
        /// <returns>文本。</returns>
        private static string Format(float value)
        {
            return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool TryResolvePath(GameObject[] roots, string path, out GameObject target, out string error)
        {
            target = null;
            error = string.Empty;
            string[] segments = path.Split('/');
            GameObject current = null;
            GameObject[] level = roots;
            for (var index = 0; index < segments.Length; index++)
            {
                string segment = segments[index].Trim();
                if (segment.Length == 0)
                {
                    continue;
                }

                GameObject match = null;
                for (var candidate = 0; candidate < level.Length; candidate++)
                {
                    if (string.Equals(level[candidate].name, segment, System.StringComparison.Ordinal))
                    {
                        match = level[candidate];
                        break;
                    }
                }

                if (match == null)
                {
                    error = "scene_query path segment was not found: " + segment + ".";
                    return false;
                }

                current = match;
                var children = new List<GameObject>(match.transform.childCount);
                for (var childIndex = 0; childIndex < match.transform.childCount; childIndex++)
                {
                    children.Add(match.transform.GetChild(childIndex).gameObject);
                }

                level = children.ToArray();
            }

            if (current == null)
            {
                // 走到这里说明路径里只有分隔符（例如 "///"）；调用方已把 "/" 归一化为整棵场景，
                // 因此这里给一个不再误导的诊断。
                error = "scene_query path did not resolve to a scene object.";
                return false;
            }

            target = current;
            return true;
        }

        private static bool TryReadOptions(
            string payloadJson,
            out string path,
            out int depth,
            out bool includeInactive,
            out bool includeValues,
            out string error)
        {
            path = string.Empty;
            depth = DEFAULT_DEPTH;
            includeInactive = true;
            includeValues = false;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return true;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(payloadJson);
            }
            catch (JsonException exception)
            {
                error = "scene_query payload is not valid JSON: " + exception.Message;
                return false;
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    error = "scene_query payload must be a JSON object.";
                    return false;
                }

                if (root.TryGetProperty(PATH_FIELD, out JsonElement pathValue) && pathValue.ValueKind != JsonValueKind.Null)
                {
                    if (pathValue.ValueKind != JsonValueKind.String)
                    {
                        error = "scene_query path must be a string.";
                        return false;
                    }

                    path = pathValue.GetString() ?? string.Empty;
                }

                if (root.TryGetProperty(INCLUDE_VALUES_FIELD, out JsonElement valuesValue)
                    && (valuesValue.ValueKind == JsonValueKind.True || valuesValue.ValueKind == JsonValueKind.False))
                {
                    includeValues = valuesValue.GetBoolean();
                }

                if (root.TryGetProperty(INCLUDE_INACTIVE_FIELD, out JsonElement includeValue)
                    && includeValue.ValueKind != JsonValueKind.Null)
                {
                    if (includeValue.ValueKind != JsonValueKind.True && includeValue.ValueKind != JsonValueKind.False)
                    {
                        error = "scene_query includeInactive must be a boolean.";
                        return false;
                    }

                    includeInactive = includeValue.ValueKind == JsonValueKind.True;
                }

                if (root.TryGetProperty(DEPTH_FIELD, out JsonElement depthValue) && depthValue.ValueKind != JsonValueKind.Null)
                {
                    if (!depthValue.TryGetInt32(out int requested))
                    {
                        error = "scene_query depth must be an integer.";
                        return false;
                    }

                    depth = requested < 0 ? 0 : requested > MAX_DEPTH ? MAX_DEPTH : requested;
                }

                return true;
            }
        }
    }
}
#endif
