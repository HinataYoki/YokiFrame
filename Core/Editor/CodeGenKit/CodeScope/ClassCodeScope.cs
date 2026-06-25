using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public sealed class ClassCodeScope : CodeScope
    {
        private readonly string className;
        private readonly string parentClassName;
        private readonly bool isPartial;
        private readonly bool isStatic;
        private bool isSealed;
        private readonly List<string> interfaces = new List<string>(2);
        private readonly List<AttributeCode> attributes = new List<AttributeCode>(2);
        private AccessModifier access = AccessModifier.Public;

        public ClassCodeScope(string className, string parentClassName, bool isPartial, bool isStatic)
        {
            this.className = className;
            this.parentClassName = parentClassName;
            this.isPartial = isPartial;
            this.isStatic = isStatic;
        }

        public ClassCodeScope WithAccess(AccessModifier access)
        {
            this.access = access;
            return this;
        }

        public ClassCodeScope AsSealed()
        {
            isSealed = true;
            return this;
        }

        public ClassCodeScope WithInterface(string interfaceName)
        {
            if (!string.IsNullOrEmpty(interfaceName))
            {
                interfaces.Add(interfaceName);
            }

            return this;
        }

        public ClassCodeScope WithAttribute(string attributeName)
        {
            attributes.Add(new AttributeCode(attributeName));
            return this;
        }

        public ClassCodeScope WithAttribute(string attributeName, string argument)
        {
            attributes.Add(new AttributeCode(attributeName).WithArgument(argument));
            return this;
        }

        protected override void GenFirstLine(ICodeWriteKit writer)
        {
            for (int i = 0; i < attributes.Count; i++)
            {
                attributes[i].Gen(writer);
            }

            List<string> inheritanceList = new List<string>(4);
            if (!string.IsNullOrEmpty(parentClassName))
            {
                inheritanceList.Add(parentClassName);
            }

            inheritanceList.AddRange(interfaces);

            string inheritance = inheritanceList.Count > 0 ? " : " + string.Join(", ", inheritanceList) : string.Empty;
            string staticText = isStatic ? "static " : string.Empty;
            string sealedText = isSealed ? "sealed " : string.Empty;
            string partialText = isPartial ? "partial " : string.Empty;
            writer.WriteLine(ModifierHelper.GetAccessString(access) + staticText + sealedText + partialText + "class " + className + inheritance);
        }
    }

    public static partial class ICodeScopeExtensions
    {
        public static ICodeScope Class(
            this ICodeScope self,
            string className,
            string parentClassName,
            bool isPartial,
            bool isStatic,
            Action<ClassCodeScope> configure)
        {
            ClassCodeScope scope = new ClassCodeScope(className, parentClassName, isPartial, isStatic);
            if (configure != null)
            {
                configure(scope);
            }

            self.Codes.Add(scope);
            return self;
        }

        public static ICodeScope Class(
            this ICodeScope self,
            string className,
            Action<ClassCodeScope> configure,
            Action<ClassCodeScope> body)
        {
            ClassCodeScope scope = new ClassCodeScope(className, null, false, false);
            if (configure != null)
            {
                configure(scope);
            }

            if (body != null)
            {
                body(scope);
            }

            self.Codes.Add(scope);
            return self;
        }
    }
}
