using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 特性代码生成
    /// </summary>
    public class AttributeCode : ICode
    {
        private readonly string mAttributeName;
        private readonly List<string> mArguments;

        /// <summary>
        /// 创建特性代码
        /// </summary>
        /// <param name="attributeName">特性名称（不含 Attribute 后缀）</param>
        public AttributeCode(string attributeName)
        {
            mAttributeName = attributeName;
            mArguments = new List<string>(4);
        }

        /// <summary>
        /// 添加参数
        /// </summary>
        /// <param name="argument">参数值（字符串需要包含引号）</param>
        public AttributeCode WithArgument(string argument)
        {
            if (!string.IsNullOrEmpty(argument))
            {
                mArguments.Add(argument);
            }
            return this;
        }

        /// <summary>
        /// 添加命名参数
        /// </summary>
        /// <param name="name">参数名</param>
        /// <param name="value">参数值</param>
        public AttributeCode WithNamedArgument(string name, string value)
        {
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
            {
                mArguments.Add($"{name} = {value}");
            }
            return this;
        }

        public void Gen(ICodeWriteKit writer)
        {
            if (mArguments.Count == 0)
            {
                writer.WriteLine($"[{mAttributeName}]");
            }
            else
            {
                writer.WriteLine($"[{mAttributeName}({string.Join(", ", mArguments)})]");
            }
        }
    }

    public static partial class ICodeScopeExtensions
    {
        /// <summary>
        /// 添加特性
        /// </summary>
        public static ICodeScope Attribute(this ICodeScope self, string attributeName)
        {
            self.Codes.Add(new AttributeCode(attributeName));
            return self;
        }

        /// <summary>
        /// 添加带参数的特性
        /// </summary>
        public static ICodeScope Attribute(this ICodeScope self, string attributeName, string argument)
        {
            var attr = new AttributeCode(attributeName).WithArgument(argument);
            self.Codes.Add(attr);
            return self;
        }
    }
}
