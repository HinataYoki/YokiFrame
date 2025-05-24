using System;

namespace YokiFrame
{
    public class NamespaceCodeScope : CodeScope
    {
        private readonly string Namespace;
        public NamespaceCodeScope(string @namespace) => Namespace = @namespace;

        protected override void GenFirstLine(ICodeWriteKit codeWriter)
        {
            codeWriter.WriteLine(string.Format("namespace {0}", Namespace));
        }
    }



    public static partial class ICodeScopeExtensions
    {
        public static ICodeScope Namespace(this ICodeScope self, string @namespace, Action<NamespaceCodeScope> CodeScopeSetting)
        {
            var namespaceCodeScope = new NamespaceCodeScope(@namespace);
            CodeScopeSetting.Invoke(namespaceCodeScope);
            self.Codes.Add(namespaceCodeScope);
            return self;
        }
    }

}