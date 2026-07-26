using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace YokiFrame
{
    /// <summary>读取带语言键和复数分类的 JSON 本地化数据。</summary>
    public sealed class JsonLocalizationProvider : ILocalizationProvider
    {
        private readonly Dictionary<LanguageId, Dictionary<int, string>> mTexts = new Dictionary<LanguageId, Dictionary<int, string>>();
        private readonly Dictionary<LanguageId, Dictionary<int, Dictionary<PluralCategory, string>>> mPluralTexts =
            new Dictionary<LanguageId, Dictionary<int, Dictionary<PluralCategory, string>>>();
        private readonly Dictionary<LanguageId, LanguageInfo> mLanguageInfos = new Dictionary<LanguageId, LanguageInfo>();
        private readonly List<LanguageId> mSupportedLanguages = new List<LanguageId>();
        private readonly ReadOnlyCollection<LanguageId> mSupportedLanguageView;
        private readonly HashSet<LanguageId> mSupportedLanguageSet = new HashSet<LanguageId>();
        private readonly HashSet<LanguageId> mLoadedLanguages = new HashSet<LanguageId>();

        /// <summary>创建空 JSON Provider。</summary>
        public JsonLocalizationProvider()
        {
            mSupportedLanguageView = mSupportedLanguages.AsReadOnly();
        }

        /// <summary>获取最近一次 JSON 加载错误；成功时为空。</summary>
        public string LastLoadError { get; private set; }

        /// <summary>加载 JSON；失败时保留之前的完整 Provider 状态。</summary>
        public void LoadFromJson(string json)
        {
            string error;
            TryLoadFromJson(json, out error);
        }

        /// <summary>验证并原子替换 JSON Provider 状态。</summary>
        /// <param name="json">符合 LocalizationKit JSON schema 的文本。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>成功加载时返回 true。</returns>
        public bool TryLoadFromJson(string json, out string error)
        {
            try
            {
                object rootValue = LocalizationJsonParser.Parse(json);
                Dictionary<string, object> root = RequireObject(rootValue, "root");
                int formatVersion = ReadOptionalStrictInt(root, "formatVersion", LocalizationSchema.CurrentFormatVersion);
                if (formatVersion != LocalizationSchema.CurrentFormatVersion)
                {
                    throw new FormatException("Unsupported localization formatVersion: " + formatVersion);
                }

                var languages = new List<LanguageId>();
                var languageSet = new HashSet<LanguageId>();
                var infos = new Dictionary<LanguageId, LanguageInfo>();
                ParseLanguages(RequireList(root, "languages"), languages, languageSet, infos);

                var texts = new Dictionary<LanguageId, Dictionary<int, string>>();
                var pluralTexts = new Dictionary<LanguageId, Dictionary<int, Dictionary<PluralCategory, string>>>();
                ParseTexts(RequireList(root, "texts"), languageSet, texts, pluralTexts);

                ApplySnapshot(languages, languageSet, infos, texts, pluralTexts);
                LastLoadError = string.Empty;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                LastLoadError = error;
                return false;
            }
        }

        /// <summary>手动写入普通文本，供测试和自定义导入器使用。</summary>
        public void AddText(LanguageId languageId, int textId, string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            EnsureLanguage(languageId);
            Dictionary<int, string> texts = GetOrCreate(mTexts, languageId);
            texts[textId] = text;
            mLoadedLanguages.Add(languageId);
        }

        /// <summary>手动写入复数文本，供测试和自定义导入器使用。</summary>
        public void AddPluralText(LanguageId languageId, int textId, PluralCategory category, string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            EnsureLanguage(languageId);
            Dictionary<int, Dictionary<PluralCategory, string>> texts = GetOrCreate(mPluralTexts, languageId);
            Dictionary<PluralCategory, string> categories = GetOrCreate(texts, textId);
            categories[category] = text;
            mLoadedLanguages.Add(languageId);
        }

        /// <summary>设置语言显示信息。</summary>
        public void SetLanguageInfo(LanguageInfo info)
        {
            EnsureLanguage(info.Id);
            mLanguageInfos[info.Id] = info;
        }

        /// <summary>获取当前普通文本和复数文本中的全部文本编号。</summary>
        public IEnumerable<int> GetAllTextIds()
        {
            var ids = new HashSet<int>();
            AddIds(mTexts, ids);
            foreach (Dictionary<int, Dictionary<PluralCategory, string>> texts in mPluralTexts.Values)
            {
                foreach (int id in texts.Keys) ids.Add(id);
            }

            return ids;
        }

        /// <summary>清空 Provider 状态。</summary>
        public void Clear()
        {
            mTexts.Clear();
            mPluralTexts.Clear();
            mLanguageInfos.Clear();
            mSupportedLanguages.Clear();
            mSupportedLanguageSet.Clear();
            mLoadedLanguages.Clear();
            LastLoadError = string.Empty;
        }

        /// <inheritdoc />
        public IReadOnlyList<LanguageId> GetSupportedLanguages() => mSupportedLanguageView;

        /// <inheritdoc />
        public bool TryGetText(LanguageId languageId, int textId, out string text)
        {
            text = null;
            if (!IsLanguageLoaded(languageId)) return false;
            Dictionary<int, string> texts;
            return mTexts.TryGetValue(languageId, out texts) && texts.TryGetValue(textId, out text);
        }

        /// <inheritdoc />
        public bool TryGetPluralText(LanguageId languageId, int textId, PluralCategory category, out string text)
        {
            text = null;
            if (!IsLanguageLoaded(languageId)) return false;
            Dictionary<int, Dictionary<PluralCategory, string>> texts;
            Dictionary<PluralCategory, string> categories;
            if (mPluralTexts.TryGetValue(languageId, out texts) && texts.TryGetValue(textId, out categories)
                && (categories.TryGetValue(category, out text)
                    || category != PluralCategory.Other && categories.TryGetValue(PluralCategory.Other, out text)))
            {
                return true;
            }

            return TryGetText(languageId, textId, out text);
        }

        /// <inheritdoc />
        public LanguageInfo GetLanguageInfo(LanguageId languageId)
        {
            LanguageInfo info;
            if (mLanguageInfos.TryGetValue(languageId, out info)) return info;

            return mSupportedLanguageSet.Contains(languageId)
                ? new LanguageInfo(languageId, 0, 0, 0)
                : LanguageInfo.Empty;
        }

        /// <inheritdoc />
        public void PreloadLanguage(LanguageId languageId)
        {
            if (mSupportedLanguageSet.Contains(languageId)) mLoadedLanguages.Add(languageId);
        }

        /// <inheritdoc />
        public void UnloadLanguage(LanguageId languageId)
        {
            if (mSupportedLanguageSet.Contains(languageId)) mLoadedLanguages.Remove(languageId);
        }

        /// <inheritdoc />
        public bool IsLanguageLoaded(LanguageId languageId) =>
            mSupportedLanguageSet.Contains(languageId) && mLoadedLanguages.Contains(languageId);

        /// <summary>解析语言列表并建立稳定的语言顺序、集合和元数据。</summary>
        private void ParseLanguages(IList values, List<LanguageId> languages, HashSet<LanguageId> languageSet, Dictionary<LanguageId, LanguageInfo> infos)
        {
            for (int index = 0; index < values.Count; index++)
            {
                Dictionary<string, object> data = RequireObject(values[index], "languages[" + index + "]");
                LanguageId languageId = ReadLanguageId(data, "id");
                if (!languageSet.Add(languageId)) throw new FormatException("Duplicate language: " + languageId);
                languages.Add(languageId);
                infos[languageId] = new LanguageInfo(languageId,
                    ReadOptionalInt(data, "displayNameTextId", 0),
                    ReadOptionalInt(data, "nativeNameTextId", 0),
                    ReadOptionalInt(data, "iconSpriteId", 0));
            }
        }

        /// <summary>解析普通文本和复数文本，并拒绝未声明语言或错误值类型。</summary>
    private void ParseTexts(IList values, HashSet<LanguageId> languageSet,
            Dictionary<LanguageId, Dictionary<int, string>> texts,
            Dictionary<LanguageId, Dictionary<int, Dictionary<PluralCategory, string>>> pluralTexts)
        {
            var textIds = new HashSet<int>();
            for (int index = 0; index < values.Count; index++)
            {
                Dictionary<string, object> data = RequireObject(values[index], "texts[" + index + "]");
                int textId = ReadRequiredInt(data, "id");
                if (!textIds.Add(textId))
                {
                    throw new FormatException("Duplicate text ID: " + textId);
                }

                object valuesValue;
                if (data.TryGetValue("values", out valuesValue) && valuesValue != null)
                {
                    Dictionary<string, object> textValues = RequireObject(valuesValue, "texts[" + index + "].values");
                    foreach (KeyValuePair<string, object> entry in textValues)
                    {
                        LanguageId languageId = ReadLanguageId(entry.Key);
                        EnsureKnownLanguage(languageId, languageSet);
                        if (!(entry.Value is string)) throw new FormatException("Text value must be a string.");
                        GetOrCreate(texts, languageId)[textId] = (string)entry.Value;
                    }
                }

                object pluralValue;
                if (!data.TryGetValue("plural", out pluralValue) || pluralValue == null) continue;
                Dictionary<string, object> pluralLanguages = RequireObject(pluralValue, "texts[" + index + "].plural");
                foreach (KeyValuePair<string, object> languageEntry in pluralLanguages)
                {
                    LanguageId languageId = ReadLanguageId(languageEntry.Key);
                    EnsureKnownLanguage(languageId, languageSet);
                    Dictionary<string, object> categoryValues = RequireObject(languageEntry.Value, "plural language");
                    Dictionary<int, Dictionary<PluralCategory, string>> languageTexts = GetOrCreate(pluralTexts, languageId);
                    Dictionary<PluralCategory, string> categories = GetOrCreate(languageTexts, textId);
                    foreach (KeyValuePair<string, object> categoryEntry in categoryValues)
                    {
                        PluralCategory category = ReadPluralCategory(categoryEntry.Key);
                        if (!(categoryEntry.Value is string)) throw new FormatException("Plural value must be a string.");
                        categories[category] = (string)categoryEntry.Value;
                    }
                }
            }
        }

        /// <summary>替换当前 Provider 快照，保证失败解析不会留下半份数据。</summary>
        private void ApplySnapshot(List<LanguageId> languages, HashSet<LanguageId> languageSet,
            Dictionary<LanguageId, LanguageInfo> infos,
            Dictionary<LanguageId, Dictionary<int, string>> texts,
            Dictionary<LanguageId, Dictionary<int, Dictionary<PluralCategory, string>>> pluralTexts)
        {
            Clear();
            mSupportedLanguages.AddRange(languages);
            foreach (LanguageId languageId in languageSet)
            {
                mSupportedLanguageSet.Add(languageId);
                mLoadedLanguages.Add(languageId);
            }

            foreach (KeyValuePair<LanguageId, LanguageInfo> entry in infos) mLanguageInfos.Add(entry.Key, entry.Value);
            foreach (KeyValuePair<LanguageId, Dictionary<int, string>> entry in texts) mTexts.Add(entry.Key, entry.Value);
            foreach (KeyValuePair<LanguageId, Dictionary<int, Dictionary<PluralCategory, string>>> entry in pluralTexts)
                mPluralTexts.Add(entry.Key, entry.Value);
        }

        /// <summary>把手动添加的语言加入支持列表并保持首次出现顺序。</summary>
        private void EnsureLanguage(LanguageId languageId)
        {
            if (mSupportedLanguageSet.Add(languageId)) mSupportedLanguages.Add(languageId);
        }

        /// <summary>验证文本条目引用的语言已经在 languages 段声明。</summary>
        private static void EnsureKnownLanguage(LanguageId languageId, HashSet<LanguageId> languages)
        {
            if (!languages.Contains(languageId)) throw new FormatException("Text references unknown language: " + languageId);
        }

        /// <summary>读取必需 JSON 对象并在类型不符时给出字段路径。</summary>
        private static Dictionary<string, object> RequireObject(object value, string path)
        {
            Dictionary<string, object> result = value as Dictionary<string, object>;
            if (result == null) throw new FormatException(path + " must be an object.");
            return result;
        }

        /// <summary>读取必需 JSON 数组并在类型不符时给出字段名。</summary>
        private static IList RequireList(Dictionary<string, object> data, string key)
        {
            object value;
            data.TryGetValue(key, out value);
            IList result = value as IList;
            if (result == null) throw new FormatException(key + " must be an array.");
            return result;
        }

        /// <summary>读取必需整数，避免缺失字段静默变成零。</summary>
        private static int ReadRequiredInt(Dictionary<string, object> data, string key)
        {
            object value;
            if (!data.TryGetValue(key, out value))
            {
                throw new FormatException("Missing integer field: " + key);
            }

            int result;
            if (value == null || !TryReadInteger(value, out result))
            {
                throw new FormatException("Invalid integer field: " + key);
            }

            return result;
        }

        /// <summary>读取可选整数；字段缺失时用默认值，存在但非法时报错。</summary>
        private static int ReadOptionalStrictInt(Dictionary<string, object> data, string key, int fallback)
        {
            object value;
            if (!data.TryGetValue(key, out value) || value == null) return fallback;

            int result;
            if (!TryReadInteger(value, out result)) throw new FormatException("Invalid integer field: " + key);
            return result;
        }

        /// <summary>读取可选整数，兼容 JSON 数字和 invariant 字符串。</summary>
        private static int ReadOptionalInt(Dictionary<string, object> data, string key, int fallback)
        {
            object value;
            if (!data.TryGetValue(key, out value) || value == null)
            {
                return fallback;
            }

            int result;
            return TryReadInteger(value, out result) ? result : fallback;
        }

        /// <summary>读取 JSON 解析器产生的整数值，并拒绝小数或超出 Int32 范围的数字。</summary>
        private static bool TryReadInteger(object value, out int result)
        {
            if (value is long integerValue
                && integerValue >= int.MinValue
                && integerValue <= int.MaxValue)
            {
                result = (int)integerValue;
                return true;
            }

            if (value is double decimalValue
                && decimalValue >= int.MinValue
                && decimalValue <= int.MaxValue
                && decimalValue == Math.Truncate(decimalValue))
            {
                result = (int)decimalValue;
                return true;
            }

            if (value is string text
                && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>从对象字段读取并验证语言标识。</summary>
        private static LanguageId ReadLanguageId(Dictionary<string, object> data, string key)
        {
            object value;
            data.TryGetValue(key, out value);
            return ReadLanguageId(value);
        }

        /// <summary>解析数字或名称形式的语言标识，并拒绝未知枚举值。</summary>
        private static LanguageId ReadLanguageId(object value)
        {
            LanguageId result;
            int numericValue;
            if (TryReadInteger(value, out numericValue)
                && LocalizationSchema.TryParseLanguageId(numericValue, out result))
            {
                return result;
            }

            if (value is string text && LocalizationSchema.TryParseLanguageId(text, out result))
            {
                return result;
            }

            throw new FormatException("Invalid language ID.");
        }

        /// <summary>解析名称或数字形式的复数分类。</summary>
        private static PluralCategory ReadPluralCategory(string value)
        {
            PluralCategory result;
            if (LocalizationSchema.TryParsePluralCategory(value, out result)) return result;
            throw new FormatException("Invalid plural category: " + value);
        }

        /// <summary>获取字典项，不存在时创建对应的嵌套字典。</summary>
        private static TValue GetOrCreate<TKey, TValue>(Dictionary<TKey, TValue> values, TKey key)
            where TValue : new()
        {
            TValue result;
            if (!values.TryGetValue(key, out result))
            {
                result = new TValue();
                values.Add(key, result);
            }

            return result;
        }

        /// <summary>把普通文本字典中的编号并入去重集合。</summary>
        private static void AddIds(Dictionary<LanguageId, Dictionary<int, string>> values, HashSet<int> ids)
        {
            foreach (Dictionary<int, string> texts in values.Values)
                foreach (int id in texts.Keys) ids.Add(id);
        }
    }

}
