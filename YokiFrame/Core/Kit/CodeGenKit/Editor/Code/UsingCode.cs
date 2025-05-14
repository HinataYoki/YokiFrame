namespace YokiFrame
{
    public class UsingCode : ICode
    {
        private readonly string Namespace;
        public UsingCode(string @namespace) => Namespace = @namespace;

        public void Gen(ICodeWriter writer)
        {
            writer.WriteFormatLine("using {0};", Namespace);
        }
    }


    public static partial class ICodeScopeExtensions
    {
        public static ICodeScope Using(this ICodeScope self, string @namespace)
        {
            self.Codes.Add(new UsingCode(@namespace));
            return self;
        }
    }
}