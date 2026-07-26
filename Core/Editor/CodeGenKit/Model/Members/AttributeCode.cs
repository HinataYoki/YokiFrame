using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 表示结构化声明上的单个 C# 特性及其原始单行参数。
    /// </summary>
    internal sealed class AttributeCode : ICodeNode
    {
        private readonly string mAttributeName;
        private readonly List<string> mArguments = new List<string>();

        /// <summary>
        /// 创建经过限定名称校验的特性节点。
        /// </summary>
        /// <param name="attributeName">特性类型名称，可包含命名空间。</param>
        internal AttributeCode(string attributeName)
        {
            mAttributeName = CSharpIdentifierValidator.RequireQualifiedName(attributeName, nameof(attributeName));
        }

        /// <summary>
        /// 追加调用方负责语义的原始特性参数，并约束其只能占一行。
        /// </summary>
        /// <param name="argument">特性参数表达式。</param>
        /// <returns>当前特性节点。</returns>
        internal AttributeCode WithArgument(string argument)
        {
            mArguments.Add(CSharpText.RequireNonEmptyLine(argument, nameof(argument)));
            return this;
        }

        /// <summary>
        /// 渲染无参数或带参数的单行特性声明。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        public void Generate(CodeTextWriter writer)
        {
            string arguments = mArguments.Count == 0 ? string.Empty : "(" + string.Join(", ", mArguments) + ")";
            writer.WriteLine("[" + mAttributeName + arguments + "]");
        }
    }
}
