using System.Collections.Generic;

namespace YokiFrame
{
    public sealed class RootCode : ICodeScope
    {
        public List<ICode> Codes { get; set; } = new List<ICode>();

        public void Gen(ICodeWriteKit writer)
        {
            for (int i = 0; i < Codes.Count; i++)
            {
                Codes[i].Gen(writer);
            }
        }
    }
}
