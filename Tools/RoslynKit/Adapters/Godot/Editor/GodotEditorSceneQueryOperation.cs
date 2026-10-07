#if GODOT && TOOLS
using System.Collections.Generic;
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// scene_query（godot-editor）：读取当前编辑场景的有界快照。
    /// </summary>
    /// <remarks>
    /// payload 与 Unity、godot-runtime 完全一致（path/depth/includeInactive），
    /// 场景来源是 EditorInterface 的编辑中场景根。
    /// </remarks>
    public sealed class GodotEditorSceneQueryOperation : IRoslynOperation
    {
        internal const string ACTION = "scene_query";

        /// <summary>创建编辑器 scene_query 操作。</summary>
        public GodotEditorSceneQueryOperation()
        {
            Descriptor = new RoslynOperationDescriptor(
                ACTION,
                YokiFrameCommandKind.ReadOnly,
                isDiagnostic: false,
                isCancellation: false,
                targets: RoslynExecutionTarget.Editor);
        }

        /// <summary>获取操作描述。</summary>
        public RoslynOperationDescriptor Descriptor { get; }

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
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, error);
            }

            Node root;
            try
            {
                EditorInterface editor = EditorInterface.Singleton;
                root = editor == null ? null : editor.GetEditedSceneRoot();
            }
            catch (System.Exception exception)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.UNAVAILABLE,
                    "EditorInterface is not available in this process: " + exception.Message);
            }

            if (root == null)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.UNAVAILABLE,
                    "No scene is currently being edited.");
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
                        RoslynErrorCodes.INVALID_PAYLOAD,
                        "scene_query path was not found: " + path + ".");
                }

                roots.Add(target);
            }

            var state = new GodotSceneTraversalState();
            RoslynJsonBuilder builder = new RoslynJsonBuilder()
                .StartObject()
                .Property("operation", ACTION)
                .Property("host", "editor")
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
