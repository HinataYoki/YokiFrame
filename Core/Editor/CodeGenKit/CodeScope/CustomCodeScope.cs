using System;

namespace YokiFrame
{
    public sealed class CustomCodeScope : CodeScope
    {
        private readonly string firstLine;

        public CustomCodeScope(string firstLine)
        {
            this.firstLine = firstLine;
        }

        protected override void GenFirstLine(ICodeWriteKit writer)
        {
            writer.WriteLine(firstLine);
        }
    }

    public static partial class ICodeScopeExtensions
    {
        public static ICodeScope CustomScope(this ICodeScope self, string firstLine, bool semicolon, Action<CustomCodeScope> configure)
        {
            CustomCodeScope scope = new CustomCodeScope(firstLine)
            {
                Semicolon = semicolon
            };

            if (configure != null)
            {
                configure(scope);
            }

            self.Codes.Add(scope);
            return self;
        }
    }
}
