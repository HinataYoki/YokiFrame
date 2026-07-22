using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 表示方法声明及其参数、泛型约束、特性、文档和 body。
    /// </summary>
    public sealed class MethodCode : ICodeNode
    {
        private readonly string mReturnType;
        private readonly string mMethodName;
        private readonly List<ParameterInfo> mParameters = new List<ParameterInfo>();
        private readonly List<AttributeCode> mAttributes = new List<AttributeCode>();
        private readonly List<string> mGenericParameters = new List<string>();
        private readonly List<string> mGenericConstraints = new List<string>();
        private AccessModifier mAccess = AccessModifier.Public;
        private MemberModifier mModifiers = MemberModifier.None;
        private string mComment;
        private Action<ICodeScope> mBodyBuilder;
        private string mExpressionBody;

        /// <summary>
        /// 创建方法 builder；方法名严格校验，返回类型保留为调用方负责的单行表达式。
        /// </summary>
        /// <param name="returnType">返回类型表达式。</param>
        /// <param name="methodName">方法名称。</param>
        public MethodCode(string returnType, string methodName)
        {
            mReturnType = CSharpText.RequireNonEmptyLine(returnType, nameof(returnType));
            mMethodName = CSharpIdentifierValidator.RequireIdentifier(methodName, nameof(methodName));
        }

        /// <summary>
        /// 设置方法访问级别。
        /// </summary>
        /// <param name="access">目标访问级别。</param>
        /// <returns>当前方法 builder。</returns>
        public MethodCode WithAccess(AccessModifier access)
        {
            CodeModifierText.GetAccessText(access);
            mAccess = access;
            return this;
        }

        /// <summary>
        /// 设置方法修饰符；具体组合在生成前按方法规则校验。
        /// </summary>
        /// <param name="modifiers">方法修饰符组合。</param>
        /// <returns>当前方法 builder。</returns>
        public MethodCode WithModifiers(MemberModifier modifiers)
        {
            mModifiers = modifiers;
            return this;
        }

        /// <summary>
        /// 设置方法 XML summary。
        /// </summary>
        /// <param name="comment">方法职责说明。</param>
        /// <returns>当前方法 builder。</returns>
        public MethodCode WithComment(string comment)
        {
            mComment = comment;
            return this;
        }

        /// <summary>
        /// 追加方法参数，并可配置默认值表达式和 XML 参数说明。
        /// </summary>
        /// <param name="type">参数类型表达式。</param>
        /// <param name="name">参数名称。</param>
        /// <param name="defaultValue">可选默认值表达式。</param>
        /// <param name="comment">可选参数说明。</param>
        /// <returns>当前方法 builder。</returns>
        public MethodCode WithParameter(string type, string name, string defaultValue = null, string comment = null)
        {
            string validType = CSharpText.RequireNonEmptyLine(type, nameof(type));
            string validName = CSharpIdentifierValidator.RequireIdentifier(name, nameof(name));
            string validDefault = defaultValue == null
                ? null
                : CSharpText.RequireNonEmptyLine(defaultValue, nameof(defaultValue));
            mParameters.Add(new ParameterInfo(validType, validName, validDefault, comment));
            return this;
        }

        /// <summary>
        /// 为方法追加无参数特性。
        /// </summary>
        /// <param name="attributeName">特性类型名称。</param>
        /// <returns>当前方法 builder。</returns>
        public MethodCode WithAttribute(string attributeName)
        {
            mAttributes.Add(new AttributeCode(attributeName));
            return this;
        }

        /// <summary>
        /// 为方法追加带单个原始参数的特性。
        /// </summary>
        /// <param name="attributeName">特性类型名称。</param>
        /// <param name="argument">特性参数表达式。</param>
        /// <returns>当前方法 builder。</returns>
        public MethodCode WithAttribute(string attributeName, string argument)
        {
            mAttributes.Add(new AttributeCode(attributeName).WithArgument(argument));
            return this;
        }

        /// <summary>
        /// 追加泛型参数及其可选 where 约束，参数名不可重复。
        /// </summary>
        /// <param name="parameterName">泛型参数名。</param>
        /// <param name="constraint">可选约束表达式，不包含 where 前缀。</param>
        /// <returns>当前方法 builder。</returns>
        public MethodCode WithGenericParameter(string parameterName, string constraint = null)
        {
            string validName = CSharpIdentifierValidator.RequireIdentifier(parameterName, nameof(parameterName));
            if (mGenericParameters.Contains(validName))
            {
                throw new ArgumentException("泛型参数不能重复: " + validName, nameof(parameterName));
            }

            mGenericParameters.Add(validName);
            if (!string.IsNullOrEmpty(constraint))
            {
                string validConstraint = CSharpText.RequireNonEmptyLine(constraint, nameof(constraint));
                mGenericConstraints.Add("where " + validName + " : " + validConstraint);
            }

            return this;
        }

        /// <summary>
        /// 设置块级方法 body，并清除之前的表达式 body。
        /// </summary>
        /// <param name="bodyBuilder">方法体构建回调；null 表示空 body。</param>
        /// <returns>当前方法 builder。</returns>
        public MethodCode WithBody(Action<ICodeScope> bodyBuilder)
        {
            mBodyBuilder = bodyBuilder;
            mExpressionBody = null;
            return this;
        }

        /// <summary>
        /// 设置表达式方法 body，并清除之前的块级 body。
        /// </summary>
        /// <param name="expression">箭头右侧单行表达式。</param>
        /// <returns>当前方法 builder。</returns>
        public MethodCode WithExpressionBody(string expression)
        {
            mExpressionBody = CSharpText.RequireNonEmptyLine(expression, nameof(expression));
            mBodyBuilder = null;
            return this;
        }

        /// <summary>
        /// 校验方法状态后渲染文档、特性、声明头和对应 body。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        void ICodeNode.Generate(CodeTextWriter writer)
        {
            CodeModifierValidator.ValidateMethod(mModifiers);
            ValidateAbstractMethod();
            XmlDocumentationWriter.WriteSummary(writer, mComment);
            GenerateParameterDocumentation(writer);
            GenerateAttributes(writer);
            string header = BuildHeader();
            if ((mModifiers & MemberModifier.Abstract) != 0)
            {
                writer.WriteLine(header + ";");
                return;
            }

            if (mExpressionBody != null)
            {
                writer.WriteLine(header + " => " + mExpressionBody + ";");
                return;
            }

            GenerateBlockBody(writer, header);
        }

        /// <summary>
        /// 拒绝为 abstract 方法配置表达式或块级 body。
        /// </summary>
        private void ValidateAbstractMethod()
        {
            if ((mModifiers & MemberModifier.Abstract) != 0
                && (mBodyBuilder != null || mExpressionBody != null))
            {
                throw new InvalidOperationException("abstract 方法不能配置方法体。");
            }
        }

        /// <summary>
        /// 为具有说明的参数生成 XML param 节点。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        private void GenerateParameterDocumentation(CodeTextWriter writer)
        {
            for (var index = 0; index < mParameters.Count; index++)
            {
                ParameterInfo parameter = mParameters[index];
                XmlDocumentationWriter.WriteParameter(writer, parameter.Name, parameter.Comment);
            }
        }

        /// <summary>
        /// 按调用顺序渲染方法特性。
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
        /// 构造包含泛型参数、参数列表和 where 约束的完整方法声明头。
        /// </summary>
        /// <returns>不含方法体的声明头。</returns>
        private string BuildHeader()
        {
            string genericText = mGenericParameters.Count == 0
                ? string.Empty
                : "<" + string.Join(", ", mGenericParameters) + ">";
            string constraintText = mGenericConstraints.Count == 0
                ? string.Empty
                : " " + string.Join(" ", mGenericConstraints);
            return CodeModifierText.GetAccessText(mAccess)
                + CodeModifierText.GetMemberText(mModifiers)
                + mReturnType + " " + mMethodName + genericText
                + "(" + BuildParameterList() + ")" + constraintText;
        }

        /// <summary>
        /// 按注册顺序构造方法参数列表。
        /// </summary>
        /// <returns>逗号分隔的参数文本。</returns>
        private string BuildParameterList()
        {
            StringBuilder builder = new StringBuilder(64);
            for (var index = 0; index < mParameters.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(", ");
                }

                ParameterInfo parameter = mParameters[index];
                builder.Append(parameter.Type).Append(' ').Append(parameter.Name);
                if (parameter.DefaultValue != null)
                {
                    builder.Append(" = ").Append(parameter.DefaultValue);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// 渲染普通块级方法体，并在回调失败时恢复缩进。
        /// </summary>
        /// <param name="writer">接收源码的 writer。</param>
        /// <param name="header">完整方法声明头。</param>
        private void GenerateBlockBody(CodeTextWriter writer, string header)
        {
            writer.WriteLine(header);
            writer.WriteLine("{");
            writer.PushIndent();
            try
            {
                if (mBodyBuilder != null)
                {
                    RootCode body = new RootCode();
                    mBodyBuilder(body);
                    ((ICodeNode)body).Generate(writer);
                }
            }
            finally
            {
                writer.PopIndent();
            }

            writer.WriteLine("}");
        }

        /// <summary>
        /// 保存单个参数的类型、名称、默认值与文档说明。
        /// </summary>
        private readonly struct ParameterInfo
        {
            internal readonly string Type;
            internal readonly string Name;
            internal readonly string DefaultValue;
            internal readonly string Comment;

            /// <summary>
            /// 创建已经过外层 builder 校验的参数快照。
            /// </summary>
            /// <param name="type">参数类型。</param>
            /// <param name="name">参数名称。</param>
            /// <param name="defaultValue">可选默认值。</param>
            /// <param name="comment">可选说明。</param>
            internal ParameterInfo(string type, string name, string defaultValue, string comment)
            {
                Type = type;
                Name = name;
                DefaultValue = defaultValue;
                Comment = comment;
            }
        }
    }
}
