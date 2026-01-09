namespace YokiFrame
{
    public class EmptyLineCode : ICode
    {
        public void Gen(ICodeWriteKit writer)
        {
            writer.WriteLine();
        }
    }

    public static partial class ICodeScopeExtensions
    {
        public static ICodeScope EmptyLine(this ICodeScope self)
        {
            self.Codes.Add(new EmptyLineCode());
            return self;
        }
    }

}