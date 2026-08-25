using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 当前唯一的运行时设置实现：按 kit/key 保存稀疏字符串。
    /// 宿主 Adapter 负责从 JSON 或 ProjectSettings 填充后注入，本类型不读写磁盘。
    /// </summary>
    public sealed class YokiFrameRuntimeSettingsStore : IKitSettingsStore
    {
        private readonly Dictionary<string, string> mValues = new(StringComparer.Ordinal);

        /// <summary>
        /// 尝试读取指定 Kit 设置；Kit 与 key 必须是安全标识，避免宿主配置形成路径注入。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">设置键。</param>
        /// <param name="value">读取成功时返回设置值。</param>
        /// <returns>存在对应设置时返回 true。</returns>
        public bool TryGetValue(string kit, string key, out string value)
        {
            ValidateIdentifier(kit, nameof(kit));
            ValidateIdentifier(key, nameof(key));
            return mValues.TryGetValue(BuildCompositeKey(kit, key), out value);
        }

        /// <summary>
        /// 写入一项稀疏运行时设置；相同 Kit/key 重复写入时最后值生效。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">设置键。</param>
        /// <param name="value">设置值；null 按空字符串保存。</param>
        public void SetValue(string kit, string key, string value)
        {
            ValidateIdentifier(kit, nameof(kit));
            ValidateIdentifier(key, nameof(key));
            mValues[BuildCompositeKey(kit, key)] = value ?? string.Empty;
        }

        /// <summary>
        /// 移除指定运行时设置，使 Kit 后续读取回退到自身代码默认值。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">设置键。</param>
        public void RemoveValue(string kit, string key)
        {
            ValidateIdentifier(kit, nameof(kit));
            ValidateIdentifier(key, nameof(key));
            mValues.Remove(BuildCompositeKey(kit, key));
        }

        /// <summary>
        /// 清空当前 Store，供测试、重新加载项目配置或宿主关闭时使用。
        /// </summary>
        public void Clear()
        {
            mValues.Clear();
        }

        /// <summary>
        /// 校验 Kit 和 key 仅使用安全 ASCII，确保所有宿主遵守相同标识规则。
        /// </summary>
        /// <param name="value">待校验标识。</param>
        /// <param name="parameterName">异常参数名。</param>
        internal static void ValidateIdentifier(string value, string parameterName)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128 || value == "." || value == "..")
            {
                throw new ArgumentException("Kit setting identifiers must be 1-128 safe ASCII characters.", parameterName);
            }

            for (var index = 0; index < value.Length; index++)
            {
                if (!IsSafeIdentifierCharacter(value[index]))
                {
                    throw new ArgumentException("Kit setting identifiers must be safe ASCII characters.", parameterName);
                }
            }
        }

        /// <summary>
        /// 判断字符能否用于跨宿主设置标识；该集合不包含任何路径分隔符。
        /// </summary>
        /// <param name="value">待判断字符。</param>
        /// <returns>属于允许字符集合时返回 true。</returns>
        private static bool IsSafeIdentifierCharacter(char value)
        {
            return (value >= 'a' && value <= 'z')
                   || (value >= 'A' && value <= 'Z')
                   || (value >= '0' && value <= '9')
                   || value == '.'
                   || value == '_'
                   || value == '-';
        }

        /// <summary>
        /// 构造只在内存字典使用的复合键；输入已通过标识校验，不会包含分隔符。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">设置键。</param>
        /// <returns>稳定复合键。</returns>
        private static string BuildCompositeKey(string kit, string key)
        {
            return kit + "/" + key;
        }
    }
}
