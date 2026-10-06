#if GODOT && TOOLS
using System.Collections.Generic;
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// scene_query（godot-runtime）：读取运行中场景树的有界快照。
    /// </summary>
    public sealed class GodotRuntimeSceneQueryOperation : IYokiFrameEngineOperation
    {
        internal const string ACTION = "scene_query";

        private readonly Node mOwner;

        /// <summary>创建 runtime scene_query 操作。</summary>
        /// <param name="owner">用于获取 SceneTree 的节点。</param>
        public GodotRuntimeSceneQueryOperation(Node owner)
        {
            mOwner = owner;
            Descriptor = new YokiFrameEngineOperationDescriptor(
                ACTION,
                YokiFrameCommandKind.ReadOnly,
                isDiagnostic: false,
                isCancellation: false,
                targets: YokiFrameEngineExecutionTarget.Runtime);
        }

        /// <summary>获取操作描述。</summary>
        public YokiFrameEngineOperationDescriptor Descriptor { get; }

        /// <summary>执行查询。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令结果。</returns>
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            if (!GodotSceneQuerySupport.TryReadOptions(
                    request.PayloadJson,
                    out string path,
                    out int depth,
                    out bool includeInactive,
                    out string error))
            {
                return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, error);
            }

            SceneTree tree = mOwner == null ? null : mOwner.GetTree();
            Node root = tree == null ? null : tree.Root;
            if (root == null)
            {
                return YokiFrameCommandResult.Error(
                    YokiFrameEngineErrorCodes.UNAVAILABLE,
                    "SceneTree root is not available for scene_query.");
            }

            var roots = new List<Node>();
            if (string.IsNullOrEmpty(path))
            {
                roots.Add(root);
            }
            else
            {
                Node target = GodotSceneQuerySupport.FindByPath(root, path);
                if (target == null)
                {
                    return YokiFrameCommandResult.Error(
                        YokiFrameEngineErrorCodes.INVALID_PAYLOAD,
                        "scene_query path was not found: " + path + ".");
                }

                roots.Add(target);
            }

            var state = new GodotSceneTraversalState();
            YokiFrameEngineJsonBuilder builder = new YokiFrameEngineJsonBuilder()
                .StartObject()
                .Property("operation", ACTION)
                .Property("host", "runtime")
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
            GodotSceneQuerySupport.WriteNodes(builder, roots, depth, includeInactive, state);
            return YokiFrameCommandResult.Success(builder
                .Property("nodeCount", state.Written)
                .Property("truncated", state.Truncated)
                .EndObject()
                .ToString());
        }
    }
}
#endif
