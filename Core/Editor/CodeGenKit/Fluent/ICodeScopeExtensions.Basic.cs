namespace YokiFrame
{
    /// <summary>
    /// 提供 CodeGenKit 作用域的 fluent 构建入口。
    /// </summary>
    public static partial class ICodeScopeExtensions
    {
        /// <summary>
        /// 追加经过限定名称校验的 using 指令。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="namespaceName">要导入的命名空间。</param>
        /// <returns>原作用域，便于继续链式调用。</returns>
        public static ICodeScope Using(this ICodeScope scope, string namespaceName)
        {
            CodeScopeAccess.Add(scope, new UsingCode(namespaceName));
            return scope;
        }

        /// <summary>
        /// 追加一个不带缩进字符的空行。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope EmptyLine(this ICodeScope scope)
        {
            CodeScopeAccess.Add(scope, new TextLineCode(string.Empty));
            return scope;
        }

        /// <summary>
        /// 追加调用方负责语义的单行原始 C#，用于结构化模型未覆盖的表达式或指令。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="line">单行原始源码。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope Custom(this ICodeScope scope, string line)
        {
            CodeScopeAccess.Add(scope, new TextLineCode(line));
            return scope;
        }
    }
}
