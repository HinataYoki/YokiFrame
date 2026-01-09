using System.Collections.Generic;

namespace YokiFrame
{
    public class RootCode : ICodeScope
    {
        public List<ICode> Codes { get; set; } = new();

        public void Gen(ICodeWriteKit writer)
        {
            foreach (var code in Codes)
            {
                code.Gen(writer);
            }
        }
    }
}