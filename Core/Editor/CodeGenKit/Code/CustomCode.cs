namespace YokiFrame
{
    public sealed class CustomCode : ICode
    {
        private readonly string line;

        public CustomCode(string line)
        {
            this.line = line;
        }

        public void Gen(ICodeWriteKit writer)
        {
            writer.WriteLine(line);
        }
    }
}
