using System;

namespace YokiFrame
{
    /// <summary>
    /// CodeGenKit 静态门面，提供结构化 C# 构建、确定性渲染和事务文件提交入口。
    /// </summary>
    public static class CodeGenKit
    {
        private const int DEFAULT_INITIAL_CAPACITY = 1024;

        /// <summary>
        /// 创建可由 fluent API 独立构建的空根作用域。
        /// </summary>
        /// <returns>新的代码文件根作用域。</returns>
        public static RootCode Root()
        {
            return new RootCode();
        }

        /// <summary>
        /// 为指定 CodeGenKit 作用域创建逐行模板构建器。
        /// </summary>
        /// <param name="scope">接收模板行的作用域。</param>
        /// <returns>逐行构建器。</returns>
        public static CodeGenLineBuilder Lines(ICodeScope scope)
        {
            return new CodeGenLineBuilder(scope);
        }

        /// <summary>
        /// 构建并返回固定 LF、Tab 缩进和 invariant culture 的 C# 源码。
        /// </summary>
        /// <param name="build">根作用域构建回调。</param>
        /// <param name="initialCapacity">内部 StringBuilder 初始容量。</param>
        /// <returns>完整源码字符串。</returns>
        public static string GenerateToString(Action<RootCode> build, int initialCapacity = DEFAULT_INITIAL_CAPACITY)
        {
            RootCode root = BuildRoot(build);
            return Render(root, initialCapacity);
        }

        /// <summary>
        /// 完整构建源码后以内容比较、flush 和原子替换提交到目标文件。
        /// </summary>
        /// <param name="filePath">目标文件路径。</param>
        /// <param name="build">根作用域构建回调。</param>
        /// <returns>创建、更新或无变化结果。</returns>
        public static CodeGenerationFileResult GenerateToFile(string filePath, Action<RootCode> build)
        {
            RootCode root = BuildRoot(build);
            return WriteToFile(filePath, root);
        }

        /// <summary>
        /// 将已构建根作用域事务提交到目标文件，无变化时保持文件时间戳不变。
        /// </summary>
        /// <param name="filePath">目标文件路径。</param>
        /// <param name="root">已构建根作用域。</param>
        /// <returns>创建、更新或无变化结果。</returns>
        public static CodeGenerationFileResult WriteToFile(string filePath, RootCode root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            return CodeFileCommitter.Commit(filePath, Render(root, DEFAULT_INITIAL_CAPACITY));
        }

        /// <summary>
        /// 将已经完整生成并校验的 C# 文本按 CodeGenKit 原子提交规则写入文件。
        /// </summary>
        /// <param name="filePath">目标文件路径。</param>
        /// <param name="source">完整 C# 源码。</param>
        /// <returns>创建、更新或无变化结果。</returns>
        public static CodeGenerationFileResult WriteTextToFile(string filePath, string source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return CodeFileCommitter.Commit(filePath, source);
        }

        /// <summary>
        /// 校验单个 C# 9 标识符并返回原值，便于宿主生成器复用统一规则。
        /// </summary>
        /// <param name="value">待校验标识符。</param>
        /// <param name="parameterName">异常中使用的参数名。</param>
        /// <returns>校验通过的原始标识符。</returns>
        public static string RequireIdentifier(string value, string parameterName)
        {
            return CSharpIdentifierValidator.RequireIdentifier(value, parameterName);
        }

        /// <summary>
        /// 校验点分隔的 C# 9 限定名称并返回原值。
        /// </summary>
        /// <param name="value">待校验命名空间或限定名称。</param>
        /// <param name="parameterName">异常中使用的参数名。</param>
        /// <returns>校验通过的原始限定名称。</returns>
        public static string RequireQualifiedName(string value, string parameterName)
        {
            return CSharpIdentifierValidator.RequireQualifiedName(value, parameterName);
        }

        /// <summary>
        /// 创建根作用域并执行调用方构建回调；回调失败时不进入任何文件写入阶段。
        /// </summary>
        /// <param name="build">根作用域构建回调。</param>
        /// <returns>已完成构建的根作用域。</returns>
        private static RootCode BuildRoot(Action<RootCode> build)
        {
            if (build == null)
            {
                throw new ArgumentNullException(nameof(build));
            }

            RootCode root = new RootCode();
            build(root);
            return root;
        }

        /// <summary>
        /// 使用共享确定性 writer 渲染根作用域。
        /// </summary>
        /// <param name="root">待渲染根作用域。</param>
        /// <param name="initialCapacity">内部 StringBuilder 初始容量。</param>
        /// <returns>完整源码。</returns>
        private static string Render(RootCode root, int initialCapacity)
        {
            CodeTextWriter writer = new CodeTextWriter(initialCapacity);
            ((ICodeNode)root).Generate(writer);
            return writer.ToString();
        }
    }
}
