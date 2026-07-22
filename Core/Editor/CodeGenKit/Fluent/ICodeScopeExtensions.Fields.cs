using System;

namespace YokiFrame
{
    public static partial class ICodeScopeExtensions
    {
        /// <summary>
        /// 追加可完整配置的字段声明。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="typeName">字段类型表达式。</param>
        /// <param name="fieldName">字段名称。</param>
        /// <param name="configure">可选字段配置回调。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope Field(
            this ICodeScope scope,
            string typeName,
            string fieldName,
            Action<FieldCode> configure = null)
        {
            FieldCode field = new FieldCode(typeName, fieldName);
            configure?.Invoke(field);
            CodeScopeAccess.Add(scope, field);
            return scope;
        }

        /// <summary>
        /// 追加 public 字段并可配置 XML summary。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="typeName">字段类型表达式。</param>
        /// <param name="fieldName">字段名称。</param>
        /// <param name="comment">可选字段说明。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope PublicField(
            this ICodeScope scope,
            string typeName,
            string fieldName,
            string comment = null)
        {
            return scope.Field(typeName, fieldName, field =>
            {
                field.WithAccess(AccessModifier.Public);
                if (!string.IsNullOrEmpty(comment))
                {
                    field.WithComment(comment);
                }
            });
        }

        /// <summary>
        /// 追加 private 字段并可配置默认值表达式。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="typeName">字段类型表达式。</param>
        /// <param name="fieldName">字段名称。</param>
        /// <param name="defaultValue">可选默认值表达式。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope PrivateField(
            this ICodeScope scope,
            string typeName,
            string fieldName,
            string defaultValue = null)
        {
            return scope.Field(typeName, fieldName, field =>
            {
                field.WithAccess(AccessModifier.Private);
                if (!string.IsNullOrEmpty(defaultValue))
                {
                    field.WithDefaultValue(defaultValue);
                }
            });
        }

        /// <summary>
        /// 追加带 SerializeField 特性的 private 字段；该入口只生成文本，不依赖 UnityEngine。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="typeName">字段类型表达式。</param>
        /// <param name="fieldName">字段名称。</param>
        /// <param name="comment">可选字段说明。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope SerializeField(
            this ICodeScope scope,
            string typeName,
            string fieldName,
            string comment = null)
        {
            return scope.Field(typeName, fieldName, field =>
            {
                field.WithAccess(AccessModifier.Private).WithAttribute("SerializeField");
                if (!string.IsNullOrEmpty(comment))
                {
                    field.WithComment(comment);
                }
            });
        }
    }
}
