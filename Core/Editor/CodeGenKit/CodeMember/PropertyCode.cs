using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public sealed class PropertyCode : ICode
    {
        private readonly string typeName;
        private readonly string propertyName;
        private readonly List<AttributeCode> attributes = new List<AttributeCode>(2);
        private AccessModifier access = AccessModifier.Public;
        private MemberModifier modifiers = MemberModifier.None;
        private string comment;
        private bool hasGetter = true;
        private bool hasSetter;
        private string getterExpression;
        private Action<ICodeScope> getterBody;
        private Action<ICodeScope> setterBody;
        private AccessModifier setterAccess = AccessModifier.None;

        public PropertyCode(string typeName, string propertyName)
        {
            this.typeName = typeName;
            this.propertyName = propertyName;
        }

        public PropertyCode WithAccess(AccessModifier access)
        {
            this.access = access;
            return this;
        }

        public PropertyCode WithModifiers(MemberModifier modifiers)
        {
            this.modifiers = modifiers;
            return this;
        }

        public PropertyCode WithComment(string comment)
        {
            this.comment = comment;
            return this;
        }

        public PropertyCode WithAttribute(string attributeName)
        {
            attributes.Add(new AttributeCode(attributeName));
            return this;
        }

        public PropertyCode AsReadonly()
        {
            hasGetter = true;
            hasSetter = false;
            getterExpression = null;
            getterBody = null;
            setterBody = null;
            return this;
        }

        public PropertyCode AsAutoProperty(AccessModifier setterAccess = AccessModifier.None)
        {
            hasGetter = true;
            hasSetter = true;
            this.setterAccess = setterAccess;
            getterExpression = null;
            getterBody = null;
            setterBody = null;
            return this;
        }

        public PropertyCode WithExpressionBody(string expression)
        {
            getterExpression = expression;
            hasGetter = true;
            hasSetter = false;
            getterBody = null;
            return this;
        }

        public PropertyCode WithGetter(Action<ICodeScope> getterBody)
        {
            this.getterBody = getterBody;
            getterExpression = null;
            hasGetter = true;
            return this;
        }

        public PropertyCode WithSetter(Action<ICodeScope> setterBody, AccessModifier access = AccessModifier.None)
        {
            this.setterBody = setterBody;
            hasSetter = true;
            setterAccess = access;
            return this;
        }

        public void Gen(ICodeWriteKit writer)
        {
            FieldCode.WriteSummary(writer, comment);
            for (int i = 0; i < attributes.Count; i++)
            {
                attributes[i].Gen(writer);
            }

            string header = ModifierHelper.GetAccessString(access) + ModifierHelper.GetMemberString(modifiers) + typeName + " " + propertyName;
            if (!string.IsNullOrEmpty(getterExpression) && !hasSetter)
            {
                writer.WriteLine(header + " => " + getterExpression + ";");
                return;
            }

            if (getterBody == null && setterBody == null)
            {
                string getterText = hasGetter ? "get; " : string.Empty;
                string setterText = hasSetter ? ModifierHelper.GetAccessString(setterAccess) + "set; " : string.Empty;
                writer.WriteLine(header + " { " + getterText + setterText + "}");
                return;
            }

            writer.WriteLine(header);
            writer.WriteLine("{");
            writer.IndentCount++;
            WriteAccessor(writer, "get", AccessModifier.None, hasGetter, getterBody);
            WriteAccessor(writer, "set", setterAccess, hasSetter, setterBody);
            writer.IndentCount--;
            writer.WriteLine("}");
        }

        private static void WriteAccessor(ICodeWriteKit writer, string name, AccessModifier access, bool enabled, Action<ICodeScope> body)
        {
            if (!enabled)
            {
                return;
            }

            string firstLine = ModifierHelper.GetAccessString(access) + name;
            if (body == null)
            {
                writer.WriteLine(firstLine + ";");
                return;
            }

            CustomCodeScope scope = new CustomCodeScope(firstLine);
            body(scope);
            scope.Gen(writer);
        }
    }

    public static partial class ICodeScopeExtensions
    {
        public static ICodeScope Property(this ICodeScope self, string typeName, string propertyName, Action<PropertyCode> configure = null)
        {
            PropertyCode property = new PropertyCode(typeName, propertyName);
            if (configure != null)
            {
                configure(property);
            }

            self.Codes.Add(property);
            return self;
        }

        public static ICodeScope ReadonlyProperty(this ICodeScope self, string typeName, string propertyName, string expression, string comment = null)
        {
            PropertyCode property = new PropertyCode(typeName, propertyName).WithExpressionBody(expression);
            if (!string.IsNullOrEmpty(comment))
            {
                property.WithComment(comment);
            }

            self.Codes.Add(property);
            return self;
        }

        public static ICodeScope AutoProperty(this ICodeScope self, string typeName, string propertyName, bool hasSetter = true, string comment = null)
        {
            PropertyCode property = new PropertyCode(typeName, propertyName);
            if (hasSetter)
            {
                property.AsAutoProperty();
            }
            else
            {
                property.AsReadonly();
            }

            if (!string.IsNullOrEmpty(comment))
            {
                property.WithComment(comment);
            }

            self.Codes.Add(property);
            return self;
        }
    }
}
