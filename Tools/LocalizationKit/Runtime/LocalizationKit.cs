using System;
using System.Buffers;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 提供本地化文本、复数文本、语言切换和绑定刷新入口。
    /// </summary>
    public static class LocalizationKit
    {
        private static ILocalizationProvider sProvider;
        private static ITextFormatter sFormatter = new DefaultTextFormatter();
        private static LanguageId sCurrentLanguage = LanguageId.ChineseSimplified;
        private static LanguageId sDefaultLanguage = LanguageId.ChineseSimplified;

        private static readonly Dictionary<LocalizationTextCacheKey, string> sTextCache = new();
        private static readonly Dictionary<LocalizationPluralCacheKey, string> sPluralCache = new();
        private static readonly HashSet<ILocalizationBinder> sBinders = new();

        /// <summary>
        /// 当前语言成功切换后触发。
        /// </summary>
        public static event Action<LanguageId> OnLanguageChanged;

        /// <summary>
        /// 设置本地化数据提供器，并清空文本缓存。
        /// </summary>
        /// <param name="localizationProvider">本地化数据提供器。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="localizationProvider"/> 为空时抛出。</exception>
        public static void SetProvider(ILocalizationProvider localizationProvider)
        {
            if (localizationProvider == null)
            {
                throw new ArgumentNullException(nameof(localizationProvider));
            }

            sProvider = localizationProvider;
            ClearCache();
        }

        /// <summary>
        /// 获取当前本地化数据提供器。
        /// </summary>
        /// <returns>当前本地化数据提供器；未设置时返回空。</returns>
        public static ILocalizationProvider GetProvider() => sProvider;

        /// <summary>
        /// 设置文本格式化器。
        /// </summary>
        /// <param name="textFormatter">文本格式化器。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="textFormatter"/> 为空时抛出。</exception>
        public static void SetFormatter(ITextFormatter textFormatter)
        {
            if (textFormatter == null)
            {
                throw new ArgumentNullException(nameof(textFormatter));
            }

            sFormatter = textFormatter;
        }

        /// <summary>
        /// 获取当前文本格式化器。
        /// </summary>
        /// <returns>当前文本格式化器。</returns>
        public static ITextFormatter GetFormatter() => sFormatter;

        /// <summary>
        /// 设置文本缺失时使用的默认回退语言。
        /// </summary>
        /// <param name="languageId">默认语言。</param>
        public static void SetDefaultLanguage(LanguageId languageId) => sDefaultLanguage = languageId;

        /// <summary>
        /// 获取当前默认回退语言。
        /// </summary>
        /// <returns>默认回退语言。</returns>
        public static LanguageId GetDefaultLanguage() => sDefaultLanguage;

        /// <summary>
        /// 切换当前语言。
        /// </summary>
        /// <param name="languageId">目标语言。</param>
        /// <returns>语言被接受时返回 true；提供器不支持该语言时返回 false。</returns>
        public static bool SetLanguage(LanguageId languageId)
        {
            if (sCurrentLanguage == languageId)
            {
                return true;
            }

            if (sProvider != null && !IsSupportedLanguage(languageId))
            {
                return false;
            }

            sCurrentLanguage = languageId;
            ClearCache();
            NotifyBinders();

            Action<LanguageId> handler = OnLanguageChanged;
            if (handler != null)
            {
                handler(languageId);
            }

            return true;
        }

        /// <summary>
        /// 获取当前语言。
        /// </summary>
        /// <returns>当前语言。</returns>
        public static LanguageId GetCurrentLanguage() => sCurrentLanguage;

        /// <summary>
        /// 获取当前提供器支持的语言列表。
        /// </summary>
        /// <returns>支持的语言列表；未设置提供器时返回空列表。</returns>
        public static IReadOnlyList<LanguageId> GetAvailableLanguages()
        {
            if (sProvider == null)
            {
                return Array.Empty<LanguageId>();
            }

            return sProvider.GetSupportedLanguages();
        }

        /// <summary>
        /// 获取指定语言的显示信息。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        /// <returns>语言信息；未设置提供器或缺失时返回 <see cref="LanguageInfo.Empty"/>。</returns>
        public static LanguageInfo GetLanguageInfo(LanguageId languageId) =>
            sProvider != null ? sProvider.GetLanguageInfo(languageId) : LanguageInfo.Empty;

        /// <summary>
        /// 判断指定语言是否已加载。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        /// <returns>已加载时返回 true。</returns>
        public static bool IsLanguageLoaded(LanguageId languageId) =>
            sProvider != null && sProvider.IsLanguageLoaded(languageId);

        /// <summary>
        /// 使用当前语言获取文本。
        /// </summary>
        /// <param name="textId">文本编号。</param>
        /// <returns>本地化文本；缺失时返回缺失占位文本。</returns>
        public static string Get(int textId) => GetInternal(sCurrentLanguage, textId);

        /// <summary>
        /// 使用指定语言获取文本。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        /// <param name="textId">文本编号。</param>
        /// <returns>本地化文本；缺失时返回缺失占位文本。</returns>
        public static string Get(LanguageId languageId, int textId) => GetInternal(languageId, textId);

        /// <summary>
        /// 使用当前语言获取文本并按索引参数格式化。
        /// </summary>
        /// <param name="textId">文本编号。</param>
        /// <param name="args">索引格式化参数。</param>
        /// <returns>格式化后的本地化文本。</returns>
        public static string Get(int textId, params object[] args)
        {
            string template = GetInternal(sCurrentLanguage, textId);
            if (args == null || args.Length == 0)
            {
                return template;
            }

            return sFormatter.Format(template, args);
        }

        /// <summary>
        /// 使用当前语言获取文本并按命名参数格式化。
        /// </summary>
        /// <param name="textId">文本编号。</param>
        /// <param name="args">命名格式化参数。</param>
        /// <returns>格式化后的本地化文本。</returns>
        public static string Get(int textId, IReadOnlyDictionary<string, object> args)
        {
            string template = GetInternal(sCurrentLanguage, textId);
            if (args == null || args.Count == 0)
            {
                return template;
            }

            return sFormatter.Format(template, args);
        }

        /// <summary>
        /// 使用当前语言获取复数文本，并把数量作为第一个格式化参数。
        /// </summary>
        /// <param name="textId">文本编号。</param>
        /// <param name="count">数量。</param>
        /// <returns>格式化后的复数文本。</returns>
        public static string GetPlural(int textId, int count)
        {
            PluralCategory category = PluralRuleFactory.GetCategory(sCurrentLanguage, count);
            string template = GetPluralInternal(sCurrentLanguage, textId, category);
            return FormatSingleCount(template, count);
        }

        /// <summary>
        /// 使用当前语言获取复数文本，并把数量和额外参数一起格式化。
        /// </summary>
        /// <param name="textId">文本编号。</param>
        /// <param name="count">数量。</param>
        /// <param name="extraArgs">追加在数量之后的格式化参数。</param>
        /// <returns>格式化后的复数文本。</returns>
        public static string GetPlural(int textId, int count, params object[] extraArgs)
        {
            PluralCategory category = PluralRuleFactory.GetCategory(sCurrentLanguage, count);
            string template = GetPluralInternal(sCurrentLanguage, textId, category);
            if (extraArgs == null || extraArgs.Length == 0)
            {
                return FormatSingleCount(template, count);
            }

            object[] args = new object[extraArgs.Length + 1];
            args[0] = count;
            Array.Copy(extraArgs, 0, args, 1, extraArgs.Length);
            return sFormatter.Format(template, args);
        }

        /// <summary>
        /// 清空文本与复数文本缓存。
        /// </summary>
        public static void ClearCache()
        {
            sTextCache.Clear();
            sPluralCache.Clear();
        }

        /// <summary>
        /// 注册语言切换时需要刷新的绑定对象。
        /// </summary>
        /// <param name="binder">绑定对象。</param>
        public static void RegisterBinder(ILocalizationBinder binder)
        {
            if (binder == null)
            {
                return;
            }

            sBinders.Add(binder);
        }

        /// <summary>
        /// 注销语言绑定对象。
        /// </summary>
        /// <param name="binder">绑定对象。</param>
        public static void UnregisterBinder(ILocalizationBinder binder)
        {
            if (binder == null)
            {
                return;
            }

            sBinders.Remove(binder);
        }

        /// <summary>
        /// 获取当前注册的绑定对象数量。
        /// </summary>
        /// <returns>绑定对象数量。</returns>
        public static int GetBinderCount() => sBinders.Count;

        /// <summary>
        /// 请求提供器预加载指定语言。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        public static void PreloadLanguage(LanguageId languageId)
        {
            if (sProvider != null)
            {
                sProvider.PreloadLanguage(languageId);
            }
        }

        /// <summary>
        /// 请求提供器卸载指定语言，并清理该语言缓存。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        public static void UnloadLanguage(LanguageId languageId)
        {
            if (sProvider != null)
            {
                sProvider.UnloadLanguage(languageId);
            }

            RemoveLanguageCache(languageId);
        }

        internal static LocalizationKitDiagnosticsSnapshot CreateDiagnosticsSnapshot()
        {
            IReadOnlyList<LanguageId> availableLanguages = GetAvailableLanguages();
            var languages = new List<LocalizationLanguageDiagnosticsSnapshot>(availableLanguages.Count);
            for (int i = 0; i < availableLanguages.Count; i++)
            {
                LanguageId languageId = availableLanguages[i];
                LanguageInfo info = GetLanguageInfo(languageId);
                languages.Add(new LocalizationLanguageDiagnosticsSnapshot(
                    languageId,
                    info,
                    IsLanguageLoaded(languageId)));
            }

            // 诊断快照只在 CommandBridge/Tauri 读取当前状态时创建，避免在本地化热路径写文件或序列化。
            return new LocalizationKitDiagnosticsSnapshot(
                sCurrentLanguage,
                sDefaultLanguage,
                languages,
                sProvider != null ? sProvider.GetType().Name : string.Empty,
                sFormatter != null ? sFormatter.GetType().Name : string.Empty,
                sBinders.Count,
                sTextCache.Count,
                sPluralCache.Count);
        }

        /// <summary>
        /// 重置 LocalizationKit 的所有运行时状态。
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

        private static string GetInternal(LanguageId languageId, int textId)
        {
            LocalizationTextCacheKey cacheKey = new(languageId, textId);
            string cached;
            if (sTextCache.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            string text;
            if (sProvider != null && sProvider.TryGetText(languageId, textId, out text))
            {
                sTextCache[cacheKey] = text;
                return text;
            }

            if (languageId != sDefaultLanguage)
            {
                LocalizationTextCacheKey fallbackKey = new(sDefaultLanguage, textId);
                if (sTextCache.TryGetValue(fallbackKey, out cached))
                {
                    return cached;
                }

                if (sProvider != null && sProvider.TryGetText(sDefaultLanguage, textId, out text))
                {
                    sTextCache[fallbackKey] = text;
                    return text;
                }
            }

            return "[Missing:" + textId + "]";
        }

        private static string GetPluralInternal(LanguageId languageId, int textId, PluralCategory category)
        {
            LocalizationPluralCacheKey cacheKey = new(languageId, textId, category);
            string cached;
            if (sPluralCache.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            string text;
            if (sProvider != null && sProvider.TryGetPluralText(languageId, textId, category, out text))
            {
                sPluralCache[cacheKey] = text;
                return text;
            }

            if (languageId != sDefaultLanguage)
            {
                LocalizationPluralCacheKey fallbackKey = new(sDefaultLanguage, textId, category);
                if (sPluralCache.TryGetValue(fallbackKey, out cached))
                {
                    return cached;
                }

                if (sProvider != null && sProvider.TryGetPluralText(sDefaultLanguage, textId, category, out text))
                {
                    sPluralCache[fallbackKey] = text;
                    return text;
                }
            }

            return "[Missing:" + textId + ":" + category + "]";
        }

        private static string FormatSingleCount(string template, int count)
        {
            object[] args = ArrayPool<object>.Shared.Rent(1);
            try
            {
                args[0] = count;
                return sFormatter.Format(template, new ReadOnlySpan<object>(args, 0, 1));
            }
            finally
            {
                args[0] = null;
                ArrayPool<object>.Shared.Return(args);
            }
        }

        private static bool IsSupportedLanguage(LanguageId languageId)
        {
            IReadOnlyList<LanguageId> supportedLanguages = sProvider.GetSupportedLanguages();
            for (int i = 0; i < supportedLanguages.Count; i++)
            {
                if (supportedLanguages[i] == languageId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void NotifyBinders()
        {
            foreach (ILocalizationBinder binder in sBinders)
            {
                if (binder != null && binder.IsValid)
                {
                    binder.Refresh();
                }
            }
        }

        private static void RemoveLanguageCache(LanguageId languageId)
        {
            List<LocalizationTextCacheKey> textKeys = new();
            foreach (LocalizationTextCacheKey key in sTextCache.Keys)
            {
                if (key.LanguageId == languageId)
                {
                    textKeys.Add(key);
                }
            }

            for (int i = 0; i < textKeys.Count; i++)
            {
                sTextCache.Remove(textKeys[i]);
            }

            List<LocalizationPluralCacheKey> pluralKeys = new();
            foreach (LocalizationPluralCacheKey key in sPluralCache.Keys)
            {
                if (key.LanguageId == languageId)
                {
                    pluralKeys.Add(key);
                }
            }

            for (int i = 0; i < pluralKeys.Count; i++)
            {
                sPluralCache.Remove(pluralKeys[i]);
            }
        }
    }
}
