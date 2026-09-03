using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;

namespace YokiFrame
{
    /// <summary>提供本地化文本、复数文本、语言切换和 Binder 刷新的统一 Runtime 入口。</summary>
    public static class LocalizationKit
    {
        private static ILocalizationProvider sProvider;
        private static ITextFormatter sFormatter = new DefaultTextFormatter();
        private static LanguageId sCurrentLanguage = LanguageId.ChineseSimplified;
        private static LanguageId sDefaultLanguage = LanguageId.ChineseSimplified;
        private static readonly Dictionary<TextCacheKey, string> sTextCache = new Dictionary<TextCacheKey, string>();
        private static readonly Dictionary<PluralCacheKey, string> sPluralCache = new Dictionary<PluralCacheKey, string>();
        private static readonly HashSet<ILocalizationBinder> sBinders = new HashSet<ILocalizationBinder>();
        private static readonly string[] sCategoryNames = { "Zero", "One", "Two", "Few", "Many", "Other" };

        /// <summary>语言成功切换后触发。</summary>
        public static event Action<LanguageId> OnLanguageChanged;

        /// <summary>设置 Provider，并清理旧 Provider 产生的缓存。</summary>
        /// <param name="localizationProvider">不得为空的 Provider。</param>
        public static void SetProvider(ILocalizationProvider localizationProvider)
        {
            if (localizationProvider == null)
            {
                throw new ArgumentNullException(nameof(localizationProvider));
            }

            bool changed = !ReferenceEquals(sProvider, localizationProvider);
            sProvider = localizationProvider;
            ClearCache();
            if (changed)
            {
                NotifyBinders();
            }

        }

        /// <summary>获取当前 Provider；未配置时返回 null。</summary>
        public static ILocalizationProvider GetProvider() => sProvider;

        /// <summary>设置文本 Formatter。</summary>
        /// <param name="textFormatter">不得为空的 Formatter。</param>
        public static void SetFormatter(ITextFormatter textFormatter)
        {
            if (textFormatter == null)
            {
                throw new ArgumentNullException(nameof(textFormatter));
            }

            sFormatter = textFormatter;
        }

        /// <summary>获取当前 Formatter。</summary>
        public static ITextFormatter GetFormatter() => sFormatter;

        /// <summary>设置缺失文本的 fallback 语言并清理相关缓存。</summary>
        public static void SetDefaultLanguage(LanguageId languageId)
        {
            if (sDefaultLanguage == languageId)
            {
                return;
            }

            sDefaultLanguage = languageId;
            ClearCache();
            NotifyBinders();
        }

        /// <summary>获取缺失文本的 fallback 语言。</summary>
        public static LanguageId GetDefaultLanguage() => sDefaultLanguage;

        /// <summary>切换当前语言；Provider 存在时目标语言必须在支持列表中。</summary>
        /// <returns>切换成功或目标已是当前语言时返回 true。</returns>
        public static bool SetLanguage(LanguageId languageId)
        {
            if (sProvider != null && !IsSupportedLanguage(languageId))
            {
                return false;
            }

            if (sCurrentLanguage == languageId)
            {
                return true;
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

        /// <summary>获取当前语言。</summary>
        public static LanguageId GetCurrentLanguage() => sCurrentLanguage;

        /// <summary>获取 Provider 支持的语言。</summary>
        public static IReadOnlyList<LanguageId> GetAvailableLanguages() =>
            sProvider == null ? Array.Empty<LanguageId>() : sProvider.GetSupportedLanguages();

        /// <summary>获取指定语言的显示信息。</summary>
        public static LanguageInfo GetLanguageInfo(LanguageId languageId) =>
            sProvider == null ? LanguageInfo.Empty : sProvider.GetLanguageInfo(languageId);

        /// <summary>判断指定语言是否已加载。</summary>
        public static bool IsLanguageLoaded(LanguageId languageId) =>
            sProvider != null && sProvider.IsLanguageLoaded(languageId);

        /// <summary>按当前语言读取普通文本。</summary>
        public static string Get(int textId) => GetInternal(sCurrentLanguage, textId);

        /// <summary>按指定语言读取普通文本。</summary>
        public static string Get(LanguageId languageId, int textId) => GetInternal(languageId, textId);

        /// <summary>读取并按索引参数格式化普通文本。</summary>
        public static string Get(int textId, params object[] args)
        {
            string template = GetInternal(sCurrentLanguage, textId);
            return args == null || args.Length == 0 ? template : sFormatter.Format(template, args);
        }

        /// <summary>读取并按命名参数格式化普通文本。</summary>
        public static string Get(int textId, IReadOnlyDictionary<string, object> args)
        {
            string template = GetInternal(sCurrentLanguage, textId);
            return args == null || args.Count == 0 ? template : sFormatter.Format(template, args);
        }

        /// <summary>按当前语言选择复数分类，并把数量作为第一个参数格式化。</summary>
        public static string GetPlural(int textId, int count)
        {
            PluralCategory category = PluralRuleFactory.GetCategory(sCurrentLanguage, count);
            return FormatSingleCount(GetPluralInternal(sCurrentLanguage, textId, category, count), count);
        }

        /// <summary>按当前语言选择复数分类，并把数量和额外参数一起格式化。</summary>
        public static string GetPlural(int textId, int count, params object[] extraArgs)
        {
            PluralCategory category = PluralRuleFactory.GetCategory(sCurrentLanguage, count);
            string template = GetPluralInternal(sCurrentLanguage, textId, category, count);
            if (extraArgs == null || extraArgs.Length == 0)
            {
                return FormatSingleCount(template, count);
            }

            object[] args = new object[extraArgs.Length + 1];
            args[0] = count;
            Array.Copy(extraArgs, 0, args, 1, extraArgs.Length);
            return sFormatter.Format(template, args);
        }

        /// <summary>清理普通文本和复数文本缓存。</summary>
        public static void ClearCache()
        {
            sTextCache.Clear();
            sPluralCache.Clear();
        }

        /// <summary>注册语言切换时需要刷新的 Binder。</summary>
        public static void RegisterBinder(ILocalizationBinder binder)
        {
            if (binder == null)
            {
                return;
            }

            sBinders.Add(binder);
        }

        /// <summary>注销语言切换 Binder。</summary>
        public static void UnregisterBinder(ILocalizationBinder binder)
        {
            if (binder == null)
            {
                return;
            }

            sBinders.Remove(binder);
        }

        /// <summary>获取当前注册的 Binder 数量。</summary>
        public static int GetBinderCount() => sBinders.Count;

        /// <summary>请求 Provider 预加载语言。</summary>
        public static void PreloadLanguage(LanguageId languageId)
        {
            if (sProvider == null)
            {
                return;
            }

            sProvider.PreloadLanguage(languageId);
        }

        /// <summary>请求 Provider 卸载语言并移除该语言缓存。</summary>
        public static void UnloadLanguage(LanguageId languageId)
        {
            if (sProvider != null)
            {
                sProvider.UnloadLanguage(languageId);
            }

            RemoveLanguageCache(languageId);
        }

        /// <summary>重置全部 Runtime 状态，主要用于测试和宿主重置。</summary>
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

        /// <summary>按语言读取普通文本，并执行默认语言 fallback。</summary>
        private static string GetInternal(LanguageId languageId, int textId)
        {
            TextCacheKey cacheKey = new TextCacheKey(languageId, textId);
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
                TextCacheKey fallbackKey = new TextCacheKey(sDefaultLanguage, textId);
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

        /// <summary>按当前分类读取复数文本，并按 fallback 语言重新计算分类。</summary>
        private static string GetPluralInternal(LanguageId languageId, int textId, PluralCategory category, int count)
        {
            PluralCacheKey cacheKey = new PluralCacheKey(languageId, textId, category);
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
                PluralCategory fallbackCategory = PluralRuleFactory.GetCategory(sDefaultLanguage, count);
                PluralCacheKey fallbackKey = new PluralCacheKey(sDefaultLanguage, textId, fallbackCategory);
                if (sPluralCache.TryGetValue(fallbackKey, out cached))
                {
                    return cached;
                }

                if (sProvider != null && sProvider.TryGetPluralText(sDefaultLanguage, textId, fallbackCategory, out text))
                {
                    sPluralCache[fallbackKey] = text;
                    return text;
                }
            }

            return BuildMissingPluralText(textId, category);
        }

        /// <summary>构造复数缺失标记，避免枚举装箱与名称查找。</summary>
        private static string BuildMissingPluralText(int textId, PluralCategory category)
        {
            int categoryIndex = (int)category;
            string categoryName = categoryIndex >= 0 && categoryIndex < sCategoryNames.Length
                ? sCategoryNames[categoryIndex]
                : categoryIndex.ToString(CultureInfo.InvariantCulture);
            return string.Concat("[Missing:", textId.ToString(CultureInfo.InvariantCulture), ":", categoryName, "]");
        }

        /// <summary>使用 ArrayPool 为单数量复数格式化提供低分配参数缓冲。</summary>
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

        /// <summary>在线性扫描 Provider 只读语言列表中确认目标语言。</summary>
        private static bool IsSupportedLanguage(LanguageId languageId)
        {
            IReadOnlyList<LanguageId> languages = sProvider.GetSupportedLanguages();
            for (int index = 0; index < languages.Count; index++)
            {
                if (languages[index] == languageId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>使用快照刷新有效 Binder，允许刷新期间注销自身，并顺带移除失效条目。</summary>
        private static void NotifyBinders()
        {
            if (sBinders.Count == 0)
            {
                return;
            }

            var snapshot = new List<ILocalizationBinder>(sBinders);
            for (int index = 0; index < snapshot.Count; index++)
            {
                ILocalizationBinder binder = snapshot[index];
                if (binder == null || !binder.IsValid)
                {
                    sBinders.Remove(binder);
                    continue;
                }

                binder.Refresh();
            }
        }

        /// <summary>移除指定语言的普通文本和复数缓存项。</summary>
        private static void RemoveLanguageCache(LanguageId languageId)
        {
            var textKeys = new List<TextCacheKey>();
            foreach (TextCacheKey key in sTextCache.Keys)
            {
                if (key.LanguageId == languageId)
                {
                    textKeys.Add(key);
                }
            }

            for (int index = 0; index < textKeys.Count; index++)
            {
                sTextCache.Remove(textKeys[index]);
            }

            var pluralKeys = new List<PluralCacheKey>();
            foreach (PluralCacheKey key in sPluralCache.Keys)
            {
                if (key.LanguageId == languageId)
                {
                    pluralKeys.Add(key);
                }
            }

            for (int index = 0; index < pluralKeys.Count; index++)
            {
                sPluralCache.Remove(pluralKeys[index]);
            }
        }

        private readonly struct TextCacheKey : IEquatable<TextCacheKey>
        {
            public TextCacheKey(LanguageId languageId, int textId)
            {
                LanguageId = languageId;
                TextId = textId;
            }

            public LanguageId LanguageId { get; }
            private int TextId { get; }
            public bool Equals(TextCacheKey other) => LanguageId == other.LanguageId && TextId == other.TextId;
            public override bool Equals(object obj) => obj is TextCacheKey && Equals((TextCacheKey)obj);
            public override int GetHashCode() => ((int)LanguageId * 397) ^ TextId;

            /// <summary>比较两个文本缓存键是否相等。</summary>
            public static bool operator ==(TextCacheKey left, TextCacheKey right) => left.Equals(right);

            /// <summary>比较两个文本缓存键是否不相等。</summary>
            public static bool operator !=(TextCacheKey left, TextCacheKey right) => !left.Equals(right);
        }

        private readonly struct PluralCacheKey : IEquatable<PluralCacheKey>
        {
            public PluralCacheKey(LanguageId languageId, int textId, PluralCategory category)
            {
                LanguageId = languageId;
                TextId = textId;
                Category = category;
            }

            public LanguageId LanguageId { get; }
            private int TextId { get; }
            private PluralCategory Category { get; }
            public bool Equals(PluralCacheKey other) =>
                LanguageId == other.LanguageId && TextId == other.TextId && Category == other.Category;
            public override bool Equals(object obj) => obj is PluralCacheKey && Equals((PluralCacheKey)obj);
            public override int GetHashCode() => (((int)LanguageId * 397) ^ TextId) * 397 ^ (int)Category;

            /// <summary>比较两个复数缓存键是否相等。</summary>
            public static bool operator ==(PluralCacheKey left, PluralCacheKey right) => left.Equals(right);

            /// <summary>比较两个复数缓存键是否不相等。</summary>
            public static bool operator !=(PluralCacheKey left, PluralCacheKey right) => !left.Equals(right);
        }
    }
}
