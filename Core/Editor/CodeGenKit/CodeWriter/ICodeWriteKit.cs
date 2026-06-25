using System;

namespace YokiFrame
{
    public interface ICodeWriteKit : IDisposable
    {
        int IndentCount { get; set; }

        void WriteFormatLine(string format, params object[] args);

        void WriteLine(string code = null);
    }
}
