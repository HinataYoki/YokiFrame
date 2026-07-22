using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 表示自动、表达式或显式访问器属性声明。
    /// </summary>
    public sealed class PropertyCode : ICodeNode
    {
        private readonly string mTypeName;
        private readonly string mPropertyName;
        private readonly List<AttributeCode> mAttributes = new List<AttributeCode>();
        private AccessModifier mAccess = AccessModifier.Public;
        private AccessModifier mSetterAccess = AccessModifier.None;
        private MemberModifier mModifiers = MemberModifier.None;
        private string mComment;
        private string mGetterExpression;
        private Action<ICodeScope> mGetterBody;
        private Action<ICodeScope> mSetterBody;
        private bool mHasGetter = true;
        private bool mHasSetter;
        private PropertyBodyKind mBodyKind = PropertyBodyKind.Auto;

        /// <summary>
        /// 创建属性 builder；属性名严格校验，类型保留为调用方负责的单行表达式。
        /// </summary>
        /// <param name="typeName">属性类型表达式。</param>
        /// <param name="propertyName">属性名称。</param>
        public PropertyCode(string typeName, string propertyName)
        {
            mTypeName = CSharpText.RequireNonEmptyLine(typeName, nameof(typeName));
            mPropertyName = CSharpIdentifierValidator.RequireIdentifier(propertyName, nameof(propertyName));
        }

        /// <summary>
        /// 设置属性访问级别。
        /// </summary>
        /// <param name="access">目标访问级别。</param>
        /// <returns>当前属性 builder。</returns>
        public PropertyCode WithAccess(AccessModifier access)
        {
            CodeModifierText.GetAccessText(access);
            mAccess = access;
            return this;
        }

        /// <summary>
        /// 设置属性修饰符；具体组合在生成前按属性规则校验。
        /// </summary>
        /// <param name="modifiers">属性修饰符组合。</param>
        /// <returns>当前属性 builder。</returns>
        public PropertyCode WithModifiers(MemberModifier modifiers)
        {
            mModifiers = modifiers;
            return this;
        }

        /// <summary>
        /// 设置属性 XML summary。
        /// </summary>
        /// <param name="comment">属性说明。</param>
        /// <returns>当前属性 builder。</returns>
        public PropertyCode WithComment(string comment)
        {
            mComment = comment;
            return this;
        }

        /// <summary>
        /// 为属性追加无参数特性。
        /// </summary>
        /// <param name="attributeName">特性类型名称。</param>
        /// <returns>当前属性 builder。</returns>
        public PropertyCode WithAttribute(string attributeName)
        {
            mAttributes.Add(new AttributeCode(attributeName));
            return this;
        }

        /// <summary>
        /// 将属性重置为只有自动 getter 的只读属性。
        /// </summary>
        /// <returns>当前属性 builder。</returns>
        public PropertyCode AsReadonly()
        {
            ResetBody(PropertyBodyKind.Auto);
            mHasGetter = true;
            mHasSetter = false;
            return this;
        }

        /// <summary>
        /// 将属性重置为自动属性，并可指定更严格的 setter 访问级别。
        /// </summary>
        /// <param name="setterAccess">setter 访问级别；None 表示与属性一致。</param>
        /// <returns>当前属性 builder。</returns>
        public PropertyCode AsAutoProperty(AccessModifier setterAccess = AccessModifier.None)
        {
            CodeModifierText.GetAccessText(setterAccess);
            ResetBody(PropertyBodyKind.Auto);
            mHasGetter = true;
            mHasSetter = true;
            mSetterAccess = setterAccess;
            return this;
        }

        /// <summary>
        /// 将属性重置为只有 getter 的表达式属性。
        /// </summary>
        /// <param name="expression">箭头右侧单行表达式。</param>
        /// <returns>当前属性 builder。</returns>
        public PropertyCode WithExpressionBody(string expression)
        {
            ResetBody(PropertyBodyKind.Expression);
            mGetterExpression = CSharpText.RequireNonEmptyLine(expression, nameof(expression));
            mHasGetter = true;
            mHasSetter = false;
            return this;
        }

        /// <summary>
        /// 配置显式 getter 作用域；该调用会清除之前的表达式 getter。
        /// </summary>
        /// <param name="getterBody">getter 内容构建回调。</param>
        /// <returns>当前属性 builder。</returns>
        public PropertyCode WithGetter(Action<ICodeScope> getterBody)
        {
            mBodyKind = PropertyBodyKind.Accessors;
            mGetterExpression = null;
            mGetterBody = getterBody;
            mHasGetter = true;
            return this;
        }

        /// <summary>
        /// 配置显式 setter；表达式属性必须先显式切换 getter，避免旧版顺序导致语义丢失。
        /// </summary>
        /// <param name="setterBody">setter 内容构建回调。</param>
        /// <param name="access">可选 setter 访问级别。</param>
        /// <returns>当前属性 builder。</returns>
        public PropertyCode WithSetter(Action<ICodeScope> setterBody, AccessModifier access = AccessModifier.None)
        {
            if (mBodyKind == PropertyBodyKind.Expression)
            {
                throw new InvalidOperationException("表达式属性不能直接追加 setter，请先调用 WithGetter 切换为访问器属性。");
            }

            CodeModifierText.GetAccessText(access);
            mBodyKind = PropertyBodyKind.Accessors;
            mSetterBody = setterBody;
            mSetterAccess = access;
            mHasSetter = true;
            return this;
        }

        /// <summary>
        /// 校验属性状态与修饰符后渲染对应属性形态。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        void ICodeNode.Generate(CodeTextWriter writer)
        {
            CodeModifierValidator.ValidateProperty(mModifiers);
            ValidateAbstractBody();
            XmlDocumentationWriter.WriteSummary(writer, mComment);
            GenerateAttributes(writer);
            string header = CodeModifierText.GetAccessText(mAccess)
                + CodeModifierText.GetMemberText(mModifiers) + mTypeName + " " + mPropertyName;
            if (mBodyKind == PropertyBodyKind.Expression)
            {
                writer.WriteLine(header + " => " + mGetterExpression + ";");
                return;
            }

            if (mBodyKind == PropertyBodyKind.Auto)
            {
                writer.WriteLine(header + " { " + BuildAutoAccessors() + "}");
                return;
            }

            GenerateExplicitAccessors(writer, header);
        }

        /// <summary>
        /// 清除其它属性形态的残留状态，保证配置顺序不会静默复用旧 body。
        /// </summary>
        /// <param name="bodyKind">切换后的属性形态。</param>
        private void ResetBody(PropertyBodyKind bodyKind)
        {
            mBodyKind = bodyKind;
            mGetterExpression = null;
            mGetterBody = null;
            mSetterBody = null;
            mSetterAccess = AccessModifier.None;
        }

        /// <summary>
        /// 抽象属性只能使用无方法体访问器，拒绝表达式或显式 body。
        /// </summary>
        private void ValidateAbstractBody()
        {
            if ((mModifiers & MemberModifier.Abstract) != 0 && mBodyKind != PropertyBodyKind.Auto)
            {
                throw new InvalidOperationException("abstract 属性只能使用自动访问器声明。");
            }
        }

        /// <summary>
        /// 按调用顺序渲染属性特性。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        private void GenerateAttributes(CodeTextWriter writer)
        {
            for (var index = 0; index < mAttributes.Count; index++)
            {
                mAttributes[index].Generate(writer);
            }
        }

        /// <summary>
        /// 构造单行自动 getter/setter 文本。
        /// </summary>
        /// <returns>包含必要尾随空格的访问器文本。</returns>
        private string BuildAutoAccessors()
        {
            string getter = mHasGetter ? "get; " : string.Empty;
            string setter = mHasSetter ? CodeModifierText.GetAccessText(mSetterAccess) + "set; " : string.Empty;
            return getter + setter;
        }

        /// <summary>
        /// 渲染具有独立花括号的 getter/setter 访问器。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        /// <param name="header">完整属性声明头。</param>
        private void GenerateExplicitAccessors(CodeTextWriter writer, string header)
        {
            writer.WriteLine(header);
            writer.WriteLine("{");
            writer.PushIndent();
            try
            {
                GenerateAccessor(writer, "get", AccessModifier.None, mHasGetter, mGetterBody);
                GenerateAccessor(writer, "set", mSetterAccess, mHasSetter, mSetterBody);
            }
            finally
            {
                writer.PopIndent();
            }

            writer.WriteLine("}");
        }

        /// <summary>
        /// 渲染单个自动或带 body 的访问器。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        /// <param name="name">get 或 set。</param>
        /// <param name="access">访问器访问级别。</param>
        /// <param name="enabled">是否生成该访问器。</param>
        /// <param name="body">可选访问器 body。</param>
        private static void GenerateAccessor(
            CodeTextWriter writer,
            string name,
            AccessModifier access,
            bool enabled,
            Action<ICodeScope> body)
        {
            if (!enabled)
            {
                return;
            }

            string firstLine = CodeModifierText.GetAccessText(access) + name;
            if (body == null)
            {
                writer.WriteLine(firstLine + ";");
                return;
            }

            CustomCodeScope scope = new CustomCodeScope(firstLine, false);
            body(scope);
            ((ICodeNode)scope).Generate(writer);
        }

        /// <summary>
        /// 区分自动、表达式和显式访问器属性，避免用多个 bool 推断隐含状态。
        /// </summary>
        private enum PropertyBodyKind
        {
            Auto,
            Expression,
            Accessors
        }
    }
}
