using System.Collections.Generic;

namespace YokiFrame
{
    public abstract class CodeScope : ICodeScope
    {
        public bool Semicolon { get; set; }

        public List<ICode> Codes { get; set; } = new();

        public void Gen(ICodeWriteKit writer)
        {
            GenFirstLine(writer);

            new OpenBraceCode().Gen(writer);

            writer.IndentCount++;

            foreach (var code in Codes)
            {
                code.Gen(writer);
            }

            writer.IndentCount--;

            new CloseBraceCode(Semicolon).Gen(writer);
        }

        protected abstract void GenFirstLine(ICodeWriteKit codeWriter);
    }
}