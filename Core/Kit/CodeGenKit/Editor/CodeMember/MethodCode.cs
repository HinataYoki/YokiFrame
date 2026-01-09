using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 方法代码生成
    /// </summary>
    public class MethodCode : ICode
    {
        private readonly string mReturnType;
        private readonly string mMethodName;
        private AccessModifier mAccess;
        private MemberModifier mModifiers;
        private string mComment;
        private readonly List<ParameterInfo> mParameters;
        private readonly List<AttributeCode> mAttributes;
        private readonly List<string> mGenericParams;
        private readonly List<string> mGenericConstraints;
        private Action<ICodeScope> mBodyBuilder;
        private string mExpressionBody;

        /// <summary>
        /// 创建方法代码
        /// </summary>
        public MethodCode(string returnType, string methodName)
        {
            mReturnType = returnType;
            mMethodName = methodName;
            mAccess = AccessModifier.Public;
            mModifiers = MemberModifier.None;
            mParameters = new List<ParameterInfo>(4);
            mAttributes = new List<AttributeCode>(2);
            mGenericParams = new List<string>(2);
            mGenericConstraints = new List<string>(2);
        }

        /// <summary>
        /// 设置访问修饰符
        /// </summary>
        public MethodCode WithAccess(AccessModifier access)
        {
            mAccess = access;
            return this;
        }

        /// <summary>
        /// 设置成员修饰符
        /// </summary>
        public MethodCode WithModifiers(MemberModifier modifiers)
        {
            mModifiers = modifiers;
            return this;
        }

        /// <summary>
        /// 设置注释
        /// </summary>
        public MethodCode WithComment(string comment)
        {
            mComment = comment;
            return this;
        }

        /// <summary>
        /// 添加参数
        /// </summary>
        public MethodCode WithParameter(string type, string name, string defaultValue = null)
        {
            mParameters.Add(new ParameterInfo(type, name, defaultValue));
            return this;
        }

        /// <summary>
        /// 添加特性
        /// </summary>
        public MethodCode WithAttribute(string attributeName)
        {
            mAttributes.Add(new AttributeCode(attributeName));
            return this;
        }

        /// <summary>
        /// 添加带参数的特性
        /// </summary>
        public MethodCode WithAttribute(string attributeName, string argument)
        {
            mAttributes.Add(new AttributeCode(attributeName).WithArgument(argument));
            return this;
        }

        /// <summary>
        /// 添加泛型参数
        /// </summary>
        public MethodCode WithGenericParameter(string paramName, string constraint = null)
        {
            mGenericParams.Add(paramName);
            if (!string.IsNullOrEmpty(constraint))
            {
                mGenericConstraints.Add($"where {paramName} : {constraint}");
            }
            return this;
        }

        /// <summary>
        /// 设置方法体（块体）
        /// </summary>
        public MethodCode WithBody(Action<ICodeScope> bodyBuilder)
        {
            mBodyBuilder = bodyBuilder;
            mExpressionBody = null;
            return this;
        }

        /// <summary>
        /// 设置方法体（表达式体）
        /// </summary>
        public MethodCode WithExpressionBody(string expression)
        {
            mExpressionBody = expression;
            mBodyBuilder = null;
            return this;
        }

        public void Gen(ICodeWriteKit writer)
        {
            // 生成注释
            if (!string.IsNullOrEmpty(mComment))
            {
                writer.WriteLine("/// <summary>");
                writer.WriteLine($"/// {mComment}");
                writer.WriteLine("/// </summary>");
            }

            // 生成参数注释
            foreach (var param in mParameters)
            {
                if (!string.IsNullOrEmpty(param.Comment))
                {
                    writer.WriteLine($"/// <param name=\"{param.Name}\">{param.Comment}</param>");
                }
            }

            // 生成特性
            foreach (var attr in mAttributes)
            {
                attr.Gen(writer);
            }

            // 构建方法签名
            var accessStr = ModifierHelper.GetAccessString(mAccess);
            var modifierStr = ModifierHelper.GetMemberString(mModifiers);
            var genericStr = mGenericParams.Count > 0 ? $"<{string.Join(", ", mGenericParams)}>" : "";
            var paramsStr = BuildParameterString();
            var constraintStr = mGenericConstraints.Count > 0 ? $" {string.Join(" ", mGenericConstraints)}" : "";

            // 表达式体方法
            if (!string.IsNullOrEmpty(mExpressionBody))
            {
                writer.WriteLine($"{accessStr}{modifierStr}{mReturnType} {mMethodName}{genericStr}({paramsStr}){constraintStr} => {mExpressionBody};");
                return;
            }

            // 块体方法
            writer.WriteLine($"{accessStr}{modifierStr}{mReturnType} {mMethodName}{genericStr}({paramsStr}){constraintStr}");
            writer.WriteLine("{");
            writer.IndentCount++;

            if (mBodyBuilder != null)
            {
                var bodyScope = new MethodBodyScope();
                mBodyBuilder.Invoke(bodyScope);
                foreach (var code in bodyScope.Codes)
                {
                    code.Gen(writer);
                }
            }

            writer.IndentCount--;
            writer.WriteLine("}");
        }

        private string BuildParameterString()
        {
            if (mParameters.Count == 0)
                return string.Empty;

            var builder = new StringBuilder(64);
            for (int i = 0; i < mParameters.Count; i++)
            {
                if (i > 0) builder.Append(", ");
                var param = mParameters[i];
                builder.Append($"{param.Type} {param.Name}");
                if (!string.IsNullOrEmpty(param.DefaultValue))
                {
                    builder.Append($" = {param.DefaultValue}");
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// 参数信息
        /// </summary>
        private readonly struct ParameterInfo
        {
            public readonly string Type;
            public readonly string Name;
            public readonly string DefaultValue;
            public readonly string Comment;

            public ParameterInfo(string type, string name, string defaultValue, string comment = null)
            {
                Type = type;
                Name = name;
                DefaultValue = defaultValue;
                Comment = comment;
            }
        }

        /// <summary>
        /// 方法体作用域（不生成花括号）
        /// </summary>
        private class MethodBodyScope : ICodeScope
        {
            public List<ICode> Codes { get; set; } = new();

            public void Gen(ICodeWriteKit writer)
            {
                foreach (var code in Codes)
                {
                    code.Gen(writer);
                }
            }
        }
    }

    public static partial class ICodeScopeExtensions
    {
        /// <summary>
        /// 添加方法
        /// </summary>
        public static ICodeScope Method(
            this ICodeScope self,
            string returnType,
            string methodName,
            Action<MethodCode> configure)
        {
            var method = new MethodCode(returnType, methodName);
            configure?.Invoke(method);
            self.Codes.Add(method);
            return self;
        }

        /// <summary>
        /// 添加 void 方法
        /// </summary>
        public static ICodeScope VoidMethod(
            this ICodeScope self,
            string methodName,
            Action<MethodCode> configure)
        {
            return self.Method("void", methodName, configure);
        }

        /// <summary>
        /// 添加 override 方法
        /// </summary>
        public static ICodeScope OverrideMethod(
            this ICodeScope self,
            string returnType,
            string methodName,
            Action<MethodCode> configure)
        {
            var method = new MethodCode(returnType, methodName)
                .WithAccess(AccessModifier.Protected)
                .WithModifiers(MemberModifier.Override);
            configure?.Invoke(method);
            self.Codes.Add(method);
            return self;
        }

        /// <summary>
        /// 添加 protected override void 方法
        /// </summary>
        public static ICodeScope ProtectedOverrideVoid(
            this ICodeScope self,
            string methodName,
            Action<MethodCode> configure)
        {
            return self.OverrideMethod("void", methodName, configure);
        }
    }
}
