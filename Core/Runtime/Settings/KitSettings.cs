using System;
using System.Globalization;

namespace YokiFrame
{
    /// <summary>
    /// 定义 Kit 运行时设置存储，宿主可注入持久化实现，Core 默认使用内存存储。
    /// </summary>
    public interface IKitSettingsStore
    {
        /// <summary>
        /// 尝试读取指定 Kit 和 key 的字符串值。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">设置键。</param>
        /// <param name="value">读取成功时返回的设置值。</param>
        /// <returns>存在值时返回 true。</returns>
        bool TryGetValue(string kit, string key, out string value);

        /// <summary>
        /// 写入指定 Kit 和 key 的字符串值。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">设置键。</param>
        /// <param name="value">要保存的设置值。</param>
        void SetValue(string kit, string key, string value);

        /// <summary>
        /// 移除指定 Kit 和 key 的设置值。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">设置键。</param>
        void RemoveValue(string kit, string key);
    }

    /// <summary>
    /// 提供 Core Runtime 可用的 Kit 设置入口，避免各 Kit 直接绑定宿主配置资产。
    /// </summary>
    public static class KitSettings
    {
        private static readonly object sLock = new object();
        private static readonly YokiFrameRuntimeSettingsStore sMemoryStore = new();
        private static Func<IKitSettingsStore> sDefaultStoreFactory;
        private static IKitSettingsStore sStore;
        private static bool sHasExplicitStore;
        private static bool sUsingMemoryFallback;

        /// <summary>
        /// 注册当前宿主的默认设置 Store 工厂；只记录工厂，首次设置访问时才创建 Store。
        /// 已通过 <see cref="SetStore"/> 显式注入的 Store 始终优先。
        /// </summary>
        /// <param name="factory">创建当前宿主设置 Store 的工厂，返回值不能为 null。</param>
        public static void RegisterDefaultStoreFactory(Func<IKitSettingsStore> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            lock (sLock)
            {
                sDefaultStoreFactory = factory;
                if (!sHasExplicitStore && sUsingMemoryFallback)
                {
                    sStore = null;
                    sUsingMemoryFallback = false;
                }
            }
        }

        /// <summary>
        /// 注入宿主设置存储；传入 null 时恢复到内存存储。
        /// </summary>
        /// <param name="store">宿主设置存储。</param>
        public static void SetStore(IKitSettingsStore store)
        {
            lock (sLock)
            {
                sStore = store ?? sMemoryStore;
                sHasExplicitStore = store != null;
                sUsingMemoryFallback = store == null;
            }
        }

        /// <summary>
        /// 清空内存设置并恢复默认内存存储，主要用于测试和新会话初始化。
        /// </summary>
        public static void Reset()
        {
            lock (sLock)
            {
                sMemoryStore.Clear();
                sDefaultStoreFactory = null;
                sStore = null;
                sHasExplicitStore = false;
                sUsingMemoryFallback = false;
            }
        }

        /// <summary>
        /// 尝试读取指定 Kit 设置字符串。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">设置键。</param>
        /// <param name="value">读取成功时返回的字符串值。</param>
        /// <returns>存在值时返回 true。</returns>
        public static bool TryGetString(string kit, string key, out string value)
        {
            value = null;
            ValidateKey(kit, nameof(kit));
            ValidateKey(key, nameof(key));
            lock (sLock)
            {
                return GetStoreLocked().TryGetValue(kit, key, out value);
            }
        }

        /// <summary>
        /// 读取指定 Kit 设置字符串，不存在时返回默认值。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">设置键。</param>
        /// <param name="defaultValue">设置不存在或无效时的默认值。</param>
        /// <returns>设置字符串。</returns>
        public static string GetString(string kit, string key, string defaultValue)
        {
            string value;
            return TryGetString(kit, key, out value) ? value : defaultValue;
        }

        /// <summary>
        /// 读取指定 Kit 设置布尔值，兼容 true/false、1/0 和 yes/no。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">设置键。</param>
        /// <param name="defaultValue">设置不存在或无效时的默认值。</param>
        /// <returns>设置布尔值。</returns>
        public static bool GetBool(string kit, string key, bool defaultValue)
        {
            string value;
            if (!TryGetString(kit, key, out value))
            {
                return defaultValue;
            }

            return ParseBool(value, defaultValue);
        }

        /// <summary>
        /// 读取指定 Kit 设置整数值。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">设置键。</param>
        /// <param name="defaultValue">设置不存在或无效时的默认值。</param>
        /// <returns>设置整数值。</returns>
        public static int GetInt(string kit, string key, int defaultValue)
        {
            string value;
            int parsed;
            return TryGetString(kit, key, out value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : defaultValue;
        }

        /// <summary>
        /// 写入指定 Kit 设置字符串。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">设置键。</param>
        /// <param name="value">要保存的字符串值；null 会保存为空字符串。</param>
        public static void SetString(string kit, string key, string value)
        {
            ValidateKey(kit, nameof(kit));
            ValidateKey(key, nameof(key));
            lock (sLock)
            {
                GetStoreLocked().SetValue(kit, key, value ?? string.Empty);
            }
        }

        /// <summary>
        /// 写入指定 Kit 设置布尔值。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">设置键。</param>
        /// <param name="value">要保存的布尔值。</param>
        public static void SetBool(string kit, string key, bool value)
        {
            SetString(kit, key, value ? "true" : "false");
        }

        /// <summary>
        /// 写入指定 Kit 设置整数值。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">设置键。</param>
        /// <param name="value">要保存的整数值。</param>
        public static void SetInt(string kit, string key, int value)
        {
            SetString(kit, key, value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 移除指定 Kit 设置。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">设置键。</param>
        public static void Remove(string kit, string key)
        {
            ValidateKey(kit, nameof(kit));
            ValidateKey(key, nameof(key));
            lock (sLock)
            {
                GetStoreLocked().RemoveValue(kit, key);
            }
        }

        /// <summary>
        /// 在锁内解析当前有效 Store；宿主工厂只调用一次，未注册宿主时使用 Core 内存实现。
        /// </summary>
        /// <returns>当前会话唯一的设置 Store。</returns>
        private static IKitSettingsStore GetStoreLocked()
        {
            if (sStore != null)
            {
                return sStore;
            }

            if (sDefaultStoreFactory == null)
            {
                sStore = sMemoryStore;
                sUsingMemoryFallback = true;
                return sStore;
            }

            sStore = sDefaultStoreFactory();
            if (sStore == null)
            {
                throw new InvalidOperationException("The default Kit settings Store factory returned null.");
            }

            sUsingMemoryFallback = false;
            return sStore;
        }

        /// <summary>
        /// 解析宽松布尔字符串。
        /// </summary>
        /// <param name="value">原始字符串。</param>
        /// <param name="defaultValue">无法解析时的默认值。</param>
        /// <returns>解析后的布尔值。</returns>
        private static bool ParseBool(string value, bool defaultValue)
        {
            bool parsed;
            if (bool.TryParse(value, out parsed))
            {
                return parsed;
            }

            if (string.Equals(value, "1", StringComparison.Ordinal) || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(value, "0", StringComparison.Ordinal) || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase)
                ? false
                : defaultValue;
        }

        /// <summary>
        /// 校验 Kit 和 key 只使用安全 ASCII，避免宿主持久化时出现路径或协议注入。
        /// </summary>
        /// <param name="value">待校验标识。</param>
        /// <param name="parameterName">参数名，用于异常提示。</param>
        private static void ValidateKey(string value, string parameterName)
        {
            YokiFrameRuntimeSettingsStore.ValidateIdentifier(value, parameterName);
        }
    }
}
