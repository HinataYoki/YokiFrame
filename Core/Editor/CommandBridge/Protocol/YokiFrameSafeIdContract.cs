#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 定义所有会进入协议路径片段的安全 ID 规则。
    /// </summary>
    public static class YokiFrameSafeIdContract
    {
        /// <summary>
        /// 安全 ID 允许的最大字符数。
        /// </summary>
        public const int MAX_LENGTH = 128;

        /// <summary>
        /// 判断标识是否满足长度、点边界、穿越和 ASCII 白名单约束。
        /// </summary>
        /// <param name="value">待检查标识。</param>
        /// <returns>可安全用于单个路径片段时返回 true。</returns>
        public static bool IsSafeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MAX_LENGTH)
            {
                return false;
            }

            if (value[0] == '.' || value[value.Length - 1] == '.' || value.Contains(".."))
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                if (!IsSafeIdCharacter(value[index]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 判断字符是否属于安全 ID 的 ASCII 字母、数字、连字符、下划线或点。
        /// </summary>
        /// <param name="character">待检查字符。</param>
        /// <returns>字符属于白名单时返回 true。</returns>
        private static bool IsSafeIdCharacter(char character)
        {
            return character >= 'a' && character <= 'z'
                || character >= 'A' && character <= 'Z'
                || character >= '0' && character <= '9'
                || character == '-'
                || character == '_'
                || character == '.';
        }
    }
}
#endif
