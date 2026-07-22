using System;

namespace YokiFrame
{
    public static partial class ICodeScopeExtensions
    {
        /// <summary>
        /// 追加块级 namespace，并在插入前完成全部子节点构建。
        /// </summary>
        /// <param name="scope">父作用域。</param>
        /// <param name="namespaceName">命名空间限定名称。</param>
        /// <param name="build">命名空间内容构建回调。</param>
        /// <returns>父作用域。</returns>
        public static ICodeScope Namespace(
            this ICodeScope scope,
            string namespaceName,
            Action<NamespaceCodeScope> build)
        {
            NamespaceCodeScope namespaceScope = new NamespaceCodeScope(namespaceName);
            build?.Invoke(namespaceScope);
            CodeScopeAccess.Add(scope, namespaceScope);
            return scope;
        }

        /// <summary>
        /// 使用默认 public、非 partial、非 static 配置追加类作用域。
        /// </summary>
        /// <param name="scope">父作用域。</param>
        /// <param name="className">类名称。</param>
        /// <param name="build">类配置和成员构建回调。</param>
        /// <returns>父作用域。</returns>
        public static ICodeScope Class(
            this ICodeScope scope,
            string className,
            Action<ClassCodeScope> build)
        {
            return Class(scope, className, null, false, false, build);
        }

        /// <summary>
        /// 追加可指定父类型、partial 和 static 的类作用域。
        /// </summary>
        /// <param name="scope">父作用域。</param>
        /// <param name="className">类名称。</param>
        /// <param name="parentClassName">可选父类型表达式。</param>
        /// <param name="isPartial">是否生成 partial。</param>
        /// <param name="isStatic">是否生成 static。</param>
        /// <param name="build">类配置和成员构建回调。</param>
        /// <returns>父作用域。</returns>
        public static ICodeScope Class(
            this ICodeScope scope,
            string className,
            string parentClassName,
            bool isPartial,
            bool isStatic,
            Action<ClassCodeScope> build)
        {
            ClassCodeScope classScope = new ClassCodeScope(className, parentClassName, isPartial, isStatic);
            build?.Invoke(classScope);
            CodeScopeAccess.Add(scope, classScope);
            return scope;
        }

        /// <summary>
        /// 追加具有调用方声明头的通用块级作用域。
        /// </summary>
        /// <param name="scope">父作用域。</param>
        /// <param name="firstLine">花括号前的单行声明头。</param>
        /// <param name="semicolon">闭合花括号后是否追加分号。</param>
        /// <param name="build">作用域内容构建回调。</param>
        /// <returns>父作用域。</returns>
        public static ICodeScope CustomScope(
            this ICodeScope scope,
            string firstLine,
            bool semicolon,
            Action<CustomCodeScope> build)
        {
            CustomCodeScope customScope = new CustomCodeScope(firstLine, semicolon);
            build?.Invoke(customScope);
            CodeScopeAccess.Add(scope, customScope);
            return scope;
        }
    }
}
