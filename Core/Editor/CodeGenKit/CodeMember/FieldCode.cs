using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 字段代码生成
    /// </summary>
    public class FieldCode : ICode
    {
        private readonly string mTypeName;
        private readonly string mFieldName;
        private string mDefaultValue;
        private AccessModifier mAccess;
        private MemberModifier mModifiers;
        private readonly List<AttributeCode> mAttributes;
        private string mComment;

        /// <summary>
        /// 创建字段代码
        /// </summary>
        /// <param name="typeName">类型名称</param>
        /// <param name="fieldName">字段名称</param>
        public FieldCode(string typeName, string fieldName)
        {
            mTypeName = typeName;
            mFieldName = fieldName;
            mAccess = AccessModifier.Private;
            mModifiers = MemberModifier.None;
            mAttributes = new(2);
        }

        /// <summary>
        /// 设置访问修饰符
        /// </summary>
        public FieldCode WithAccess(AccessModifier access)
        {
            mAccess = access;
            return this;
        }

        /// <summary>
        /// 设置成员修饰符
        /// </summary>
        public FieldCode WithModifiers(MemberModifier modifiers)
        {
            mModifiers = modifiers;
            return this;
        }

        /// <summary>
        /// 设置默认值
        /// </summary>
        public FieldCode WithDefaultValue(string defaultValue)
        {
            mDefaultValue = defaultValue;
            return this;
        }

        /// <summary>
        /// 设置注释
        /// </summary>
        public FieldCode WithComment(string comment)
        {
            mComment = comment;
            return this;
        }

        /// <summary>
        /// 添加特性
        /// </summary>
        public FieldCode WithAttribute(string attributeName)
        {
            mAttributes.Add(new(attributeName));
            return this;
        }

        /// <summary>
        /// 添加带参数的特性
        /// </summary>
        public FieldCode WithAttribute(string attributeName, string argument)
        {
            mAttributes.Add(new AttributeCode(attributeName).WithArgument(argument));
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

            // 生成字段声明
            var accessStr = ModifierHelper.GetAccessString(mAccess);
            var modifierStr = ModifierHelper.GetMemberString(mModifiers);
            var defaultStr = string.IsNullOrEmpty(mDefaultValue) ? "" : $" = {mDefaultValue}";

            writer.WriteLine($"{accessStr}{modifierStr}{mTypeName} {mFieldName}{defaultStr};");
        }
    }

    public static partial class ICodeScopeExtensions
    {
        /// <summary>
        /// 添加字段
        /// </summary>
        public static ICodeScope Field(
            this ICodeScope self,
            string typeName,
            string fieldName,
            System.Action<FieldCode> configure = null)
        {
            var field = new FieldCode(typeName, fieldName);
            configure?.Invoke(field);
            self.Codes.Add(field);
            return self;
        }

        /// <summary>
        /// 添加公共字段
        /// </summary>
        public static ICodeScope PublicField(
            this ICodeScope self,
            string typeName,
            string fieldName,
            string comment = null)
        {
            var field = new FieldCode(typeName, fieldName)
                .WithAccess(AccessModifier.Public);
            if (!string.IsNullOrEmpty(comment))
            {
                field.WithComment(comment);
            }
            self.Codes.Add(field);
            return self;
        }

        /// <summary>
        /// 添加私有字段
        /// </summary>
        public static ICodeScope PrivateField(
            this ICodeScope self,
            string typeName,
            string fieldName,
            string defaultValue = null)
        {
            var field = new FieldCode(typeName, fieldName)
                .WithAccess(AccessModifier.Private);
            if (!string.IsNullOrEmpty(defaultValue))
            {
                field.WithDefaultValue(defaultValue);
            }
            self.Codes.Add(field);
            return self;
        }

        /// <summary>
        /// 添加序列化字段（带 [SerializeField] 特性）
        /// </summary>
        public static ICodeScope SerializeField(
            this ICodeScope self,
            string typeName,
            string fieldName,
            string comment = null)
        {
            var field = new FieldCode(typeName, fieldName)
                .WithAccess(AccessModifier.Public)
                .WithAttribute("SerializeField");
            if (!string.IsNullOrEmpty(comment))
            {
                field.WithComment(comment);
            }
            self.Codes.Add(field);
            return self;
        }
    }
}
