#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using YokiFrame.Json;

namespace YokiFrame
{
    internal sealed partial class UnitySceneQueryOperation
    {
        /// <summary>
        /// 把请求路径展开为起始节点。空白或纯斜杠表示整棵场景；
        /// 解析失败时不写入任何节点。
        /// </summary>
        /// <param name="roots">活动场景根物体。</param>
        /// <param name="path">原始请求路径。</param>
        /// <param name="contexts">起始节点。</param>
        /// <param name="error">失败说明。</param>
        /// <returns>至少可以开始遍历时返回 true。</returns>
        private static bool TryCollectContexts(
            GameObject[] roots,
            string path,
            out List<NodeContext> contexts,
            out string error)
        {
            contexts = new List<NodeContext>();
            error = string.Empty;
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

                return true;
            }

            if (!TryResolvePath(roots, normalizedPath, out GameObject target, out error))
            {
                return false;
            }

            contexts.Add(new NodeContext(target, normalizedPath));
            return true;
        }

        /// <summary>
        /// 写出场景头和节点数组。节点预算耗尽时停止，并在结果里带回 truncated。
        /// </summary>
        /// <param name="scene">活动场景。</param>
        /// <param name="path">原始请求路径，不使用归一化结果。</param>
        /// <param name="depth">剩余深度。</param>
        /// <param name="includeInactive">是否包含未激活物体。</param>
        /// <param name="includeValues">是否写出字段值。</param>
        /// <param name="contexts">起始节点。</param>
        /// <returns>成功的命令结果。</returns>
        private static YokiFrameCommandResult WriteQuery(
            Scene scene,
            string path,
            int depth,
            bool includeInactive,
            bool includeValues,
            List<NodeContext> contexts)
        {
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

        /// <summary>写出节点身份、激活状态、组件数量和变换。不关闭对象。</summary>
        /// <param name="builder">JSON 写出器。</param>
        /// <param name="context">节点上下文。</param>
        /// <param name="gameObject">当前物体。</param>
        /// <param name="components">已取到的组件，数量按原数组长度报告。</param>
        private static void WriteNodeHeader(
            RoslynJsonBuilder builder,
            NodeContext context,
            GameObject gameObject,
            Component[] components)
        {
            builder.StartObject()
                .Property("name", gameObject.name)
                .Property("path", context.Path)
                .Property("active", gameObject.activeSelf)
                .Property("activeInHierarchy", gameObject.activeInHierarchy)
                .Property("childCount", gameObject.transform.childCount)
                .Property("componentCount", components.Length);
            AppendTransform(builder, gameObject.transform);
        }

        /// <summary>按上限写出组件类型名；缺失脚本记为 MissingScript。</summary>
        /// <param name="builder">JSON 写出器。</param>
        /// <param name="components">组件数组。</param>
        private static void WriteComponentNames(RoslynJsonBuilder builder, Component[] components)
        {
            builder.Name("components")
                .StartArray();
            int limit = components.Length < MAX_COMPONENTS ? components.Length : MAX_COMPONENTS;
            for (var index = 0; index < limit; index++)
            {
                Component component = components[index];
                builder.String(component == null ? "MissingScript" : component.GetType().Name);
            }

            builder.EndArray();
        }

        /// <summary>
        /// 写出直接子节点。未激活子物体可跳过；子节点返回 false 时停止，但仍关闭 children 数组。
        /// </summary>
        /// <param name="builder">JSON 写出器。</param>
        /// <param name="gameObject">父物体。</param>
        /// <param name="parentPath">父路径。</param>
        /// <param name="depth">父节点剩余深度，子节点使用 depth-1。</param>
        /// <param name="includeInactive">是否包含未激活子物体。</param>
        /// <param name="includeValues">是否写出字段值。</param>
        /// <param name="state">遍历状态。</param>
        private static void WriteChildren(
            RoslynJsonBuilder builder,
            GameObject gameObject,
            string parentPath,
            int depth,
            bool includeInactive,
            bool includeValues,
            TraversalState state)
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
                        new NodeContext(child.gameObject, parentPath + "/" + child.gameObject.name),
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

        /// <summary>
        /// 在当前层匹配一个路径段。空白段跳过；未命中时返回 false 且不改当前物体。
        /// </summary>
        /// <param name="rawSegment">原始路径段。</param>
        /// <param name="current">最近一次命中的物体。</param>
        /// <param name="level">当前层候选。</param>
        /// <param name="error">失败说明。</param>
        /// <returns>可以继续下一段时返回 true。</returns>
        private static bool TryMatchSegment(
            string rawSegment,
            ref GameObject current,
            ref GameObject[] level,
            out string error)
        {
            error = string.Empty;
            string segment = rawSegment.Trim();
            if (segment.Length == 0)
            {
                return true;
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
            return true;
        }

        /// <summary>
        /// 按 path、includeValues、includeInactive、depth 的原顺序读取字段。
        /// 缺字段保持调用方默认值；类型不对立即失败。
        /// </summary>
        /// <param name="root">payload 对象。</param>
        /// <param name="path">路径。</param>
        /// <param name="depth">深度。</param>
        /// <param name="includeInactive">是否包含未激活物体。</param>
        /// <param name="includeValues">是否写出字段值。</param>
        /// <param name="error">失败说明。</param>
        /// <returns>字段类型合法时返回 true。</returns>
        private static bool TryReadQueryFields(
            JsonElement root, ref string path, ref int depth,
            ref bool includeInactive, ref bool includeValues, out string error)
        {
            error = string.Empty;
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
#endif
