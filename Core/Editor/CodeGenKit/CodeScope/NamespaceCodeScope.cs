using System;

namespace YokiFrame
{
    public sealed class NamespaceCodeScope : CodeScope
    {
        private readonly string namespaceName;

        public NamespaceCodeScope(string namespaceName)
        {
            this.namespaceName = namespaceName;
        }

        protected override void GenFirstLine(ICodeWriteKit writer)
        {
            writer.WriteFormatLine("namespace {0}", namespaceName);
        }
    }

    public static partial class ICodeScopeExtensions
    {
        public static ICodeScope Namespace(this ICodeScope self, string namespaceName, Action<NamespaceCodeScope> configure)
        {
            NamespaceCodeScope scope = new NamespaceCodeScope(namespaceName);
            if (configure != null)
            {
                configure(scope);
            }

            self.Codes.Add(scope);
            return self;
        }
    }
}
