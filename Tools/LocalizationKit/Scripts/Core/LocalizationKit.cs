using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 本地化系统静态入口类
    /// 提供文本获取、语言切换、格式化等功能
    /// </summary>
    public static class LocalizationKit
    {
        #region 配置字段

        /// <summary>
        /// 数据提供者
        /// </summary>
        private static ILocalizationProvider sProvider;

        /// <summary>
        /// 文本格式化器
        /// </summary>
        private static ITextFormatter sFormatter = new DefaultTextFormatter();

        /// <summary>
        /// 当前语言
        /// </summary>
        private static LanguageId sCurrentLanguage = LanguageId.ChineseSimplified;

        /// <summary>
        /// 默认语言（fallback）
        /// </summary>
        private static LanguageId sDefaultLanguage = LanguageId.ChineseSimplified;

        /// <summary>
        /// 文本缓存 (languageId, textId) -> text
        /// </summary>
        private static readonly Dictionary<(LanguageId, int), string> sTextCache = new(256);

        /// <summary>
        /// 复数文本缓存 (languageId, textId, category) -> text
        /// </summary>
        private static readonly Dictionary<(LanguageId, int, PluralCategory), string> sPluralCache = new(64);

        /// <summary>
        /// UI 绑定器集合
        /// </summary>
        private static readonly HashSet<ILocalizationBinder> sBinders = new();

        /// <summary>
        /// 语言切换事件
        /// </summary>
        public static event Action<LanguageId> OnLanguageChanged;

        #endregion

        #region 配置方法

        /// <summary>
        /// 设置数据提供者
        /// </summary>
        public static void SetProvider(ILocalizationProvider provider)
        {
            sProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            ClearCache();
            KitLogger.Log($"[LocalizationKit] Provider 已设置: {provider.GetType().Name}");
        }

        /// <summary>
        /// 获取当前数据提供者
        /// </summary>
        public static ILocalizationProvider GetProvider() => sProvider;

        /// <summary>
        /// 设置文本格式化器
        /// </summary>
        public static void SetFormatter(ITextFormatter formatter)
        {
            sFormatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
            KitLogger.Log($"[LocalizationKit] Formatter 已设置: {formatter.GetType().Name}");
        }

        /// <summary>
        /// 获取当前格式化器
        /// </summary>
        public static ITextFormatter GetFormatter() => sFormatter;

        /// <summary>
        /// 设置默认语言（fallback）
        /// </summary>
        public static void SetDefaultLanguage(LanguageId languageId)
        {
            sDefaultLanguage = languageId;
            KitLogger.Log($"[LocalizationKit] 默认语言已设置: {languageId}");
        }

        /// <summary>
        /// 获取默认语言
        /// </summary>
        public static LanguageId GetDefaultLanguage() => sDefaultLanguage;

        #endregion

        #region 语言管理

        /// <summary>
        /// 设置当前语言
        /// </summary>
        /// <param name="languageId">语言标识符</param>
        /// <returns>是否成功切换</returns>
        public static bool SetLanguage(LanguageId languageId)
        {
            if (sCurrentLanguage == languageId)
                return true;

            // 验证语言是否支持
            if (sProvider != null)
            {
                var supported = sProvider.GetSupportedLanguages();
                var isSupported = false;
                for (int i = 0; i < supported.Count; i++)
                {
                    if (supported[i] == languageId)
                    {
                        isSupported = true;
                        break;
                    }
                }

                if (!isSupported)
                {
                    KitLogger.Warning($"[LocalizationKit] 语言不支持: {languageId}");
                    return false;
                }
            }

            var oldLanguage = sCurrentLanguage;
            sCurrentLanguage = languageId;

            // 清除缓存
            ClearCache();

            // 通知所有绑定器
            NotifyBinders();

            // 触发事件
            OnLanguageChanged?.Invoke(languageId);

            KitLogger.Log($"[LocalizationKit] 语言已切换: {oldLanguage} -> {languageId}");
            return true;
        }

        /// <summary>
        /// 获取当前语言
        /// </summary>
        public static LanguageId GetCurrentLanguage() => sCurrentLanguage;

        /// <summary>
        /// 获取支持的语言列表
        /// </summary>
        public static IReadOnlyList<LanguageId> GetAvailableLanguages()
        {
            return sProvider?.GetSupportedLanguages() ?? Array.Empty<LanguageId>();
        }

        /// <summary>
        /// 获取语言信息
        /// </summary>
        public static LanguageInfo GetLanguageInfo(LanguageId languageId)
        {
            return sProvider?.GetLanguageInfo(languageId) ?? LanguageInfo.Empty;
        }

        /// <summary>
        /// 检查语言是否已加载
        /// </summary>
        public static bool IsLanguageLoaded(LanguageId languageId)
        {
            return sProvider?.IsLanguageLoaded(languageId) ?? false;
        }

        #endregion

        #region 文本获取

        /// <summary>
        /// 获取本地化文本
        /// </summary>
        /// <param name="textId">文本ID</param>
        /// <returns>本地化文本，未找到时返回调试字符串</returns>
        public static string Get(int textId)
        {
            return GetInternal(sCurrentLanguage, textId);
        }

        /// <summary>
        /// 获取指定语言的本地化文本
        /// </summary>
        public static string Get(LanguageId languageId, int textId)
        {
            return GetInternal(languageId, textId);
        }

        /// <summary>
        /// 获取带参数的本地化文本
        /// </summary>
        public static string Get(int textId, params object[] args)
        {
            var template = GetInternal(sCurrentLanguage, textId);
            if (args == null || args.Length == 0)
                return template;

            return sFormatter.Format(template, args);
        }

        /// <summary>
        /// 获取带命名参数的本地化文本
        /// </summary>
        public static string Get(int textId, IReadOnlyDictionary<string, object> args)
        {
            var template = GetInternal(sCurrentLanguage, textId);
            if (args == null || args.Count == 0)
                return template;

            return sFormatter.Format(template, args);
        }

        /// <summary>
        /// 获取复数形式文本
        /// </summary>
        /// <param name="textId">文本ID</param>
        /// <param name="count">数量</param>
        /// <returns>复数形式文本</returns>
        public static string GetPlural(int textId, int count)
        {
            var category = PluralRuleFactory.GetCategory(sCurrentLanguage, count);
            return GetPluralInternal(sCurrentLanguage, textId, category, count);
        }

        /// <summary>
        /// 获取复数形式文本（带额外参数）
        /// </summary>
        public static string GetPlural(int textId, int count, params object[] extraArgs)
        {
            var category = PluralRuleFactory.GetCategory(sCurrentLanguage, count);
            var template = GetPluralInternal(sCurrentLanguage, textId, category, count);

            // 合并 count 和额外参数
            var allArgs = new object[extraArgs.Length + 1];
            allArgs[0] = count;
            Array.Copy(extraArgs, 0, allArgs, 1, extraArgs.Length);

            return sFormatter.Format(template, allArgs);
        }

        #endregion

        #region 内部方法

        private static string GetInternal(LanguageId languageId, int textId)
        {
            // 先查缓存
            var cacheKey = (languageId, textId);
            if (sTextCache.TryGetValue(cacheKey, out var cached))
                return cached;

            // 从 Provider 获取
            if (sProvider != null && sProvider.TryGetText(languageId, textId, out var text))
            {
                sTextCache[cacheKey] = text;
                return text;
            }

            // Fallback 到默认语言
            if (languageId != sDefaultLanguage)
            {
                var fallbackKey = (sDefaultLanguage, textId);
                if (sTextCache.TryGetValue(fallbackKey, out var fallbackCached))
                    return fallbackCached;

                if (sProvider != null && sProvider.TryGetText(sDefaultLanguage, textId, out var fallbackText))
                {
                    sTextCache[fallbackKey] = fallbackText;
                    return fallbackText;
                }
            }

            // 返回调试字符串
            return $"[Missing:{textId}]";
        }

        private static string GetPluralInternal(LanguageId languageId, int textId, PluralCategory category, int count)
        {
            // 先查缓存
            var cacheKey = (languageId, textId, category);
            if (sPluralCache.TryGetValue(cacheKey, out var cached))
                return cached;

            // 从 Provider 获取
            if (sProvider != null && sProvider.TryGetPluralText(languageId, textId, category, out var text))
            {
                sPluralCache[cacheKey] = text;
                return text;
            }

            // Fallback 到默认语言
            if (languageId != sDefaultLanguage)
            {
                var fallbackKey = (sDefaultLanguage, textId, category);
                if (sPluralCache.TryGetValue(fallbackKey, out var fallbackCached))
                    return fallbackCached;

                if (sProvider != null && sProvider.TryGetPluralText(sDefaultLanguage, textId, category, out var fallbackText))
                {
                    sPluralCache[fallbackKey] = fallbackText;
                    return fallbackText;
                }
            }

            // 返回调试字符串
            return $"[Missing:{textId}:{category}]";
        }

        /// <summary>
        /// 清除文本缓存
        /// </summary>
        public static void ClearCache()
        {
            sTextCache.Clear();
            sPluralCache.Clear();
        }

        #endregion

        #region 绑定器管理

        /// <summary>
        /// 注册绑定器
        /// </summary>
        public static void RegisterBinder(ILocalizationBinder binder)
        {
            if (binder == null) return;
            sBinders.Add(binder);
        }

        /// <summary>
        /// 注销绑定器
        /// </summary>
        public static void UnregisterBinder(ILocalizationBinder binder)
        {
            if (binder == null) return;
            sBinders.Remove(binder);
        }

        /// <summary>
        /// 通知所有绑定器刷新
        /// </summary>
        private static void NotifyBinders()
        {
            foreach (var binder in sBinders)
            {
                if (binder.IsValid)
                {
                    binder.Refresh();
                }
            }
        }

        /// <summary>
        /// 获取绑定器数量
        /// </summary>
        public static int GetBinderCount() => sBinders.Count;

        #endregion

        #region 预加载

        /// <summary>
        /// 预加载指定语言
        /// </summary>
        public static void PreloadLanguage(LanguageId languageId)
        {
            sProvider?.PreloadLanguage(languageId);
        }

        /// <summary>
        /// 卸载指定语言
        /// </summary>
        public static void UnloadLanguage(LanguageId languageId)
        {
            sProvider?.UnloadLanguage(languageId);

            // 清除该语言的缓存
            var keysToRemove = new List<(LanguageId, int)>();
            foreach (var key in sTextCache.Keys)
            {
                if (key.Item1 == languageId)
                    keysToRemove.Add(key);
            }
            foreach (var key in keysToRemove)
            {
                sTextCache.Remove(key);
            }

            var pluralKeysToRemove = new List<(LanguageId, int, PluralCategory)>();
            foreach (var key in sPluralCache.Keys)
            {
                if (key.Item1 == languageId)
                    pluralKeysToRemove.Add(key);
            }
            foreach (var key in pluralKeysToRemove)
            {
                sPluralCache.Remove(key);
            }
        }

        #endregion

        #region 重置（测试用）

        /// <summary>
        /// 重置所有配置（仅用于测试）
        /// </summary>
        public static void Reset()
        {
            sProvider = null;
            sFormatter = new DefaultTextFormatter();
            sCurrentLanguage = LanguageId.ChineseSimplified;
            sDefaultLanguage = LanguageId.ChineseSimplified;
            sTextCache.Clear();
            sPluralCache.Clear();
            sBinders.Clear();
            OnLanguageChanged = null;
        }

        #endregion
    }
}
