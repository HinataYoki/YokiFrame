#if GODOT && TOOLS
using System.Collections.Generic;
using Godot;

namespace YokiFrame
{
    public sealed partial class GodotRuntimeSceneQueryOperation
    {
        /// <summary>把请求路径展开为起始节点。空路径表示整棵运行场景。</summary>
        /// <param name="root">运行中的场景根。</param>
        /// <param name="path">原始请求路径。</param>
        /// <param name="roots">起始节点。</param>
        /// <param name="failure">路径不存在时的命令结果。</param>
        /// <returns>至少可以开始遍历时返回 true。</returns>
        private static bool TryCollectRoots(Node root, string path, out List<Node> roots, out YokiFrameCommandResult failure)
        {
            return GodotSceneQuerySupport.TryCollectRoots(root, path, out roots, out failure);
        }

        /// <summary>写出运行时场景头和节点数组。截断状态由共享遍历器带回。</summary>
        /// <param name="root">运行中的场景根，用于场景头，不随请求路径改变。</param>
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
            return GodotSceneQuerySupport.WriteQuery(ACTION, "runtime", root, path, depth, includeInactive, roots);
        }
    }
}
#endif
