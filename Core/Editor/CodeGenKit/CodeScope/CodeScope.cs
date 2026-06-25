using System.Collections.Generic;

namespace YokiFrame
{
    public abstract class CodeScope : ICodeScope
    {
        public bool Semicolon { get; set; }

        public List<ICode> Codes { get; set; } = new List<ICode>();

        public void Gen(ICodeWriteKit writer)
        {
            GenFirstLine(writer);
            new OpenBraceCode().Gen(writer);
            writer.IndentCount++;

            for (int i = 0; i < Codes.Count; i++)
            {
                Codes[i].Gen(writer);
            }

            writer.IndentCount--;
            new CloseBraceCode(Semicolon).Gen(writer);
        }

        protected abstract void GenFirstLine(ICodeWriteKit writer);
    }
}
