#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using Godot;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// Godot 场景查询的共享内核：路径解析、有界遍历与 JSON 投影。
    /// </summary>
    /// <remarks>
    /// 编辑器与 Runtime 两个宿主共用同一份实现与同一份 payload 契约（path/depth/includeInactive），
    /// 因此 Agent 在 Unity 与 Godot 之间切换时不需要换 schema。
    /// 遍历有硬上限：深度 ≤8、节点 ≤2000、每组子节点 ≤256，超限返回 truncated=true。
    /// </remarks>
    public static partial class GodotSceneQuerySupport
    {
        /// <summary>默认深度。</summary>
        public const int DEFAULT_DEPTH = 2;

        /// <summary>最大深度。</summary>
        public const int MAX_DEPTH = 8;

        /// <summary>最大节点数。</summary>
        public const int MAX_NODES = 2000;

        /// <summary>单个节点的最大子节点枚举数。</summary>
        public const int MAX_CHILDREN_PER_NODE = 256;

        /// <summary>解析查询选项。</summary>
        /// <param name="payloadJson">payload。</param>
        /// <param name="path">请求路径。</param>
        /// <param name="depth">深度。</param>
        /// <param name="includeInactive">是否包含不可见节点。</param>
        /// <param name="error">失败说明。</param>
        /// <returns>解析成功时返回 true。</returns>
        public static bool TryReadOptions(
            string payloadJson,
            out string path,
            out int depth,
            out bool includeInactive,
            out string error)
        {
            path = string.Empty;
            depth = DEFAULT_DEPTH;
            includeInactive = true;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return true;
            }

            if (!TryParseObject(payloadJson, out JsonDocument document, out error))
            {
                return false;
            }

            using (document)
            {
                return TryReadQueryFields(document.RootElement, ref path, ref depth, ref includeInactive, out error);
            }
        }

        /// <summary>把请求路径展开为起始节点。空路径表示从场景根开始，不把斜杠归一化成整棵场景。</summary>
        /// <param name="root">场景根。</param>
        /// <param name="path">原始请求路径。</param>
        /// <param name="roots">起始节点。</param>
        /// <param name="failure">路径不存在时的命令结果。</param>
        /// <returns>至少可以开始遍历时返回 true。</returns>
        public static bool TryCollectRoots(Node root, string path, out List<Node> roots, out YokiFrameCommandResult failure)
        {
            roots = new List<Node>();
            failure = null;
            if (string.IsNullOrEmpty(path))
            {
                roots.Add(root);
                return true;
            }

            Node target = FindByPath(root, path);
            if (target == null)
            {
                failure = YokiFrameCommandResult.Error(
                    RoslynErrorCodes.INVALID_PAYLOAD,
                    "scene_query path was not found: " + path + ".");
                return false;
            }

            roots.Add(target);
            return true;
        }

        /// <summary>写出场景头和节点数组。host 只区分 editor/runtime，节点投影保持同一契约。</summary>
        /// <param name="action">操作名。</param>
        /// <param name="host">editor 或 runtime。</param>
        /// <param name="root">用于场景头的场景根，不随请求路径改变。</param>
        /// <param name="path">原始请求路径。</param>
        /// <param name="depth">剩余深度。</param>
        /// <param name="includeInactive">是否包含不可见节点。</param>
        /// <param name="roots">起始节点。</param>
        /// <returns>成功的命令结果。</returns>
        public static YokiFrameCommandResult WriteQuery(
            string action,
            string host,
            Node root,
            string path,
            int depth,
            bool includeInactive,
            List<Node> roots)
        {
            var state = new GodotSceneTraversalState();
            RoslynJsonBuilder builder = new RoslynJsonBuilder()
                .StartObject()
                .Property("operation", action)
                .Property("host", host)
                .Name("scene")
                .StartObject()
                .Property("name", root.Name.ToString())
                .Property("path", root.SceneFilePath ?? string.Empty)
                .Property("childCount", root.GetChildCount())
                .EndObject()
                .Property("requestedPath", path)
                .Property("depth", depth)
                .Property("includeInactive", includeInactive)
                .Name("nodes");
            WriteNodes(builder, roots, depth, includeInactive, state);
            return YokiFrameCommandResult.Success(builder
                .Property("nodeCount", state.Written)
                .Property("truncated", state.Truncated)
                .EndObject()
                .ToString());
        }

        /// <summary>按 "Root/Child" 路径查找节点。</summary>
        /// <param name="root">场景根节点。</param>
        /// <param name="path">路径。</param>
        /// <returns>命中节点；找不到时返回 null。</returns>
        public static Node FindByPath(Node root, string path)
        {
            if (root == null || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            Node current = root;
            string[] segments = path.Split('/');
            for (var index = 0; index < segments.Length; index++)
            {
                string segment = segments[index].Trim();
                if (segment.Length == 0 || string.Equals(segment, ".", StringComparison.Ordinal))
                {
                    continue;
                }

                if (current == null)
                {
                    return null;
                }

                if (string.Equals(current.Name.ToString(), segment, StringComparison.Ordinal))
                {
                    // 路径首段允许直接命中根自身。
                    continue;
                }

                current = current.GetNodeOrNull(new NodePath(segment));
            }

            return current;
        }

        /// <summary>遍历并写出节点数组。</summary>
        /// <param name="builder">JSON 写出器。</param>
        /// <param name="roots">根节点集合。</param>
        /// <param name="depth">剩余深度。</param>
        /// <param name="includeInactive">是否包含不可见节点。</param>
        /// <param name="state">遍历状态。</param>
        public static void WriteNodes(
            RoslynJsonBuilder builder,
            IReadOnlyList<Node> roots,
            int depth,
            bool includeInactive,
            GodotSceneTraversalState state)
        {
            builder.StartArray();
            for (var index = 0; index < roots.Count; index++)
            {
                if (!WriteNode(builder, roots[index], depth, includeInactive, state))
                {
                    break;
                }
            }

            builder.EndArray();
        }

        /// <summary>判断节点当前是否"活动"（可见或未被禁用）。</summary>
        /// <param name="node">节点。</param>
        /// <returns>活动时返回 true。</returns>
        public static bool IsActive(Node node)
        {
            if (node is CanvasItem canvasItem)
            {
                return canvasItem.Visible;
            }

            if (node is Node3D node3D)
            {
                return node3D.Visible;
            }

            return node.ProcessMode != Node.ProcessModeEnum.Disabled;
        }

        /// <summary>写出单个节点。预算用尽或节点为空时停止，不补空对象。</summary>
        /// <param name="builder">JSON 写出器。</param>
        /// <param name="node">当前节点。</param>
        /// <param name="depth">剩余深度。</param>
        /// <param name="includeInactive">是否包含不可见子节点。</param>
        /// <param name="state">遍历状态。</param>
        /// <returns>写出成功时返回 true。</returns>
        private static bool WriteNode(
            RoslynJsonBuilder builder,
            Node node,
            int depth,
            bool includeInactive,
            GodotSceneTraversalState state)
        {
            if (node == null)
            {
                return false;
            }

            if (state.Remaining <= 0)
            {
                state.Truncated = true;
                return false;
            }

            state.Remaining--;
            state.Written++;
            WriteNodeHeader(builder, node);
            if (depth > 0)
            {
                WriteChildren(builder, node, depth, includeInactive, state);
            }

            builder.EndObject();
            return true;
        }

        /// <summary>写出节点头、分组和变换。不递归子节点。</summary>
        /// <param name="builder">JSON 写出器。</param>
        /// <param name="node">当前节点。</param>
        private static void WriteNodeHeader(RoslynJsonBuilder builder, Node node)
        {
            builder.StartObject()
                .Property("name", node.Name.ToString())
                .Property("path", node.GetPath().ToString())
                .Property("class", node.GetClass())
                .Property("active", IsActive(node))
                .Property("childCount", node.GetChildCount())
                .Property("scenePath", node.SceneFilePath ?? string.Empty)
                .Name("groups")
                .StartArray();
            var groups = node.GetGroups();
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                builder.String(groups[groupIndex].ToString());
            }

            builder.EndArray();
            AppendTransform(builder, node);
        }

        /// <summary>写出子节点。超过每节点上限时标记截断，但不把超限子节点写入。</summary>
        /// <param name="builder">JSON 写出器。</param>
        /// <param name="node">父节点。</param>
        /// <param name="depth">父节点剩余深度，写出时减一。</param>
        /// <param name="includeInactive">是否包含不可见子节点。</param>
        /// <param name="state">遍历状态。</param>
        private static void WriteChildren(
            RoslynJsonBuilder builder,
            Node node,
            int depth,
            bool includeInactive,
            GodotSceneTraversalState state)
        {
            builder.Name("children");
            var children = new List<Node>();
            int childCount = node.GetChildCount();
            int limit = childCount < MAX_CHILDREN_PER_NODE ? childCount : MAX_CHILDREN_PER_NODE;
            if (childCount > MAX_CHILDREN_PER_NODE)
            {
                state.Truncated = true;
            }

            for (var childIndex = 0; childIndex < limit; childIndex++)
            {
                Node child = node.GetChild(childIndex);
                if (child == null || (!includeInactive && !IsActive(child)))
                {
                    continue;
                }

                children.Add(child);
            }

            WriteNodes(builder, children, depth - 1, includeInactive, state);
        }

        /// <summary>按节点维度写出变换。既不是 Node2D 也不是 Node3D 时不写 transform 键。</summary>
        /// <param name="builder">JSON 写出器。</param>
        /// <param name="node">当前节点。</param>
        private static void AppendTransform(RoslynJsonBuilder builder, Node node)
        {
            if (node is Node3D node3D)
            {
                builder.Name("transform")
                    .StartObject()
                    .Property("kind", "3d")
                    .Name("position").StartArray().String(Format(node3D.Position.X)).String(Format(node3D.Position.Y)).String(Format(node3D.Position.Z)).EndArray()
                    .Name("rotationDegrees").StartArray().String(Format(node3D.RotationDegrees.X)).String(Format(node3D.RotationDegrees.Y)).String(Format(node3D.RotationDegrees.Z)).EndArray()
                    .Name("scale").StartArray().String(Format(node3D.Scale.X)).String(Format(node3D.Scale.Y)).String(Format(node3D.Scale.Z)).EndArray()
                    .EndObject();
                return;
            }

            if (node is Node2D node2D)
            {
                builder.Name("transform")
                    .StartObject()
                    .Property("kind", "2d")
                    .Name("position").StartArray().String(Format(node2D.Position.X)).String(Format(node2D.Position.Y)).EndArray()
                    .Property("rotationDegrees", Format(node2D.RotationDegrees))
                    .Name("scale").StartArray().String(Format(node2D.Scale.X)).String(Format(node2D.Scale.Y)).EndArray()
                    .EndObject();
            }
        }

        /// <summary>以不变文化格式化浮点，保证跨区域设置下输出稳定。</summary>
        /// <param name="value">数值。</param>
        /// <returns>文本。</returns>
        private static string Format(float value)
        {
            return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Godot 场景遍历状态。</summary>
    public sealed class GodotSceneTraversalState
    {
        /// <summary>获取或设置剩余节点预算。</summary>
        public int Remaining { get; set; } = GodotSceneQuerySupport.MAX_NODES;

        /// <summary>获取或设置已写出节点数。</summary>
        public int Written { get; set; }

        /// <summary>获取或设置是否被上限截断。</summary>
        public bool Truncated { get; set; }
    }
}
#endif
