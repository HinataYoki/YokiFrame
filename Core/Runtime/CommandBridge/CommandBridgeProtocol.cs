namespace YokiFrame
{
    /// <summary>
    /// 文件桥协议标识符校验工具。
    /// </summary>
    public static class CommandBridgeProtocol
    {
        /// <summary>
        /// 获取 requestId、engineId、source、kit、action 等协议标识符的最大长度。
        /// </summary>
        public const int MAX_IDENTIFIER_LENGTH = 128;

        /// <summary>
        /// 判断字符串是否符合文件桥协议安全标识符规则。
        /// </summary>
        /// <param name="value">待校验的标识符。</param>
        /// <returns>符合规则返回 true，否则返回 false。</returns>
        public static bool IsSafeIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MAX_IDENTIFIER_LENGTH)
                return false;
            if (value == "." || value == "..")
                return false;

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var isLetter = c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z';
                var isDigit = c >= '0' && c <= '9';
                if (isLetter || isDigit || c == '.' || c == '_' || c == '-')
                    continue;

                return false;
            }

            return true;
        }
    }
}
