using System.IO;
using System.Text;

namespace YokiFrame
{
    public class FileCodeWriter : ICodeWriter
    {
        private readonly StreamWriter Writer;
        public FileCodeWriter(StreamWriter writer) => Writer = writer;

        public int IndentCount { get; set; }

        private string Indent
        {
            get
            {
                var builder = new StringBuilder();

                for (var i = 0; i < IndentCount; i++)
                {
                    builder.Append("\t");
                }

                return builder.ToString();
            }
        }

        public void WriteFormatLine(string format, params object[] args)
        {
            Writer.WriteLine(Indent + format, args);
        }

        public void WriteLine(string code = null)
        {
            Writer.WriteLine(Indent + code);
        }

        public void Dispose()
        {
            Writer?.Dispose();
        }
    }
}