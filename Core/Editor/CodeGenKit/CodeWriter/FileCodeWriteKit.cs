using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 文件代码写入器
    /// </summary>
    public class FileCodeWriteKit : ICodeWriteKit
    {
        /// <summary>
        /// 预生成的缩进字符串缓存（最多支持 16 层缩进）
        /// </summary>
        private static readonly string[] sIndentCache = GenerateIndentCache(16);

        private readonly StreamWriter mWriter;
        private int mIndentCount;

        public FileCodeWriteKit(StreamWriter writer) => mWriter = writer;

        public int IndentCount
        {
            get => mIndentCount;
            set => mIndentCount = value < 0 ? 0 : value;
        }

        /// <summary>
        /// 获取当前缩进字符串
        /// </summary>
        private string Indent => mIndentCount < sIndentCache.Length
            ? sIndentCache[mIndentCount]
            : GenerateIndent(mIndentCount);

        public void WriteFormatLine(string format, params object[] args)
        {
            mWriter.Write(Indent);
            mWriter.WriteLine(format, args);
        }

        public void WriteLine(string code = null)
        {
            if (string.IsNullOrEmpty(code))
            {
                mWriter.WriteLine();
            }
            else
            {
                mWriter.Write(Indent);
                mWriter.WriteLine(code);
            }
        }

        public void Dispose()
        {
            mWriter?.Dispose();
        }

        /// <summary>
        /// 生成缩进缓存数组
        /// </summary>
        private static string[] GenerateIndentCache(int maxLevel)
        {
            var cache = new string[maxLevel];
            for (int i = 0; i < maxLevel; i++)
            {
                cache[i] = new string('\t', i);
            }
            return cache;
        }

        /// <summary>
        /// 生成超出缓存范围的缩进字符串
        /// </summary>
        private static string GenerateIndent(int level) => new('\t', level);
    }
}