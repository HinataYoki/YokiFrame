using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 字符串代码写入器 - 用于内存中生成代码或测试
    /// </summary>
    public class StringCodeWriteKit : ICodeWriteKit
    {
        /// <summary>
        /// 预生成的缩进字符串缓存
        /// </summary>
        private static readonly string[] sIndentCache = GenerateIndentCache(16);

        private readonly StringBuilder mBuilder;
        private int mIndentCount;

        /// <summary>
        /// 创建字符串代码写入器
        /// </summary>
        /// <param name="initialCapacity">初始容量</param>
        public StringCodeWriteKit(int initialCapacity = 1024)
        {
            mBuilder = new StringBuilder(initialCapacity);
        }

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
            mBuilder.Append(Indent);
            mBuilder.AppendFormat(format, args);
            mBuilder.AppendLine();
        }

        public void WriteLine(string code = null)
        {
            if (string.IsNullOrEmpty(code))
            {
                mBuilder.AppendLine();
            }
            else
            {
                mBuilder.Append(Indent);
                mBuilder.AppendLine(code);
            }
        }

        /// <summary>
        /// 获取生成的代码字符串
        /// </summary>
        public override string ToString() => mBuilder.ToString();

        /// <summary>
        /// 清空内容
        /// </summary>
        public void Clear() => mBuilder.Clear();

        public void Dispose()
        {
            // StringBuilder 不需要释放
        }

        private static string[] GenerateIndentCache(int maxLevel)
        {
            var cache = new string[maxLevel];
            for (int i = 0; i < maxLevel; i++)
            {
                cache[i] = new string('\t', i);
            }
            return cache;
        }

        private static string GenerateIndent(int level) => new('\t', level);
    }
}
