using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 表示类声明及其特性、继承关系和成员作用域。
    /// </summary>
    public sealed class ClassCodeScope : ICodeContainer, ICodeNode
    {
        private readonly string mClassName;
        private readonly string mParentClassName;
        private readonly bool mIsPartial;
        private readonly bool mIsStatic;
        private readonly List<string> mInterfaces = new List<string>();
        private readonly List<AttributeCode> mAttributes = new List<AttributeCode>();
        private readonly CodeScopeBody mBody = new CodeScopeBody();
        private AccessModifier mAccess = AccessModifier.Public;
        private bool mIsSealed;

        /// <summary>
        /// 创建类声明；类型名严格校验，父类型保留为调用方负责的单行类型表达式。
        /// </summary>
        /// <param name="className">类名称。</param>
        /// <param name="parentClassName">可选父类型表达式。</param>
        /// <param name="isPartial">是否生成 partial。</param>
        /// <param name="isStatic">是否生成 static。</param>
        internal ClassCodeScope(string className, string parentClassName, bool isPartial, bool isStatic)
        {
            mClassName = CSharpIdentifierValidator.RequireIdentifier(className, nameof(className));
            mParentClassName = string.IsNullOrEmpty(parentClassName)
                ? null
                : CSharpText.RequireNonEmptyLine(parentClassName, nameof(parentClassName));
            mIsPartial = isPartial;
            mIsStatic = isStatic;
        }

        /// <summary>
        /// 设置类访问级别；未知枚举值会立即失败。
        /// </summary>
        /// <param name="access">目标访问级别。</param>
        /// <returns>当前类 builder。</returns>
        public ClassCodeScope WithAccess(AccessModifier access)
        {
            CodeModifierText.GetAccessText(access);
            mAccess = access;
            return this;
        }

        /// <summary>
        /// 将非静态类标记为 sealed。
        /// </summary>
        /// <returns>当前类 builder。</returns>
        public ClassCodeScope AsSealed()
        {
            mIsSealed = true;
            return this;
        }

        /// <summary>
        /// 追加接口类型表达式，调用顺序决定继承列表顺序。
        /// </summary>
        /// <param name="interfaceName">接口类型表达式。</param>
        /// <returns>当前类 builder。</returns>
        public ClassCodeScope WithInterface(string interfaceName)
        {
            mInterfaces.Add(CSharpText.RequireNonEmptyLine(interfaceName, nameof(interfaceName)));
            return this;
        }

        /// <summary>
        /// 为类追加无参数特性。
        /// </summary>
        /// <param name="attributeName">特性类型名。</param>
        /// <returns>当前类 builder。</returns>
        public ClassCodeScope WithAttribute(string attributeName)
        {
            mAttributes.Add(new AttributeCode(attributeName));
            return this;
        }

        /// <summary>
        /// 为类追加带单个原始参数的特性。
        /// </summary>
        /// <param name="attributeName">特性类型名。</param>
        /// <param name="argument">参数表达式。</param>
        /// <returns>当前类 builder。</returns>
        public ClassCodeScope WithAttribute(string attributeName, string argument)
        {
            mAttributes.Add(new AttributeCode(attributeName).WithArgument(argument));
            return this;
        }

        /// <summary>
        /// 仅允许同一程序集中的 fluent API 追加类成员。
        /// </summary>
        /// <param name="node">待追加成员节点。</param>
        void ICodeContainer.Add(ICodeNode node)
        {
            mBody.Add(node);
        }

        /// <summary>
        /// 校验静态/密封/继承组合后渲染完整类声明。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        void ICodeNode.Generate(CodeTextWriter writer)
        {
            ValidateDeclaration();
            for (var index = 0; index < mAttributes.Count; index++)
            {
                mAttributes[index].Generate(writer);
            }

            CodeScopeRenderer.Generate(writer, BuildDeclaration(), mBody, false);
        }

        /// <summary>
        /// 拒绝 C# 不允许的 static sealed 和静态类继承组合。
        /// </summary>
        private void ValidateDeclaration()
        {
            if (mIsStatic && mIsSealed)
            {
                throw new InvalidOperationException("static 类不能再次声明 sealed。");
            }

            if (mIsStatic && (mParentClassName != null || mInterfaces.Count > 0))
            {
                throw new InvalidOperationException("static 类不能声明父类型或接口。");
            }
        }

        /// <summary>
        /// 按固定顺序构造类声明头及继承列表。
        /// </summary>
        /// <returns>不含花括号的类声明头。</returns>
        private string BuildDeclaration()
        {
            StringBuilder builder = new StringBuilder(96);
            builder.Append(CodeModifierText.GetAccessText(mAccess));
            if (mIsStatic) builder.Append("static ");
            if (mIsSealed) builder.Append("sealed ");
            if (mIsPartial) builder.Append("partial ");
            builder.Append("class ").Append(mClassName);
            AppendInheritance(builder);
            return builder.ToString();
        }

        /// <summary>
        /// 按父类型在前、接口在后的顺序追加继承列表。
        /// </summary>
        /// <param name="builder">类声明文本构建器。</param>
        private void AppendInheritance(StringBuilder builder)
        {
            bool hasItem = false;
            if (mParentClassName != null)
            {
                builder.Append(" : ").Append(mParentClassName);
                hasItem = true;
            }

            for (var index = 0; index < mInterfaces.Count; index++)
            {
                builder.Append(hasItem ? ", " : " : ").Append(mInterfaces[index]);
                hasItem = true;
            }
        }
    }
}
