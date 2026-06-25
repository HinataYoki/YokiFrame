namespace YokiFrame
{
    public sealed class OpenBraceCode : ICode
    {
        public void Gen(ICodeWriteKit writer)
        {
            writer.WriteLine("{");
        }
    }
}
