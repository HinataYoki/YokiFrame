using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 本地化系统 - 内部加载、绑定器管理、预加载
    /// </summary>
    public static partial class LocalizationKit
    {
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
