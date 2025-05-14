namespace YokiFrame
{
    public class OpenBraceCode : ICode
    {
        public void Gen(ICodeWriter writer)
        {
            writer.WriteLine("{");
        }
    }
}