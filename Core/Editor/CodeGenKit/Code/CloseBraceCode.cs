namespace YokiFrame
{
    public sealed class CloseBraceCode : ICode
    {
        private readonly bool semicolon;

        public CloseBraceCode(bool semicolon)
        {
            this.semicolon = semicolon;
        }

        public void Gen(ICodeWriteKit writer)
        {
            writer.WriteLine(semicolon ? "};" : "}");
        }
    }
}
