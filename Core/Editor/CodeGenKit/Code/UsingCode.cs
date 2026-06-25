namespace YokiFrame
{
    public sealed class UsingCode : ICode
    {
        private readonly string namespaceName;

        public UsingCode(string namespaceName)
        {
            this.namespaceName = namespaceName;
        }

        public void Gen(ICodeWriteKit writer)
        {
            writer.WriteFormatLine("using {0};", namespaceName);
        }
    }
}
