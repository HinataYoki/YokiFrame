using System;
using System.Text;

namespace YokiFrame
{
    public static class SystemStringExtension
    {
        #region 空值判断

        public static bool IsNullOrEmpty(this string self) => string.IsNullOrEmpty(self);
        public static bool IsNotNullOrEmpty(this string self) => !string.IsNullOrEmpty(self);
        public static bool IsNullOrWhiteSpace(this string self) => string.IsNullOrWhiteSpace(self);
        public static bool IsNotNullOrWhiteSpace(this string self) => !string.IsNullOrWhiteSpace(self);

        #endregion

        #region StringBuilder

        public static StringBuilder Builder(this string selfStr) => new(selfStr);

        public static StringBuilder Builder(this string selfStr, int capacity)
        {
            var sb = new StringBuilder(capacity);
            sb.Append(selfStr);
            return sb;
        }

        public static StringBuilder AddPrefix(this StringBuilder self, string prefixString)
        {
            self.Insert(0, prefixString);
            return self;
        }

        public static StringBuilder AddSuffix(this StringBuilder self, string suffixString)
        {
            self.Append(suffixString);
            return self;
        }

        public static StringBuilder AppendLineEx(this StringBuilder self, string value)
        {
            self.AppendLine(value);
            return self;
        }

        #endregion

        #region 格式化

        public static string Format(this string self, params object[] args) => string.Format(self, args);

        public static string UpperFirst(this string self)
        {
            if (string.IsNullOrEmpty(self)) return self;
            char first = self[0];
            if (char.IsUpper(first)) return self;
            return string.Create(self.Length, self, static (span, str) =>
            {
                span[0] = char.ToUpper(str[0]);
                str.AsSpan(1).CopyTo(span[1..]);
            });
        }

        public static string LowerFirst(this string self)
        {
            if (string.IsNullOrEmpty(self)) return self;
            char first = self[0];
            if (char.IsLower(first)) return self;
            return string.Create(self.Length, self, static (span, str) =>
            {
                span[0] = char.ToLower(str[0]);
                str.AsSpan(1).CopyTo(span[1..]);
            });
        }

        #endregion

        #region 截取与填充

        public static string SafeSubstring(this string self, int startIndex, int length)
        {
            if (string.IsNullOrEmpty(self)) return string.Empty;
            if (startIndex >= self.Length) return string.Empty;
            if (startIndex + length > self.Length) length = self.Length - startIndex;
            return self.Substring(startIndex, length);
        }

        public static string RemoveSuffix(this string self, string suffix)
        {
            if (string.IsNullOrEmpty(self) || string.IsNullOrEmpty(suffix)) return self;
            return self.EndsWith(suffix) ? self[..^suffix.Length] : self;
        }

        public static string RemovePrefix(this string self, string prefix)
        {
            if (string.IsNullOrEmpty(self) || string.IsNullOrEmpty(prefix)) return self;
            return self.StartsWith(prefix) ? self[prefix.Length..] : self;
        }

        #endregion
    }
}
