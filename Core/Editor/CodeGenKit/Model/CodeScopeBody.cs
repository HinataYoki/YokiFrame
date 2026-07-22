using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 保存作用域内部节点顺序，并提供唯一的节点遍历实现。
    /// </summary>
    internal sealed class CodeScopeBody
    {
        private readonly List<ICodeNode> mNodes = new List<ICodeNode>();

        /// <summary>
        /// 将节点追加到当前作用域尾部，保留调用方定义的源码顺序。
        /// </summary>
        /// <param name="node">待追加节点。</param>
        internal void Add(ICodeNode node)
        {
            mNodes.Add(node);
        }

        /// <summary>
        /// 依次渲染全部节点；任一节点失败时立即停止，避免输出伪完整源码。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        internal void Generate(CodeTextWriter writer)
        {
            for (var index = 0; index < mNodes.Count; index++)
            {
                mNodes[index].Generate(writer);
            }
        }
    }
}
