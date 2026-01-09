namespace YokiFrame
{
    public class OpenBraceCode : ICode
    {
        public void Gen(ICodeWriteKit writer)
        {
            writer.WriteLine("{");
        }
    }
}