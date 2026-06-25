using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    public sealed class MethodCode : ICode
    {
        private readonly string returnType;
        private readonly string methodName;
        private readonly List<ParameterInfo> parameters = new List<ParameterInfo>(4);
        private readonly List<AttributeCode> attributes = new List<AttributeCode>(2);
        private readonly List<string> genericParams = new List<string>(2);
        private readonly List<string> genericConstraints = new List<string>(2);
        private AccessModifier access = AccessModifier.Public;
        private MemberModifier modifiers = MemberModifier.None;
        private string comment;
        private Action<ICodeScope> bodyBuilder;
        private string expressionBody;

        public MethodCode(string returnType, string methodName)
        {
            this.returnType = returnType;
            this.methodName = methodName;
        }

        public MethodCode WithAccess(AccessModifier access)
        {
            this.access = access;
            return this;
        }

        public MethodCode WithModifiers(MemberModifier modifiers)
        {
            this.modifiers = modifiers;
            return this;
        }

        public MethodCode WithComment(string comment)
        {
            this.comment = comment;
            return this;
        }

        public MethodCode WithParameter(string type, string name, string defaultValue = null, string comment = null)
        {
            parameters.Add(new ParameterInfo(type, name, defaultValue, comment));
            return this;
        }

        public MethodCode WithAttribute(string attributeName)
        {
            attributes.Add(new AttributeCode(attributeName));
            return this;
        }

        public MethodCode WithAttribute(string attributeName, string argument)
        {
            attributes.Add(new AttributeCode(attributeName).WithArgument(argument));
            return this;
        }

        public MethodCode WithGenericParameter(string paramName, string constraint = null)
        {
            genericParams.Add(paramName);
            if (!string.IsNullOrEmpty(constraint))
            {
                genericConstraints.Add("where " + paramName + " : " + constraint);
            }

            return this;
        }

        public MethodCode WithBody(Action<ICodeScope> bodyBuilder)
        {
            this.bodyBuilder = bodyBuilder;
            expressionBody = null;
            return this;
        }

        public MethodCode WithExpressionBody(string expression)
        {
            expressionBody = expression;
            bodyBuilder = null;
            return this;
        }

        public void Gen(ICodeWriteKit writer)
        {
            FieldCode.WriteSummary(writer, comment);
            for (int i = 0; i < parameters.Count; i++)
            {
                if (!string.IsNullOrEmpty(parameters[i].Comment))
                {
                    writer.WriteFormatLine("/// <param name=\"{0}\">{1}</param>", parameters[i].Name, parameters[i].Comment);
                }
            }

            for (int i = 0; i < attributes.Count; i++)
            {
                attributes[i].Gen(writer);
            }

            string genericText = genericParams.Count > 0 ? "<" + string.Join(", ", genericParams) + ">" : string.Empty;
            string constraintText = genericConstraints.Count > 0 ? " " + string.Join(" ", genericConstraints) : string.Empty;
            string header = ModifierHelper.GetAccessString(access)
                + ModifierHelper.GetMemberString(modifiers)
                + returnType
                + " "
                + methodName
                + genericText
                + "("
                + BuildParameterString()
                + ")"
                + constraintText;

            if (!string.IsNullOrEmpty(expressionBody))
            {
                writer.WriteLine(header + " => " + expressionBody + ";");
                return;
            }

            writer.WriteLine(header);
            writer.WriteLine("{");
            writer.IndentCount++;
            if (bodyBuilder != null)
            {
                RootCode body = new RootCode();
                bodyBuilder(body);
                body.Gen(writer);
            }

            writer.IndentCount--;
            writer.WriteLine("}");
        }

        private string BuildParameterString()
        {
            if (parameters.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(64);
            for (int i = 0; i < parameters.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                ParameterInfo parameter = parameters[i];
                builder.Append(parameter.Type);
                builder.Append(' ');
                builder.Append(parameter.Name);
                if (!string.IsNullOrEmpty(parameter.DefaultValue))
                {
                    builder.Append(" = ");
                    builder.Append(parameter.DefaultValue);
                }
            }

            return builder.ToString();
        }

        private readonly struct ParameterInfo
        {
            public readonly string Type;
            public readonly string Name;
            public readonly string DefaultValue;
            public readonly string Comment;

            public ParameterInfo(string type, string name, string defaultValue, string comment)
            {
                Type = type;
                Name = name;
                DefaultValue = defaultValue;
                Comment = comment;
            }
        }
    }

    public static partial class ICodeScopeExtensions
    {
        public static ICodeScope Method(this ICodeScope self, string returnType, string methodName, Action<MethodCode> configure)
        {
            MethodCode method = new MethodCode(returnType, methodName);
            if (configure != null)
            {
                configure(method);
            }

            self.Codes.Add(method);
            return self;
        }

        public static ICodeScope VoidMethod(this ICodeScope self, string methodName, Action<MethodCode> configure)
        {
            return self.Method("void", methodName, configure);
        }

        public static ICodeScope OverrideMethod(this ICodeScope self, string returnType, string methodName, Action<MethodCode> configure)
        {
            MethodCode method = new MethodCode(returnType, methodName)
                .WithAccess(AccessModifier.Protected)
                .WithModifiers(MemberModifier.Override);
            if (configure != null)
            {
                configure(method);
            }

            self.Codes.Add(method);
            return self;
        }

        public static ICodeScope ProtectedOverrideVoid(this ICodeScope self, string methodName, Action<MethodCode> configure)
        {
            return self.OverrideMethod("void", methodName, configure);
        }
    }
}
