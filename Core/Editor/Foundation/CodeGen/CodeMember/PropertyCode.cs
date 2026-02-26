using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 属性代码生成
    /// </summary>
    public class PropertyCode : ICode
    {
        private readonly string mTypeName;
        private readonly string mPropertyName;
        private AccessModifier mAccess;
        private MemberModifier mModifiers;
        private string mComment;
        private readonly List<AttributeCode> mAttributes;

        // Getter/Setter 配置
        private bool mHasGetter = true;
        private bool mHasSetter;
        private string mGetterExpression;
        private Action<ICodeScope> mGetterBody;
        private Action<ICodeScope> mSetterBody;
        private AccessModifier mSetterAccess = AccessModifier.None;

        /// <summary>
        /// 创建属性代码
        /// </summary>
        public PropertyCode(string typeName, string propertyName)
        {
            mTypeName = typeName;
            mPropertyName = propertyName;
            mAccess = AccessModifier.Public;
            mModifiers = MemberModifier.None;
            mAttributes = new(2);
        }

        /// <summary>
        /// 设置访问修饰符
        /// </summary>
        public PropertyCode WithAccess(AccessModifier access)
        {
            mAccess = access;
            return this;
        }

        /// <summary>
        /// 设置成员修饰符
        /// </summary>
        public PropertyCode WithModifiers(MemberModifier modifiers)
        {
            mModifiers = modifiers;
            return this;
        }

        /// <summary>
        /// 设置注释
        /// </summary>
        public PropertyCode WithComment(string comment)
        {
            mComment = comment;
            return this;
        }

        /// <summary>
        /// 添加特性
        /// </summary>
        public PropertyCode WithAttribute(string attributeName)
        {
            mAttributes.Add(new(attributeName));
            return this;
        }

        /// <summary>
        /// 配置为只读自动属性 { get; }
        /// </summary>
        public PropertyCode AsReadonly()
        {
            mHasGetter = true;
            mHasSetter = false;
            mGetterExpression = null;
            mGetterBody = null;
            return this;
        }

        /// <summary>
        /// 配置为自动属性 { get; set; }
        /// </summary>
        public PropertyCode AsAutoProperty(AccessModifier setterAccess = AccessModifier.None)
        {
            mHasGetter = true;
            mHasSetter = true;
            mSetterAccess = setterAccess;
            mGetterExpression = null;
            mGetterBody = null;
            mSetterBody = null;
            return this;
        }

        /// <summary>
        /// 配置为表达式体属性 => expression
        /// </summary>
        public PropertyCode WithExpressionBody(string expression)
        {
            mGetterExpression = expression;
            mHasGetter = true;
            mHasSetter = false;
            mGetterBody = null;
            return this;
        }

        /// <summary>
        /// 配置 Getter 体
        /// </summary>
        public PropertyCode WithGetter(Action<ICodeScope> getterBody)
        {
            mGetterBody = getterBody;
            mGetterExpression = null;
            mHasGetter = true;
            return this;
        }

        /// <summary>
        /// 配置 Setter 体
        /// </summary>
        public PropertyCode WithSetter(Action<ICodeScope> setterBody, AccessModifier access = AccessModifier.None)
        {
            mSetterBody = setterBody;
            mHasSetter = true;
            mSetterAccess = access;
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

            // 生成特性
            foreach (var attr in mAttributes)
            {
                attr.Gen(writer);
            }

            var accessStr = ModifierHelper.GetAccessString(mAccess);
            var modifierStr = ModifierHelper.GetMemberString(mModifiers);

            // 表达式体属性
            if (!string.IsNullOrEmpty(mGetterExpression) && !mHasSetter)
            {
                writer.WriteLine($"{accessStr}{modifierStr}{mTypeName} {mPropertyName} => {mGetterExpression};");
                return;
            }

            // 自动属性或完整属性
            if (mGetterBody is null && mSetterBody is null)
            {
                // 自动属性
                var getterStr = mHasGetter ? "get; " : "";
                var setterAccessStr = ModifierHelper.GetAccessString(mSetterAccess);
                var setterStr = mHasSetter ? $"{setterAccessStr}set; " : "";
                writer.WriteLine($"{accessStr}{modifierStr}{mTypeName} {mPropertyName} {{ {getterStr}{setterStr}}}");
            }
            else
            {
                // 完整属性
                writer.WriteLine($"{accessStr}{modifierStr}{mTypeName} {mPropertyName}");
                writer.WriteLine("{");
                writer.IndentCount++;

                if (mHasGetter)
                {
                    if (mGetterBody is not null)
                    {
                        var getterScope = new CustomCodeScope("get");
                        mGetterBody.Invoke(getterScope);
                        getterScope.Gen(writer);
                    }
                    else
                    {
                        writer.WriteLine("get;");
                    }
                }

                if (mHasSetter)
                {
                    var setterAccessStr = ModifierHelper.GetAccessString(mSetterAccess);
                    if (mSetterBody is not null)
                    {
                        var setterScope = new CustomCodeScope($"{setterAccessStr}set");
                        mSetterBody.Invoke(setterScope);
                        setterScope.Gen(writer);
                    }
                    else
                    {
                        writer.WriteLine($"{setterAccessStr}set;");
                    }
                }

                writer.IndentCount--;
                writer.WriteLine("}");
            }
        }
    }

    public static partial class ICodeScopeExtensions
    {
        /// <summary>
        /// 添加属性
        /// </summary>
        public static ICodeScope Property(
            this ICodeScope self,
            string typeName,
            string propertyName,
            Action<PropertyCode> configure = null)
        {
            var prop = new PropertyCode(typeName, propertyName);
            configure?.Invoke(prop);
            self.Codes.Add(prop);
            return self;
        }

        /// <summary>
        /// 添加只读表达式体属性
        /// </summary>
        public static ICodeScope ReadonlyProperty(
            this ICodeScope self,
            string typeName,
            string propertyName,
            string expression,
            string comment = null)
        {
            var prop = new PropertyCode(typeName, propertyName)
                .WithExpressionBody(expression);
            if (!string.IsNullOrEmpty(comment))
            {
                prop.WithComment(comment);
            }
            self.Codes.Add(prop);
            return self;
        }

        /// <summary>
        /// 添加自动属性
        /// </summary>
        public static ICodeScope AutoProperty(
            this ICodeScope self,
            string typeName,
            string propertyName,
            bool hasSetter = true,
            string comment = null)
        {
            var prop = new PropertyCode(typeName, propertyName);
            if (hasSetter)
            {
                prop.AsAutoProperty();
            }
            else
            {
                prop.AsReadonly();
            }
            if (!string.IsNullOrEmpty(comment))
            {
                prop.WithComment(comment);
            }
            self.Codes.Add(prop);
            return self;
        }
    }
}
