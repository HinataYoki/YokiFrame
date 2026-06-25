using System.Text;

namespace YokiFrame
{
    public sealed class StringCodeWriteKit : ICodeWriteKit
    {
        private static readonly string[] IndentCache = GenerateIndentCache(16);
        private readonly StringBuilder builder;
        private int indentCount;

        public StringCodeWriteKit(int initialCapacity = 1024)
        {
            builder = new StringBuilder(initialCapacity);
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
            builder.Append(Indent);
            builder.AppendFormat(format, args);
            builder.AppendLine();
        }

        public void WriteLine(string code = null)
        {
            if (string.IsNullOrEmpty(code))
            {
                builder.AppendLine();
                return;
            }

            builder.Append(Indent);
            builder.AppendLine(code);
        }

        public void Clear()
        {
            builder.Clear();
        }

        public override string ToString()
        {
            return builder.ToString();
        }

        public void Dispose()
        {
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
