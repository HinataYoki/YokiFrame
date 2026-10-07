#if GODOT && TOOLS
using System.Collections.Generic;
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// scene_query（godot-runtime）：读取运行中场景树的有界快照。
    /// </summary>
    public sealed partial class GodotRuntimeSceneQueryOperation : IRoslynOperation
    {
        internal const string ACTION = "scene_query";

        private readonly Node mOwner;

        /// <summary>创建 runtime scene_query 操作。</summary>
        /// <param name="owner">用于获取 SceneTree 的节点。</param>
        public GodotRuntimeSceneQueryOperation(Node owner)
        {
            mOwner = owner;
            Descriptor = new RoslynOperationDescriptor(
                ACTION,
                YokiFrameCommandKind.ReadOnly,
                isDiagnostic: false,
                isCancellation: false,
                targets: RoslynExecutionTarget.Runtime);
        }

        /// <summary>获取操作描述。</summary>
        public RoslynOperationDescriptor Descriptor { get; }

        /// <summary>执行查询。场景树不可用或路径不存在时失败，不写出部分快照。</summary>
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

            Node root = ResolveSceneRoot();
            if (root == null)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.UNAVAILABLE,
                    "SceneTree root is not available for scene_query.");
            }

            if (!TryCollectRoots(root, path, out List<Node> roots, out YokiFrameCommandResult pathError))
            {
                return pathError;
            }

            return WriteQuery(root, path, depth, includeInactive, roots);
        }

        /// <summary>从构造时传入的节点取场景树根。节点或树已失效时返回 null，不抛出。</summary>
        /// <returns>运行中的场景根；不可用时返回 null。</returns>
        private Node ResolveSceneRoot()
        {
            SceneTree tree = mOwner == null ? null : mOwner.GetTree();
            return tree == null ? null : tree.Root;
        }
    }
}
#endif
