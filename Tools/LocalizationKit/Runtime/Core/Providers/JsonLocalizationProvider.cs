using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// JSON 文件本地化数据提供者
    /// 从 JSON 文件加载本地化数据，支持运行时和编辑器模式
    /// </summary>
    public class JsonLocalizationProvider : ILocalizationProvider
    {
        /// <summary>
        /// JSON 数据格式
        /// </summary>
        [Serializable]
        private class LocalizationData
        {
            public LanguageData[] languages;
            public TextEntry[] texts;
        }

        [Serializable]
        private class LanguageData
        {
            public int id;
            public int displayNameTextId;
            public int nativeNameTextId;
            public int iconSpriteId;
        }

        [Serializable]
        private class TextEntry
        {
            public int id;
            public string[] values; // 按 LanguageId 顺序存储
        }

        // 缓存：语言ID -> (文本ID -> 文本内容)
        private readonly Dictionary<LanguageId, Dictionary<int, string>> mTextCache = new();
        
        // 复数形式缓存：语言ID -> (文本ID -> (复数类别 -> 文本内容))
        private readonly Dictionary<LanguageId, Dictionary<int, Dictionary<PluralCategory, string>>> mPluralCache = new();
        
        // 语言信息缓存
        private readonly Dictionary<LanguageId, LanguageInfo> mLanguageInfoCache = new();
        
        // 支持的语言列表
        private readonly List<LanguageId> mSupportedLanguages = new();
        
        // 已加载的语言
        private readonly HashSet<LanguageId> mLoadedLanguages = new();

        // JSON 文件路径模式，{0} 为语言代码
        private readonly string mPathPattern;
        
        // 是否使用 Resources 加载
        private readonly bool mUseResources;

        /// <summary>
        /// 创建 JSON 本地化提供者
        /// </summary>
        /// <param name="pathPattern">JSON 文件路径模式，{0} 为语言代码。如 "Localization/lang_{0}"</param>
        /// <param name="useResources">是否使用 Resources 加载</param>
        public JsonLocalizationProvider(string pathPattern = "Localization/localization", bool useResources = true)
        {
            mPathPattern = pathPattern;
            mUseResources = useResources;
        }

        /// <summary>
        /// 从 JSON 文本初始化（用于测试或自定义加载）
        /// </summary>
        public void LoadFromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                KitLogger.Warning("[LocalizationKit] JSON 内容为空");
                return;
            }

            try
            {
                var data = JsonUtility.FromJson<LocalizationData>(json);
                ProcessLocalizationData(data);
            }
            catch (Exception e)
            {
                KitLogger.Error($"[LocalizationKit] 解析 JSON 失败: {e.Message}");
            }
        }

        /// <summary>
        /// 从 Resources 加载
        /// </summary>
        public void LoadFromResources()
        {
            if (!mUseResources) return;

            var textAsset = Resources.Load<TextAsset>(mPathPattern);
            if (textAsset == null)
            {
                KitLogger.Warning($"[LocalizationKit] 未找到本地化文件: {mPathPattern}");
                return;
            }

            LoadFromJson(textAsset.text);
        }

        private void ProcessLocalizationData(LocalizationData data)
        {
            if (data == null) return;

            // 处理语言信息
            if (data.languages != null)
            {
                mSupportedLanguages.Clear();
                mLanguageInfoCache.Clear();

                foreach (var lang in data.languages)
                {
                    var languageId = (LanguageId)lang.id;
                    mSupportedLanguages.Add(languageId);
                    mLanguageInfoCache[languageId] = new LanguageInfo(
                        languageId,
                        lang.displayNameTextId,
                        lang.nativeNameTextId,
                        lang.iconSpriteId
                    );
                    mLoadedLanguages.Add(languageId);
                }
            }

            // 处理文本数据
            if (data.texts != null)
            {
                foreach (var entry in data.texts)
                {
                    if (entry.values == null) continue;

                    for (int i = 0; i < entry.values.Length && i < mSupportedLanguages.Count; i++)
                    {
                        var languageId = mSupportedLanguages[i];
                        
                        if (!mTextCache.TryGetValue(languageId, out var textDict))
                        {
                            textDict = new Dictionary<int, string>();
                            mTextCache[languageId] = textDict;
                        }

                        textDict[entry.id] = entry.values[i];
                    }
                }
            }
        }

        public IReadOnlyList<LanguageId> GetSupportedLanguages()
        {
            return mSupportedLanguages;
        }

        public bool TryGetText(LanguageId languageId, int textId, out string text)
        {
            text = null;

            if (!mTextCache.TryGetValue(languageId, out var textDict))
                return false;

            return textDict.TryGetValue(textId, out text);
        }

        public bool TryGetPluralText(LanguageId languageId, int textId, PluralCategory category, out string text)
        {
            text = null;

            if (!mPluralCache.TryGetValue(languageId, out var textDict))
                return false;

            if (!textDict.TryGetValue(textId, out var categoryDict))
                return false;

            // 先尝试获取指定类别，失败则 fallback 到 Other
            if (categoryDict.TryGetValue(category, out text))
                return true;

            return categoryDict.TryGetValue(PluralCategory.Other, out text);
        }

        public LanguageInfo GetLanguageInfo(LanguageId languageId)
        {
            return mLanguageInfoCache.TryGetValue(languageId, out var info) ? info : LanguageInfo.Empty;
        }

        public void PreloadLanguage(LanguageId languageId)
        {
            // JSON 模式下，所有语言在初始化时一次性加载
            // 此方法保留用于接口兼容
            if (!mLoadedLanguages.Contains(languageId))
            {
                LoadFromResources();
            }
        }

        public void UnloadLanguage(LanguageId languageId)
        {
            mTextCache.Remove(languageId);
            mPluralCache.Remove(languageId);
            mLoadedLanguages.Remove(languageId);
        }

        public bool IsLanguageLoaded(LanguageId languageId)
        {
            return mLoadedLanguages.Contains(languageId);
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void Clear()
        {
            mTextCache.Clear();
            mPluralCache.Clear();
            mLanguageInfoCache.Clear();
            mSupportedLanguages.Clear();
            mLoadedLanguages.Clear();
        }

        /// <summary>
        /// 手动添加文本（用于测试）
        /// </summary>
        public void AddText(LanguageId languageId, int textId, string text)
        {
            if (!mTextCache.TryGetValue(languageId, out var textDict))
            {
                textDict = new Dictionary<int, string>();
                mTextCache[languageId] = textDict;
            }

            textDict[textId] = text;

            if (!mSupportedLanguages.Contains(languageId))
            {
                mSupportedLanguages.Add(languageId);
            }

            mLoadedLanguages.Add(languageId);
        }

        /// <summary>
        /// 手动添加复数文本（用于测试）
        /// </summary>
        public void AddPluralText(LanguageId languageId, int textId, PluralCategory category, string text)
        {
            if (!mPluralCache.TryGetValue(languageId, out var textDict))
            {
                textDict = new Dictionary<int, Dictionary<PluralCategory, string>>();
                mPluralCache[languageId] = textDict;
            }

            if (!textDict.TryGetValue(textId, out var categoryDict))
            {
                categoryDict = new Dictionary<PluralCategory, string>();
                textDict[textId] = categoryDict;
            }

            categoryDict[category] = text;

            if (!mSupportedLanguages.Contains(languageId))
            {
                mSupportedLanguages.Add(languageId);
            }

            mLoadedLanguages.Add(languageId);
        }

        /// <summary>
        /// 获取所有文本ID（用于编辑器工具）
        /// </summary>
        public IEnumerable<int> GetAllTextIds()
        {
            var allIds = new HashSet<int>();

            foreach (var langDict in mTextCache.Values)
            {
                foreach (var textId in langDict.Keys)
                {
                    allIds.Add(textId);
                }
            }

            return allIds;
        }
    }
}
