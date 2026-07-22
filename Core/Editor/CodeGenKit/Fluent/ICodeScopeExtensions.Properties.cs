using System;

namespace YokiFrame
{
    public static partial class ICodeScopeExtensions
    {
        /// <summary>
        /// 追加可完整配置的属性声明。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="typeName">属性类型表达式。</param>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="configure">可选属性配置回调。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope Property(
            this ICodeScope scope,
            string typeName,
            string propertyName,
            Action<PropertyCode> configure = null)
        {
            PropertyCode property = new PropertyCode(typeName, propertyName);
            configure?.Invoke(property);
            CodeScopeAccess.Add(scope, property);
            return scope;
        }

        /// <summary>
        /// 追加只有 getter 的表达式属性。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="typeName">属性类型表达式。</param>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="expression">getter 表达式。</param>
        /// <param name="comment">可选属性说明。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope ReadonlyProperty(
            this ICodeScope scope,
            string typeName,
            string propertyName,
            string expression,
            string comment = null)
        {
            return scope.Property(typeName, propertyName, property =>
            {
                property.WithExpressionBody(expression);
                if (!string.IsNullOrEmpty(comment))
                {
                    property.WithComment(comment);
                }
            });
        }

        /// <summary>
        /// 追加自动属性，并可选择是否生成 setter。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="typeName">属性类型表达式。</param>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="hasSetter">是否生成 setter。</param>
        /// <param name="comment">可选属性说明。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope AutoProperty(
            this ICodeScope scope,
            string typeName,
            string propertyName,
            bool hasSetter = true,
            string comment = null)
        {
            return scope.Property(typeName, propertyName, property =>
            {
                if (hasSetter) property.AsAutoProperty();
                else property.AsReadonly();
                if (!string.IsNullOrEmpty(comment)) property.WithComment(comment);
            });
        }
    }
}
