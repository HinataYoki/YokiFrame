using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 类代码作用域
    /// </summary>
    public class ClassCodeScope : CodeScope
    {
        private readonly string mClassName;
        private readonly string mParentClassName;
        private readonly bool mIsPartial;
        private readonly bool mIsStatic;
        private readonly List<string> mInterfaces;
        private readonly List<AttributeCode> mAttributes;
        private AccessModifier mAccess;

        public ClassCodeScope(string className, string parentClassName, bool isPartial, bool isStatic)
        {
            mClassName = className;
            mParentClassName = parentClassName;
            mIsPartial = isPartial;
            mIsStatic = isStatic;
            mInterfaces = new List<string>(2);
            mAttributes = new List<AttributeCode>(2);
            mAccess = AccessModifier.Public;
        }

        /// <summary>
        /// 设置访问修饰符
        /// </summary>
        public ClassCodeScope WithAccess(AccessModifier access)
        {
            mAccess = access;
            return this;
        }

        /// <summary>
        /// 添加实现的接口
        /// </summary>
        public ClassCodeScope WithInterface(string interfaceName)
        {
            if (!string.IsNullOrEmpty(interfaceName))
            {
                mInterfaces.Add(interfaceName);
            }
            return this;
        }

        /// <summary>
        /// 添加特性
        /// </summary>
        public ClassCodeScope WithAttribute(string attributeName)
        {
            mAttributes.Add(new AttributeCode(attributeName));
            return this;
        }

        /// <summary>
        /// 添加带参数的特性
        /// </summary>
        public ClassCodeScope WithAttribute(string attributeName, string argument)
        {
            mAttributes.Add(new AttributeCode(attributeName).WithArgument(argument));
            return this;
        }

        protected override void GenFirstLine(ICodeWriteKit codeWriter)
        {
            // 生成特性
            foreach (var attr in mAttributes)
            {
                attr.Gen(codeWriter);
            }

            // 构建类声明
            var accessStr = ModifierHelper.GetAccessString(mAccess);
            var staticStr = mIsStatic ? "static " : string.Empty;
            var partialStr = mIsPartial ? "partial " : string.Empty;

            // 构建继承列表
            var inheritanceList = new List<string>(4);
            if (!string.IsNullOrEmpty(mParentClassName))
            {
                inheritanceList.Add(mParentClassName);
            }
            inheritanceList.AddRange(mInterfaces);

            var inheritanceStr = inheritanceList.Count > 0
                ? $" : {string.Join(", ", inheritanceList)}"
                : string.Empty;

            codeWriter.WriteLine($"{accessStr}{staticStr}{partialStr}class {mClassName}{inheritanceStr}");
        }
    }

    public static partial class ICodeScopeExtensions
    {
        /// <summary>
        /// 添加类
        /// </summary>
        public static ICodeScope Class(
            this ICodeScope self,
            string className,
            string parentClassName,
            bool isPartial,
            bool isStatic,
            Action<ClassCodeScope> codeScopeSetting)
        {
            var classCodeScope = new ClassCodeScope(className, parentClassName, isPartial, isStatic);
            codeScopeSetting?.Invoke(classCodeScope);
            self.Codes.Add(classCodeScope);
            return self;
        }

        /// <summary>
        /// 添加类（带配置）
        /// </summary>
        public static ICodeScope Class(
            this ICodeScope self,
            string className,
            Action<ClassCodeScope> configure,
            Action<ClassCodeScope> bodySetting)
        {
            var classCodeScope = new ClassCodeScope(className, null, false, false);
            configure?.Invoke(classCodeScope);
            bodySetting?.Invoke(classCodeScope);
            self.Codes.Add(classCodeScope);
            return self;
        }
    }
}