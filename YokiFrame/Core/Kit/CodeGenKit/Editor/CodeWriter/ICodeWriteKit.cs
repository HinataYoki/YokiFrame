using System;

namespace YokiFrame
{
    public interface ICodeWriteKit : IDisposable
    {
        /// <summary>
        /// 缩进数量
        /// </summary>
        int IndentCount { get; set; }
        void WriteFormatLine(string format, params object[] args);
        void WriteLine(string code = null);
    }
}