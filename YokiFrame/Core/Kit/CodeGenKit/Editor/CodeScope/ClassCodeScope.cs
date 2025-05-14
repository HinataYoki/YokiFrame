using System;

namespace YokiFrame
{
    public class ClassCodeScope : CodeScope
    {
        private readonly static string Static = " static";
        private readonly static string Partial = " partial";

        private readonly string ClassName;
        private readonly string ParentClassName;
        private readonly bool IsPartial;
        private readonly bool IsStatic;

        public ClassCodeScope(string className, string parentClassName, bool isPartial, bool isStatic)
        {
            ClassName = className;
            ParentClassName = parentClassName;
            IsPartial = isPartial;
            IsStatic = isStatic;
        }

        protected override void GenFirstLine(ICodeWriter codeWriter)
        {
            var staticKey = IsStatic ? Static : string.Empty;
            var partialKey = IsPartial ? Partial : string.Empty;
            var parentClassKey = !string.IsNullOrEmpty(ParentClassName) ? $" : {ParentClassName}" : string.Empty;

            codeWriter.WriteLine(string.Format("public{0}{1} class {2}{3}", staticKey, partialKey, ClassName, parentClassKey));
        }
    }

    public static partial class ICodeScopeExtensions
    {
        public static ICodeScope Class(this ICodeScope self, string className, string parentClassName, bool isPartial, bool isStatic, Action<ClassCodeScope> codeScopeSetting)
        {
            var classCodeScope = new ClassCodeScope(className, parentClassName, isPartial, isStatic);
            codeScopeSetting.Invoke(classCodeScope);
            self.Codes.Add(classCodeScope);
            return self;
        }
    }
}