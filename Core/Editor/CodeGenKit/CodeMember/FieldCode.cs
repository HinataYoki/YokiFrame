using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public sealed class FieldCode : ICode
    {
        private readonly string typeName;
        private readonly string fieldName;
        private readonly List<AttributeCode> attributes = new List<AttributeCode>(2);
        private string defaultValue;
        private string comment;
        private AccessModifier access = AccessModifier.Private;
        private MemberModifier modifiers = MemberModifier.None;

        public FieldCode(string typeName, string fieldName)
        {
            this.typeName = typeName;
            this.fieldName = fieldName;
        }

        public FieldCode WithAccess(AccessModifier access)
        {
            this.access = access;
            return this;
        }

        public FieldCode WithModifiers(MemberModifier modifiers)
        {
            this.modifiers = modifiers;
            return this;
        }

        public FieldCode WithDefaultValue(string defaultValue)
        {
            this.defaultValue = defaultValue;
            return this;
        }

        public FieldCode WithComment(string comment)
        {
            this.comment = comment;
            return this;
        }

        public FieldCode WithAttribute(string attributeName)
        {
            attributes.Add(new AttributeCode(attributeName));
            return this;
        }

        public FieldCode WithAttribute(string attributeName, string argument)
        {
            attributes.Add(new AttributeCode(attributeName).WithArgument(argument));
            return this;
        }

        public void Gen(ICodeWriteKit writer)
        {
            WriteSummary(writer, comment);
            for (int i = 0; i < attributes.Count; i++)
            {
                attributes[i].Gen(writer);
            }

            string defaultText = string.IsNullOrEmpty(defaultValue) ? string.Empty : " = " + defaultValue;
            writer.WriteLine(ModifierHelper.GetAccessString(access) + ModifierHelper.GetMemberString(modifiers) + typeName + " " + fieldName + defaultText + ";");
        }

        internal static void WriteSummary(ICodeWriteKit writer, string summary)
        {
            if (string.IsNullOrEmpty(summary))
            {
                return;
            }

            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// " + summary);
            writer.WriteLine("/// </summary>");
        }
    }

    public static partial class ICodeScopeExtensions
    {
        public static ICodeScope Field(this ICodeScope self, string typeName, string fieldName, Action<FieldCode> configure = null)
        {
            FieldCode field = new FieldCode(typeName, fieldName);
            if (configure != null)
            {
                configure(field);
            }

            self.Codes.Add(field);
            return self;
        }

        public static ICodeScope PublicField(this ICodeScope self, string typeName, string fieldName, string comment = null)
        {
            FieldCode field = new FieldCode(typeName, fieldName).WithAccess(AccessModifier.Public);
            if (!string.IsNullOrEmpty(comment))
            {
                field.WithComment(comment);
            }

            self.Codes.Add(field);
            return self;
        }

        public static ICodeScope PrivateField(this ICodeScope self, string typeName, string fieldName, string defaultValue = null)
        {
            FieldCode field = new FieldCode(typeName, fieldName).WithAccess(AccessModifier.Private);
            if (!string.IsNullOrEmpty(defaultValue))
            {
                field.WithDefaultValue(defaultValue);
            }

            self.Codes.Add(field);
            return self;
        }

        public static ICodeScope SerializeField(this ICodeScope self, string typeName, string fieldName, string comment = null)
        {
            FieldCode field = new FieldCode(typeName, fieldName)
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
