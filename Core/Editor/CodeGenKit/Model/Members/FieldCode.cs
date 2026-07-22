using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 表示字段声明及其访问级别、修饰符、默认值、特性和 XML summary。
    /// </summary>
    public sealed class FieldCode : ICodeNode
    {
        private readonly string mTypeName;
        private readonly string mFieldName;
        private readonly List<AttributeCode> mAttributes = new List<AttributeCode>();
        private string mDefaultValue;
        private string mComment;
        private AccessModifier mAccess = AccessModifier.Private;
        private MemberModifier mModifiers = MemberModifier.None;

        /// <summary>
        /// 创建字段 builder；字段名严格校验，类型保留为调用方负责的单行表达式。
        /// </summary>
        /// <param name="typeName">字段类型表达式。</param>
        /// <param name="fieldName">字段名称。</param>
        public FieldCode(string typeName, string fieldName)
        {
            mTypeName = CSharpText.RequireNonEmptyLine(typeName, nameof(typeName));
            mFieldName = CSharpIdentifierValidator.RequireIdentifier(fieldName, nameof(fieldName));
        }

        /// <summary>
        /// 设置字段访问级别。
        /// </summary>
        /// <param name="access">目标访问级别。</param>
        /// <returns>当前字段 builder。</returns>
        public FieldCode WithAccess(AccessModifier access)
        {
            CodeModifierText.GetAccessText(access);
            mAccess = access;
            return this;
        }

        /// <summary>
        /// 设置字段修饰符；具体组合在生成前按字段规则校验。
        /// </summary>
        /// <param name="modifiers">字段修饰符组合。</param>
        /// <returns>当前字段 builder。</returns>
        public FieldCode WithModifiers(MemberModifier modifiers)
        {
            mModifiers = modifiers;
            return this;
        }

        /// <summary>
        /// 设置调用方负责语义的单行默认值表达式。
        /// </summary>
        /// <param name="defaultValue">等号右侧表达式。</param>
        /// <returns>当前字段 builder。</returns>
        public FieldCode WithDefaultValue(string defaultValue)
        {
            mDefaultValue = CSharpText.RequireNonEmptyLine(defaultValue, nameof(defaultValue));
            return this;
        }

        /// <summary>
        /// 设置字段 XML summary；输出时统一执行 XML 转义。
        /// </summary>
        /// <param name="comment">字段说明。</param>
        /// <returns>当前字段 builder。</returns>
        public FieldCode WithComment(string comment)
        {
            mComment = comment;
            return this;
        }

        /// <summary>
        /// 为字段追加无参数特性。
        /// </summary>
        /// <param name="attributeName">特性类型名称。</param>
        /// <returns>当前字段 builder。</returns>
        public FieldCode WithAttribute(string attributeName)
        {
            mAttributes.Add(new AttributeCode(attributeName));
            return this;
        }

        /// <summary>
        /// 为字段追加带单个原始参数的特性。
        /// </summary>
        /// <param name="attributeName">特性类型名称。</param>
        /// <param name="argument">特性参数表达式。</param>
        /// <returns>当前字段 builder。</returns>
        public FieldCode WithAttribute(string attributeName, string argument)
        {
            mAttributes.Add(new AttributeCode(attributeName).WithArgument(argument));
            return this;
        }

        /// <summary>
        /// 校验修饰符后按 summary、特性、字段声明的顺序渲染节点。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        void ICodeNode.Generate(CodeTextWriter writer)
        {
            CodeModifierValidator.ValidateField(mModifiers);
            XmlDocumentationWriter.WriteSummary(writer, mComment);
            for (var index = 0; index < mAttributes.Count; index++)
            {
                mAttributes[index].Generate(writer);
            }

            string defaultText = mDefaultValue == null ? string.Empty : " = " + mDefaultValue;
            writer.WriteLine(CodeModifierText.GetAccessText(mAccess)
                + CodeModifierText.GetMemberText(mModifiers)
                + mTypeName + " " + mFieldName + defaultText + ";");
        }
    }
}
