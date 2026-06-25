namespace YokiFrame
{
    public sealed class EmptyLineCode : ICode
    {
        public void Gen(ICodeWriteKit writer)
        {
            writer.WriteLine();
        }
    }
}
