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
    public sealed partial class GodotEditorSceneQueryOperation : IRoslynOperation
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

        /// <summary>执行查询。选项非法或没有编辑中的场景时直接失败，不写出部分快照。</summary>
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

            if (!TryGetEditedRoot(out Node root, out YokiFrameCommandResult unavailable))
            {
                return unavailable;
            }

            if (!TryCollectRoots(root, path, out List<Node> roots, out YokiFrameCommandResult pathError))
            {
                return pathError;
            }

            return WriteQuery(root, path, depth, includeInactive, roots);
        }
    }
}
#endif
