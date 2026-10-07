#if GODOT && TOOLS
using System.Collections.Generic;
using Godot;

namespace YokiFrame
{
    public sealed partial class GodotEditorSceneQueryOperation
    {
        /// <summary>读取当前编辑场景根。编辑器接口不可用或没有打开场景时返回错误。</summary>
        /// <param name="root">编辑中的场景根。</param>
        /// <param name="failure">不可用时的命令结果。</param>
        /// <returns>拿到根节点时返回 true。</returns>
        private static bool TryGetEditedRoot(out Node root, out YokiFrameCommandResult failure)
        {
            root = null;
            failure = null;
            try
            {
                EditorInterface editor = EditorInterface.Singleton;
                root = editor == null ? null : editor.GetEditedSceneRoot();
            }
            catch (System.Exception exception)
            {
                failure = YokiFrameCommandResult.Error(
                    RoslynErrorCodes.UNAVAILABLE,
                    "EditorInterface is not available in this process: " + exception.Message);
                return false;
            }

            if (root != null)
            {
                return true;
            }

            failure = YokiFrameCommandResult.Error(
                RoslynErrorCodes.UNAVAILABLE,
                "No scene is currently being edited.");
            return false;
        }

        /// <summary>把请求路径展开为起始节点。空路径表示整棵编辑场景。</summary>
        /// <param name="root">编辑中的场景根。</param>
        /// <param name="path">原始请求路径。</param>
        /// <param name="roots">起始节点。</param>
        /// <param name="failure">路径不存在时的命令结果。</param>
        /// <returns>至少可以开始遍历时返回 true。</returns>
        private static bool TryCollectRoots(Node root, string path, out List<Node> roots, out YokiFrameCommandResult failure)
        {
            return GodotSceneQuerySupport.TryCollectRoots(root, path, out roots, out failure);
        }

        /// <summary>写出编辑器场景头和节点数组。截断状态由共享遍历器带回。</summary>
        /// <param name="root">编辑中的场景根，用于场景头，不随请求路径改变。</param>
        /// <param name="path">原始请求路径。</param>
        /// <param name="depth">剩余深度。</param>
        /// <param name="includeInactive">是否包含不可见节点。</param>
        /// <param name="roots">起始节点。</param>
        /// <returns>成功的命令结果。</returns>
        private static YokiFrameCommandResult WriteQuery(
            Node root,
            string path,
            int depth,
            bool includeInactive,
            List<Node> roots)
        {
            return GodotSceneQuerySupport.WriteQuery(ACTION, "editor", root, path, depth, includeInactive, roots);
        }
    }
}
#endif
