using System.Collections.Generic;

namespace YokiFrame
{
    public interface ICodeScope : ICode
    {
        List<ICode> Codes { get; set; }
    }
}