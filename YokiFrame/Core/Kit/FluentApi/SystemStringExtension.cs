using System.Text;

namespace YokiFrame
{
    public static class SystemStringExtension
    {
        /// <summary>
        /// 返回包含此字符串的 StringBuilder
        /// </summary>
        public static StringBuilder Builder(this string selfStr)
        {
            return new StringBuilder(selfStr);
        }

        /// <summary>
        /// StringBuilder 添加前缀
        /// </summary>
        public static StringBuilder AddPrefix(this StringBuilder self, string prefixString)
        {
            self.Insert(0, prefixString);
            return self;
        }
    }

}