namespace YokiFrame
{
    public static partial class ICodeScopeExtensions
    {
        public static ICodeScope Using(this ICodeScope self, string namespaceName)
        {
            self.Codes.Add(new UsingCode(namespaceName));
            return self;
        }

        public static ICodeScope EmptyLine(this ICodeScope self)
        {
            self.Codes.Add(new EmptyLineCode());
            return self;
        }

        public static ICodeScope Custom(this ICodeScope self, string line)
        {
            self.Codes.Add(new CustomCode(line));
            return self;
        }
    }
}
