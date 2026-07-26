using System;

namespace YokiFrame
{
    /// <summary>
    /// 表示可以按顺序接收代码节点的结构化生成作用域。
    /// </summary>
    public interface ICodeScope
    {
    }

    /// <summary>
    /// 定义共享 Editor 程序集内部的可渲染代码节点契约。
    /// </summary>
    internal interface ICodeNode
    {
        /// <summary>
        /// 将当前节点写入固定格式的源码 writer。
        /// </summary>
        /// <param name="writer">接收生成源码的 writer。</param>
        void Generate(CodeTextWriter writer);
    }

    /// <summary>
    /// 定义共享 Editor 程序集内部向作用域追加节点的受控边界。
    /// </summary>
    internal interface ICodeContainer : ICodeScope
    {
        /// <summary>
        /// 按调用顺序把节点追加到当前作用域。
        /// </summary>
        /// <param name="node">待追加的非空节点。</param>
        void Add(ICodeNode node);
    }

    /// <summary>
    /// 集中校验公开 ICodeScope 是否来自 CodeGenKit，避免公开可变节点集合。
    /// </summary>
    internal static class CodeScopeAccess
    {
        /// <summary>
        /// 向受支持的 CodeGenKit 作用域追加节点；外部伪造作用域时给出明确错误。
        /// </summary>
        /// <param name="scope">目标公开作用域。</param>
        /// <param name="node">待追加的内部节点。</param>
        internal static void Add(ICodeScope scope, ICodeNode node)
        {
            RequireContainer(scope).Add(node ?? throw new ArgumentNullException(nameof(node)));
        }

        /// <summary>
        /// 获取受支持的内部容器，供逐行构建器保持节点插入顺序。
        /// </summary>
        /// <param name="scope">目标公开作用域。</param>
        /// <returns>经过校验的内部容器。</returns>
        internal static ICodeContainer RequireContainer(ICodeScope scope)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (scope is ICodeContainer container)
            {
                return container;
            }

            throw new ArgumentException("作用域不是由 CodeGenKit 创建的受支持作用域。", nameof(scope));
        }
    }
}
