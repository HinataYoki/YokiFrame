using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace YokiFrame
{
    /// <summary>
    /// 基于 JSON 字符串的本地化数据提供器。
    /// </summary>
    public sealed class JsonLocalizationProvider : ILocalizationProvider
    {
        private readonly Dictionary<LanguageId, Dictionary<int, string>> mTexts = new();
        private readonly Dictionary<LanguageId, Dictionary<int, Dictionary<PluralCategory, string>>> mPluralTexts = new();
        private readonly Dictionary<LanguageId, LanguageInfo> mLanguageInfos = new();
        private readonly List<LanguageId> mSupportedLanguages = new();
        private readonly HashSet<LanguageId> mSupportedLanguageSet = new();
        private readonly HashSet<LanguageId> mLoadedLanguages = new();

        /// <summary>
        /// 从 JSON 字符串加载本地化数据。
        /// </summary>
        /// <param name="json">本地化 JSON 文本。</param>
        public void LoadFromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                KitLogger.Warning("[LocalizationKit] JSON 内容为空");
                return;
            }

            try
            {
                object root = JsonValueParser.Parse(json);
                ProcessLocalizationData(root as Dictionary<string, object>);
            }
            catch (Exception e)
            {
                KitLogger.Error("[LocalizationKit] 解析 JSON 失败: " + e.Message);
            }
        }

        /// <summary>
        /// 手动添加普通文本，适合测试、工具或项目自定义加载器。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        /// <param name="textId">文本编号。</param>
        /// <param name="text">文本内容。</param>
        public void AddText(LanguageId languageId, int textId, string text)
        {
            EnsureLanguage(languageId);

            Dictionary<int, string> languageTexts;
            if (!mTexts.TryGetValue(languageId, out languageTexts))
            {
                languageTexts = new Dictionary<int, string>();
                mTexts.Add(languageId, languageTexts);
            }

            languageTexts[textId] = text;
            mLoadedLanguages.Add(languageId);
        }

        /// <summary>
        /// 手动添加复数文本。
        /// </summary>
        /// <param name="languageId">语言标识。</param>
        /// <param name="textId">文本编号。</param>
        /// <param name="category">复数分类。</param>
        /// <param name="text">文本内容。</param>
        public void AddPluralText(LanguageId languageId, int textId, PluralCategory category, string text)
        {
            EnsureLanguage(languageId);

            Dictionary<int, Dictionary<PluralCategory, string>> languagePluralTexts;
            if (!mPluralTexts.TryGetValue(languageId, out languagePluralTexts))
            {
                languagePluralTexts = new Dictionary<int, Dictionary<PluralCategory, string>>();
                mPluralTexts.Add(languageId, languagePluralTexts);
            }

            Dictionary<PluralCategory, string> categoryTexts;
            if (!languagePluralTexts.TryGetValue(textId, out categoryTexts))
            {
                categoryTexts = new Dictionary<PluralCategory, string>();
                languagePluralTexts.Add(textId, categoryTexts);
            }

            categoryTexts[category] = text;
            mLoadedLanguages.Add(languageId);
        }

        /// <summary>
        /// 获取当前所有普通文本编号。
        /// </summary>
        /// <returns>文本编号集合。</returns>
        public IEnumerable<int> GetAllTextIds()
        {
            var allIds = new HashSet<int>();
            foreach (Dictionary<int, string> texts in mTexts.Values)
            {
                foreach (int textId in texts.Keys)
                {
                    allIds.Add(textId);
                }
            }

            return allIds;
        }

        /// <summary>
        /// 清除全部 JSON 本地化缓存。
        /// </summary>
        public void Clear()
        {
            mTexts.Clear();
            mPluralTexts.Clear();
            mLanguageInfos.Clear();
            mSupportedLanguages.Clear();
            mSupportedLanguageSet.Clear();
            mLoadedLanguages.Clear();
        }

        /// <inheritdoc />
        public IReadOnlyList<LanguageId> GetSupportedLanguages()
        {
            return mSupportedLanguages;
        }

        /// <inheritdoc />
        public bool TryGetText(LanguageId languageId, int textId, out string text)
        {
            text = null;
            if (!IsLanguageLoaded(languageId))
            {
                return false;
            }

            Dictionary<int, string> languageTexts;
            return mTexts.TryGetValue(languageId, out languageTexts) && languageTexts.TryGetValue(textId, out text);
        }

        /// <inheritdoc />
        public bool TryGetPluralText(LanguageId languageId, int textId, PluralCategory category, out string text)
        {
            text = null;
            if (!IsLanguageLoaded(languageId))
            {
                return false;
            }

            Dictionary<int, Dictionary<PluralCategory, string>> languagePluralTexts;
            Dictionary<PluralCategory, string> categoryTexts;
            if (!mPluralTexts.TryGetValue(languageId, out languagePluralTexts) ||
                !languagePluralTexts.TryGetValue(textId, out categoryTexts))
            {
                return false;
            }

            return categoryTexts.TryGetValue(category, out text) ||
                   category != PluralCategory.Other && categoryTexts.TryGetValue(PluralCategory.Other, out text);
        }

        /// <inheritdoc />
        public LanguageInfo GetLanguageInfo(LanguageId languageId)
        {
            LanguageInfo info;
            return mLanguageInfos.TryGetValue(languageId, out info) ? info : LanguageInfo.Empty;
        }

        /// <inheritdoc />
        public void PreloadLanguage(LanguageId languageId)
        {
            if (mSupportedLanguageSet.Contains(languageId))
            {
                mLoadedLanguages.Add(languageId);
            }
        }

        /// <inheritdoc />
        public void UnloadLanguage(LanguageId languageId)
        {
            if (mSupportedLanguageSet.Contains(languageId))
            {
                mLoadedLanguages.Remove(languageId);
            }
        }

        /// <inheritdoc />
        public bool IsLanguageLoaded(LanguageId languageId)
        {
            return mSupportedLanguageSet.Contains(languageId) && mLoadedLanguages.Contains(languageId);
        }

        private void ProcessLocalizationData(Dictionary<string, object> data)
        {
            if (data == null)
            {
                return;
            }

            object languagesValue;
            if (data.TryGetValue("languages", out languagesValue))
            {
                LoadLanguages(languagesValue as IList);
            }

            object textsValue;
            if (data.TryGetValue("texts", out textsValue))
            {
                LoadTexts(textsValue as IList);
            }
        }

        private void LoadLanguages(IList languages)
        {
            if (languages == null)
            {
                return;
            }

            mSupportedLanguages.Clear();
            mSupportedLanguageSet.Clear();
            mLanguageInfos.Clear();
            mLoadedLanguages.Clear();

            for (int i = 0; i < languages.Count; i++)
            {
                Dictionary<string, object> languageData = languages[i] as Dictionary<string, object>;
                if (languageData == null)
                {
                    continue;
                }

                int id;
                if (!TryGetInt(languageData, "id", out id))
                {
                    continue;
                }

                LanguageId languageId = (LanguageId)id;
                EnsureLanguage(languageId);
                mLoadedLanguages.Add(languageId);

                int displayNameTextId;
                int nativeNameTextId;
                int iconSpriteId;
                TryGetInt(languageData, "displayNameTextId", out displayNameTextId);
                TryGetInt(languageData, "nativeNameTextId", out nativeNameTextId);
                TryGetInt(languageData, "iconSpriteId", out iconSpriteId);
                mLanguageInfos[languageId] = new LanguageInfo(languageId, displayNameTextId, nativeNameTextId, iconSpriteId);
            }
        }

        private void LoadTexts(IList textEntries)
        {
            if (textEntries == null)
            {
                return;
            }

            for (int i = 0; i < textEntries.Count; i++)
            {
                Dictionary<string, object> entry = textEntries[i] as Dictionary<string, object>;
                if (entry == null)
                {
                    continue;
                }

                int textId;
                object valuesValue;
                IList values;
                if (!TryGetInt(entry, "id", out textId) ||
                    !entry.TryGetValue("values", out valuesValue) ||
                    (values = valuesValue as IList) == null)
                {
                    continue;
                }

                int count = values.Count < mSupportedLanguages.Count ? values.Count : mSupportedLanguages.Count;
                for (int valueIndex = 0; valueIndex < count; valueIndex++)
                {
                    string text = values[valueIndex] as string;
                    if (text != null)
                    {
                        AddText(mSupportedLanguages[valueIndex], textId, text);
                    }
                }
            }
        }

        private void EnsureLanguage(LanguageId languageId)
        {
            if (mSupportedLanguageSet.Add(languageId))
            {
                mSupportedLanguages.Add(languageId);
            }
        }

        private static bool TryGetInt(Dictionary<string, object> data, string key, out int value)
        {
            value = 0;
            object raw;
            if (!data.TryGetValue(key, out raw) || raw == null)
            {
                return false;
            }

            if (raw is int)
            {
                value = (int)raw;
                return true;
            }

            if (raw is long)
            {
                value = (int)(long)raw;
                return true;
            }

            if (raw is double)
            {
                value = (int)(double)raw;
                return true;
            }

            string text = raw as string;
            return text != null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private sealed class JsonValueParser
        {
            private readonly string mJson;
            private int mIndex;

            private JsonValueParser(string json)
            {
                mJson = json;
            }

            public static object Parse(string json)
            {
                var parser = new JsonValueParser(json);
                object value = parser.ParseValue();
                parser.SkipWhitespace();
                if (parser.mIndex != parser.mJson.Length)
                {
                    throw new FormatException("JSON contains trailing data.");
                }

                return value;
            }

            private object ParseValue()
            {
                SkipWhitespace();
                if (mIndex >= mJson.Length)
                {
                    throw new FormatException("Unexpected end of JSON.");
                }

                char c = mJson[mIndex];
                if (c == '{')
                {
                    return ParseObject();
                }

                if (c == '[')
                {
                    return ParseArray();
                }

                if (c == '"')
                {
                    return ParseString();
                }

                if (c == '-' || c >= '0' && c <= '9')
                {
                    return ParseNumber();
                }

                if (MatchLiteral("true"))
                {
                    return true;
                }

                if (MatchLiteral("false"))
                {
                    return false;
                }

                if (MatchLiteral("null"))
                {
                    return null;
                }

                throw new FormatException("Unexpected JSON token.");
            }

            private Dictionary<string, object> ParseObject()
            {
                var result = new Dictionary<string, object>();
                Expect('{');
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    string key = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    result[key] = ParseValue();
                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        return result;
                    }

                    Expect(',');
                }
            }

            private List<object> ParseArray()
            {
                var result = new List<object>();
                Expect('[');
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return result;
                }

                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();
                    if (TryConsume(']'))
                    {
                        return result;
                    }

                    Expect(',');
                }
            }

            private string ParseString()
            {
                Expect('"');
                var chars = new List<char>();
                while (mIndex < mJson.Length)
                {
                    char c = mJson[mIndex++];
                    if (c == '"')
                    {
                        return new string(chars.ToArray());
                    }

                    if (c != '\\')
                    {
                        chars.Add(c);
                        continue;
                    }

                    if (mIndex >= mJson.Length)
                    {
                        throw new FormatException("Invalid JSON string escape.");
                    }

                    char escape = mJson[mIndex++];
                    switch (escape)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            chars.Add(escape);
                            break;
                        case 'b':
                            chars.Add('\b');
                            break;
                        case 'f':
                            chars.Add('\f');
                            break;
                        case 'n':
                            chars.Add('\n');
                            break;
                        case 'r':
                            chars.Add('\r');
                            break;
                        case 't':
                            chars.Add('\t');
                            break;
                        case 'u':
                            chars.Add(ParseUnicodeEscape());
                            break;
                        default:
                            throw new FormatException("Invalid JSON string escape.");
                    }
                }

                throw new FormatException("Unterminated JSON string.");
            }

            private char ParseUnicodeEscape()
            {
                if (mIndex + 4 > mJson.Length)
                {
                    throw new FormatException("Invalid unicode escape.");
                }

                string hex = mJson.Substring(mIndex, 4);
                mIndex += 4;
                return (char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            private object ParseNumber()
            {
                int start = mIndex;
                if (mJson[mIndex] == '-')
                {
                    mIndex++;
                }

                while (mIndex < mJson.Length && mJson[mIndex] >= '0' && mJson[mIndex] <= '9')
                {
                    mIndex++;
                }

                bool isFloat = false;
                if (mIndex < mJson.Length && mJson[mIndex] == '.')
                {
                    isFloat = true;
                    mIndex++;
                    while (mIndex < mJson.Length && mJson[mIndex] >= '0' && mJson[mIndex] <= '9')
                    {
                        mIndex++;
                    }
                }

                if (mIndex < mJson.Length && (mJson[mIndex] == 'e' || mJson[mIndex] == 'E'))
                {
                    isFloat = true;
                    mIndex++;
                    if (mIndex < mJson.Length && (mJson[mIndex] == '+' || mJson[mIndex] == '-'))
                    {
                        mIndex++;
                    }

                    while (mIndex < mJson.Length && mJson[mIndex] >= '0' && mJson[mIndex] <= '9')
                    {
                        mIndex++;
                    }
                }

                string text = mJson.Substring(start, mIndex - start);
                if (isFloat)
                {
                    return double.Parse(text, CultureInfo.InvariantCulture);
                }

                return long.Parse(text, CultureInfo.InvariantCulture);
            }

            private bool MatchLiteral(string literal)
            {
                if (mIndex + literal.Length > mJson.Length)
                {
                    return false;
                }

                for (int i = 0; i < literal.Length; i++)
                {
                    if (mJson[mIndex + i] != literal[i])
                    {
                        return false;
                    }
                }

                mIndex += literal.Length;
                return true;
            }

            private bool TryConsume(char expected)
            {
                if (mIndex < mJson.Length && mJson[mIndex] == expected)
                {
                    mIndex++;
                    return true;
                }

                return false;
            }

            private void Expect(char expected)
            {
                if (mIndex >= mJson.Length || mJson[mIndex] != expected)
                {
                    throw new FormatException("Expected '" + expected + "'.");
                }

                mIndex++;
            }

            private void SkipWhitespace()
            {
                while (mIndex < mJson.Length)
                {
                    char c = mJson[mIndex];
                    if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
                    {
                        return;
                    }

                    mIndex++;
                }
            }
        }
    }
}
