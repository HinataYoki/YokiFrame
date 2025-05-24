namespace YokiFrame
{
    public class CustomCode : ICode
    {
        private string mLine;

        public CustomCode(string line)
        {
            mLine = line;
        }

        public void Gen(ICodeWriteKit writer)
        {
            writer.WriteLine(mLine);
        }
    }

    public static partial class ICodeScopeExtensions
    {
        public static ICodeScope Custom(this ICodeScope self, string line)
        {
            self.Codes.Add(new CustomCode(line));
            return self;
        }
    }
}