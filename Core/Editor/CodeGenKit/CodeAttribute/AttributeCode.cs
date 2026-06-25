using System.Collections.Generic;

namespace YokiFrame
{
    public sealed class AttributeCode : ICode
    {
        private readonly string attributeName;
        private readonly List<string> arguments = new List<string>(4);

        public AttributeCode(string attributeName)
        {
            this.attributeName = attributeName;
        }

        public AttributeCode WithArgument(string argument)
        {
            if (!string.IsNullOrEmpty(argument))
            {
                arguments.Add(argument);
            }

            return this;
        }

        public AttributeCode WithNamedArgument(string name, string value)
        {
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
            {
                arguments.Add(name + " = " + value);
            }

            return this;
        }

        public void Gen(ICodeWriteKit writer)
        {
            if (arguments.Count == 0)
            {
                writer.WriteLine("[" + attributeName + "]");
                return;
            }

            writer.WriteLine("[" + attributeName + "(" + string.Join(", ", arguments) + ")]");
        }
    }

    public static partial class ICodeScopeExtensions
    {
        public static ICodeScope Attribute(this ICodeScope self, string attributeName)
        {
            self.Codes.Add(new AttributeCode(attributeName));
            return self;
        }

        public static ICodeScope Attribute(this ICodeScope self, string attributeName, string argument)
        {
            self.Codes.Add(new AttributeCode(attributeName).WithArgument(argument));
            return self;
        }
    }
}
