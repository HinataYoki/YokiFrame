using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// Godot ProjectSettings Runtime 键的唯一转换规则。
    /// Unity JSON 与 KitSettings 使用 camelCase；Godot section 使用 snake_case。
    /// 加载器和工具链必须共用本规则，禁止再各自维护键表。
    /// </summary>
    public static class GodotRuntimeSettingKeys
    {
        /// <summary>
        /// 把 camelCase 设置键转换为 Godot ProjectSettings 使用的 snake_case。
        /// 连续大写按一个词处理，例如 maxFileSizeMB 变为 max_file_size_mb。
        /// </summary>
        /// <param name="camelCaseKey">KitSettings 使用的 camelCase 键。</param>
        /// <returns>Godot Runtime section 键；空值原样返回。</returns>
        public static string ToSnakeCase(string camelCaseKey)
        {
            if (string.IsNullOrEmpty(camelCaseKey))
            {
                return camelCaseKey;
            }

            StringBuilder builder = new StringBuilder(camelCaseKey.Length + 8);
            for (int index = 0; index < camelCaseKey.Length; index++)
            {
                char current = camelCaseKey[index];
                if (IsAsciiUpper(current) && index > 0 && !IsAsciiUpper(camelCaseKey[index - 1]))
                {
                    builder.Append('_');
                }

                builder.Append(ToAsciiLower(current));
            }

            return builder.ToString();
        }

        /// <summary>
        /// 把 Godot snake_case 键还原为 KitSettings 使用的 camelCase。
        /// 没有下划线的键保持原样，避免误改已经是 camelCase 的值。
        /// </summary>
        /// <param name="snakeCaseKey">Godot Runtime section 键。</param>
        /// <returns>camelCase 键；空值原样返回。</returns>
        public static string ToCamelCase(string snakeCaseKey)
        {
            if (string.IsNullOrEmpty(snakeCaseKey) || snakeCaseKey.IndexOf('_') < 0)
            {
                return snakeCaseKey;
            }

            StringBuilder builder = new StringBuilder(snakeCaseKey.Length);
            bool upperNext = false;
            for (int index = 0; index < snakeCaseKey.Length; index++)
            {
                char current = snakeCaseKey[index];
                if (current == '_')
                {
                    upperNext = builder.Length > 0;
                    continue;
                }

                builder.Append(upperNext ? ToAsciiUpper(current) : current);
                upperNext = false;
            }

            return builder.ToString();
        }

        /// <summary>判断字符是否为 ASCII 大写字母。设置键不允许依赖区域性大小写规则。</summary>
        /// <param name="value">待判断字符。</param>
        /// <returns>A-Z 时返回 true。</returns>
        private static bool IsAsciiUpper(char value)
        {
            return value >= 'A' && value <= 'Z';
        }

        /// <summary>把 ASCII 大写字母转换为小写，其它字符保持不变。</summary>
        /// <param name="value">待转换字符。</param>
        /// <returns>转换后的字符。</returns>
        private static char ToAsciiLower(char value)
        {
            return IsAsciiUpper(value) ? (char)(value + ('a' - 'A')) : value;
        }

        /// <summary>把 ASCII 小写字母转换为大写，其它字符保持不变。</summary>
        /// <param name="value">待转换字符。</param>
        /// <returns>转换后的字符。</returns>
        private static char ToAsciiUpper(char value)
        {
            return value >= 'a' && value <= 'z' ? (char)(value - ('a' - 'A')) : value;
        }
    }
}
