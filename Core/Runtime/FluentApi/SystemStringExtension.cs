using System;
using System.Text;

namespace YokiFrame
{
    public static class SystemStringExtension
    {
        #region 空值判断
        
        /// <summary>
        /// 判断字符串是否为 null 或空
        /// </summary>
        public static bool IsNullOrEmpty(this string self) => string.IsNullOrEmpty(self);

        /// <summary>
        /// 判断字符串是否不为 null 且不为空
        /// </summary>
        public static bool IsNotNullOrEmpty(this string self) => !string.IsNullOrEmpty(self);

        /// <summary>
        /// 判断字符串是否为 null、空或仅包含空白字符
        /// </summary>
        public static bool IsNullOrWhiteSpace(this string self) => string.IsNullOrWhiteSpace(self);

        /// <summary>
        /// 判断字符串是否不为 null、不为空且不仅包含空白字符
        /// </summary>
        public static bool IsNotNullOrWhiteSpace(this string self) => !string.IsNullOrWhiteSpace(self);

        #endregion

        #region StringBuilder
        
        /// <summary>
        /// 返回包含此字符串的 StringBuilder
        /// </summary>
        public static StringBuilder Builder(this string selfStr) => new(selfStr);

        /// <summary>
        /// 返回指定容量的 StringBuilder 并追加此字符串
        /// </summary>
        public static StringBuilder Builder(this string selfStr, int capacity)
        {
            var sb = new StringBuilder(capacity);
            sb.Append(selfStr);
            return sb;
        }

        /// <summary>
        /// StringBuilder 添加前缀
        /// </summary>
        public static StringBuilder AddPrefix(this StringBuilder self, string prefixString)
        {
            self.Insert(0, prefixString);
            return self;
        }

        /// <summary>
        /// StringBuilder 添加后缀
        /// </summary>
        public static StringBuilder AddSuffix(this StringBuilder self, string suffixString)
        {
            self.Append(suffixString);
            return self;
        }

        /// <summary>
        /// StringBuilder 添加换行
        /// </summary>
        public static StringBuilder AppendLineEx(this StringBuilder self, string value)
        {
            self.AppendLine(value);
            return self;
        }

        #endregion

        #region 格式化
        
        /// <summary>
        /// 格式化字符串
        /// </summary>
        public static string Format(this string self, params object[] args) => string.Format(self, args);

        /// <summary>
        /// 首字母大写（零分配优化）
        /// </summary>
        public static string UpperFirst(this string self)
        {
            if (string.IsNullOrEmpty(self)) return self;
            
            char first = self[0];
            if (char.IsUpper(first)) return self;
            
            // 使用 string.Create 避免额外分配
            return string.Create(self.Length, self, static (span, str) =>
            {
                span[0] = char.ToUpper(str[0]);
                str.AsSpan(1).CopyTo(span[1..]);
            });
        }

        /// <summary>
        /// 首字母小写（零分配优化）
        /// </summary>
        public static string LowerFirst(this string self)
        {
            if (string.IsNullOrEmpty(self)) return self;
            
            char first = self[0];
            if (char.IsLower(first)) return self;
            
            // 使用 string.Create 避免额外分配
            return string.Create(self.Length, self, static (span, str) =>
            {
                span[0] = char.ToLower(str[0]);
                str.AsSpan(1).CopyTo(span[1..]);
            });
        }

        #endregion

        #region 截取与填充
        
        /// <summary>
        /// 安全截取字符串（不会抛出越界异常）
        /// </summary>
        public static string SafeSubstring(this string self, int startIndex, int length)
        {
            if (string.IsNullOrEmpty(self)) return string.Empty;
            if (startIndex >= self.Length) return string.Empty;
            if (startIndex + length > self.Length) length = self.Length - startIndex;
            return self.Substring(startIndex, length);
        }

        /// <summary>
        /// 移除字符串末尾指定后缀
        /// </summary>
        public static string RemoveSuffix(this string self, string suffix)
        {
            if (string.IsNullOrEmpty(self) || string.IsNullOrEmpty(suffix)) return self;
            return self.EndsWith(suffix) ? self.Substring(0, self.Length - suffix.Length) : self;
        }

        /// <summary>
        /// 移除字符串开头指定前缀
        /// </summary>
        public static string RemovePrefix(this string self, string prefix)
        {
            if (string.IsNullOrEmpty(self) || string.IsNullOrEmpty(prefix)) return self;
            return self.StartsWith(prefix) ? self.Substring(prefix.Length) : self;
        }

        #endregion
    }
}