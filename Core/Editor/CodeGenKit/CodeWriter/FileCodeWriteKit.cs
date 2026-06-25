using System.IO;
using System.Text;

namespace YokiFrame
{
    public sealed class FileCodeWriteKit : ICodeWriteKit
    {
        private static readonly string[] IndentCache = GenerateIndentCache(16);
        private readonly StreamWriter writer;
        private int indentCount;

        public FileCodeWriteKit(string filePath)
            : this(CreateWriter(filePath))
        {
        }

        public FileCodeWriteKit(StreamWriter writer)
        {
            this.writer = writer;
        }

        public int IndentCount
        {
            get { return indentCount; }
            set { indentCount = value < 0 ? 0 : value; }
        }

        private string Indent
        {
            get { return indentCount < IndentCache.Length ? IndentCache[indentCount] : new string('\t', indentCount); }
        }

        public void WriteFormatLine(string format, params object[] args)
        {
            writer.Write(Indent);
            writer.WriteLine(format, args);
        }

        public void WriteLine(string code = null)
        {
            if (string.IsNullOrEmpty(code))
            {
                writer.WriteLine();
                return;
            }

            writer.Write(Indent);
            writer.WriteLine(code);
        }

        public void Dispose()
        {
            writer.Dispose();
        }

        private static StreamWriter CreateWriter(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return new StreamWriter(filePath, false, new UTF8Encoding(false));
        }

        private static string[] GenerateIndentCache(int maxLevel)
        {
            string[] cache = new string[maxLevel];
            for (int i = 0; i < cache.Length; i++)
            {
                cache[i] = new string('\t', i);
            }

            return cache;
        }
    }
}
