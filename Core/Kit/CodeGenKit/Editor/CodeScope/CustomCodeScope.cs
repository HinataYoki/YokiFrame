using System;

namespace YokiFrame
{
    public class CustomCodeScope : CodeScope
    {
        private readonly string FirstLine;
        public CustomCodeScope(string firstLine) => FirstLine = firstLine;

        protected override void GenFirstLine(ICodeWriteKit codeWriter)
        {
            codeWriter.WriteLine(FirstLine);
        }
    }

    public static partial class ICodeScopeExtensions
    {
        public static ICodeScope CustomScope(this ICodeScope self, string firstLine, bool semicolon, Action<CustomCodeScope> customCodeScopeSetting)
        {
            var custom = new CustomCodeScope(firstLine)
            {
                Semicolon = semicolon
            };
            customCodeScopeSetting.Invoke(custom);
            self.Codes.Add(custom);
            return self;
        }
    }

}