namespace YokiFrame
{
    public static partial class ICodeScopeExtensions
    {
        /// <summary>
        /// 追加无参数特性节点，适用于紧随其后的原始声明。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="attributeName">特性类型名称。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope Attribute(this ICodeScope scope, string attributeName)
        {
            CodeScopeAccess.Add(scope, new AttributeCode(attributeName));
            return scope;
        }

        /// <summary>
        /// 追加带单个原始参数的特性节点。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="attributeName">特性类型名称。</param>
        /// <param name="argument">特性参数表达式。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope Attribute(this ICodeScope scope, string attributeName, string argument)
        {
            CodeScopeAccess.Add(scope, new AttributeCode(attributeName).WithArgument(argument));
            return scope;
        }
    }
}
