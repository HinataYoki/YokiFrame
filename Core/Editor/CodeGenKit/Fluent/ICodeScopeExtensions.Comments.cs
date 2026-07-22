using System;

namespace YokiFrame
{
    public static partial class ICodeScopeExtensions
    {
        /// <summary>
        /// 追加普通注释；多行文本会拆成多个具有正确缩进的注释行。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="content">注释正文。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope Comment(this ICodeScope scope, string content)
        {
            CodeScopeAccess.Add(scope, new CommentCode(content, CommentType.SingleLine));
            return scope;
        }

        /// <summary>
        /// 追加经过 XML 转义的 summary 文档节点。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="content">summary 正文。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope Summary(this ICodeScope scope, string content)
        {
            CodeScopeAccess.Add(scope, new CommentCode(content, CommentType.XmlSummary));
            return scope;
        }

        /// <summary>
        /// 追加经过名称校验和 XML 转义的 param 文档节点。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="parameterName">参数名称。</param>
        /// <param name="description">参数说明。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope Param(this ICodeScope scope, string parameterName, string description)
        {
            CodeScopeAccess.Add(scope, new CommentCode(description, CommentType.XmlParam, parameterName));
            return scope;
        }

        /// <summary>
        /// 追加经过 XML 转义的 returns 文档节点。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="description">返回值说明。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope Returns(this ICodeScope scope, string description)
        {
            CodeScopeAccess.Add(scope, new CommentCode(description, CommentType.XmlReturns));
            return scope;
        }

        /// <summary>
        /// 追加带前后空行的 region 块，内容仍由当前父作用域按顺序持有。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="regionName">region 显示名称，必须保持单行。</param>
        /// <param name="build">region 内容构建回调。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope Region(
            this ICodeScope scope,
            string regionName,
            Action<ICodeScope> build)
        {
            string validName = CSharpText.RequireNonEmptyLine(regionName, nameof(regionName));
            scope.Custom("#region " + validName).EmptyLine();
            build?.Invoke(scope);
            scope.EmptyLine().Custom("#endregion");
            return scope;
        }
    }
}
