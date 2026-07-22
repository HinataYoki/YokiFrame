using System.Globalization;
using System.Text.Json;
using YokiFrame;
using YokiFrame.Tooling.Application.Models.LocalizationKit;

namespace YokiFrame.Tooling.Application.Services.LocalizationKit;

/// <summary>承载 Luban variants 映射的 JSON 兼容与本地化值投影，隔离 target 之间的 map wire format 差异。</summary>
public sealed partial class LocalizationKitApplicationService
{
    /// <summary>读取 variants map；Text 键写入普通文本，其余合法枚举键写入对应复数分类。</summary>
    /// <param name="row">当前 Id 的 JSON 记录。</param>
    /// <param name="languages">需要读取的语言列。</param>
    /// <param name="values">普通文本的输出映射。</param>
    /// <param name="pluralValues">复数文本的输出映射。</param>
    /// <param name="path">用于生成精确诊断的记录路径。</param>
    private static void ReadLubanVariants(
        JsonElement row,
        IReadOnlyList<LocalizationLanguageRecord> languages,
        IDictionary<string, string> values,
        IDictionary<string, Dictionary<string, string>> pluralValues,
        string path)
    {
        if (!TryGetPropertyIgnoreCase(row, LUBAN_VARIANTS_FIELD_NAME, out JsonElement variants))
        {
            return;
        }

        HashSet<string> seenKinds = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, JsonElement translationsElement, string variantPath) in EnumerateLubanVariants(
            variants,
            path + "." + LUBAN_VARIANTS_FIELD_NAME))
        {
            if (!TryParseLubanValueKind(key, out bool isNormalText, out PluralCategory category))
            {
                throw new InvalidDataException(variantPath + " 的枚举键无效。");
            }

            string canonicalKind = isNormalText ? LUBAN_NORMAL_VALUE_KIND_NAME : category.ToString();
            if (!seenKinds.Add(canonicalKind))
            {
                throw new InvalidDataException(variantPath + " 的映射键重复。");
            }

            IReadOnlyDictionary<string, string> translations = ReadLubanValues(
                RequireLubanObject(translationsElement, variantPath),
                languages,
                variantPath);
            if (isNormalText)
            {
                CopyNormalTranslations(values, translations, variantPath);
            }
            else
            {
                CopyPluralTranslations(pluralValues, category, translations, variantPath);
            }
        }
    }

    /// <summary>枚举 Luban map 的键值对，兼容对象形式和标准 JSON target 的二元数组形式。</summary>
    /// <param name="variants">variants 字段原始 JSON。</param>
    /// <param name="path">variants 字段路径。</param>
    /// <returns>枚举键、翻译对象和精确诊断路径。</returns>
    private static IEnumerable<(string Key, JsonElement Translations, string Path)> EnumerateLubanVariants(
        JsonElement variants,
        string path)
    {
        if (variants.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in variants.EnumerateObject())
            {
                yield return (property.Name, property.Value, path + "." + property.Name);
            }

            yield break;
        }

        if (variants.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(path + " 必须是对象或键值对数组。");
        }

        int entryIndex = 0;
        foreach (JsonElement entry in variants.EnumerateArray())
        {
            (string Key, JsonElement Translations, string Path) pair = ReadLubanVariantPair(entry, path, entryIndex);
            entryIndex++;
            yield return pair;
        }
    }

    /// <summary>校验 Luban 标准 JSON target 输出的一条二元 map 项，并保留其准确路径。</summary>
    /// <param name="entry">variants 数组中的单个 map 项。</param>
    /// <param name="variantsPath">父 variants 字段路径。</param>
    /// <param name="entryIndex">当前 map 项的零基索引。</param>
    /// <returns>已校验的枚举键、翻译对象和诊断路径。</returns>
    private static (string Key, JsonElement Translations, string Path) ReadLubanVariantPair(
        JsonElement entry,
        string variantsPath,
        int entryIndex)
    {
        string entryPath = variantsPath + "[" + entryIndex.ToString(CultureInfo.InvariantCulture) + "]";
        if (entry.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(entryPath + " 必须是键值对数组。");
        }

        JsonElement.ArrayEnumerator pair = entry.EnumerateArray();
        if (!pair.MoveNext())
        {
            throw new InvalidDataException(entryPath + " 缺少映射键。");
        }

        string key = ReadLubanVariantKey(pair.Current, entryPath + "[0]");
        if (!pair.MoveNext())
        {
            throw new InvalidDataException(entryPath + " 缺少映射值。");
        }

        JsonElement translations = pair.Current;
        if (pair.MoveNext())
        {
            throw new InvalidDataException(entryPath + " 必须恰好包含键和值。");
        }

        return (key, translations, entryPath);
    }

    /// <summary>读取 Luban 二元 map 条目的枚举键，接受数字枚举值和字符串枚举名称。</summary>
    /// <param name="element">map 条目的第一个元素。</param>
    /// <param name="path">用于生成精确诊断的键路径。</param>
    /// <returns>规范化前的字符串枚举键。</returns>
    private static string ReadLubanVariantKey(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int numericKey))
        {
            return numericKey.ToString(CultureInfo.InvariantCulture);
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            string? namedKey = element.GetString();
            if (!string.IsNullOrWhiteSpace(namedKey))
            {
                return namedKey;
            }
        }

        throw new InvalidDataException(path + " 必须是整数或非空枚举名称。");
    }

    /// <summary>解析 Luban map 的枚举键，兼容 JSON target 输出的名称和数值键。</summary>
    /// <param name="value">Luban map 的 JSON 枚举键。</param>
    /// <param name="isNormalText">键是否表示普通文本。</param>
    /// <param name="category">复数文本时对应的 Runtime 分类。</param>
    /// <returns>键是 Text 或合法复数分类时返回 true。</returns>
    private static bool TryParseLubanValueKind(string value, out bool isNormalText, out PluralCategory category)
    {
        isNormalText = string.Equals(value, LUBAN_NORMAL_VALUE_KIND_NAME, StringComparison.OrdinalIgnoreCase);
        category = default;
        if (isNormalText)
        {
            return true;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numericValue))
        {
            isNormalText = numericValue == 0;
            return isNormalText || LocalizationSchema.TryParsePluralCategory(numericValue - 1, out category);
        }

        return LocalizationSchema.TryParsePluralCategory(value, out category);
    }

    /// <summary>复制唯一的 Text 映射值；保留空字符串，以便覆盖率正确显示该语言仍未翻译。</summary>
    /// <param name="values">普通文本的输出映射。</param>
    /// <param name="translations">当前 Text map 的语言译文。</param>
    /// <param name="path">用于生成精确诊断的记录路径。</param>
    private static void CopyNormalTranslations(
        IDictionary<string, string> values,
        IReadOnlyDictionary<string, string> translations,
        string path)
    {
        foreach (KeyValuePair<string, string> translation in translations)
        {
            if (!values.TryAdd(translation.Key, translation.Value))
            {
                throw new InvalidDataException(path + " 的语言重复: " + translation.Key);
            }
        }
    }

    /// <summary>复制复数分类的非空译文，避免空单元格伪造可用的复数覆盖率。</summary>
    /// <param name="pluralValues">复数文本的输出映射。</param>
    /// <param name="category">当前 Runtime 复数分类。</param>
    /// <param name="translations">当前分类的语言译文。</param>
    /// <param name="path">用于生成精确诊断的记录路径。</param>
    private static void CopyPluralTranslations(
        IDictionary<string, Dictionary<string, string>> pluralValues,
        PluralCategory category,
        IReadOnlyDictionary<string, string> translations,
        string path)
    {
        foreach (KeyValuePair<string, string> translation in translations)
        {
            if (string.IsNullOrWhiteSpace(translation.Value))
            {
                continue;
            }

            if (!pluralValues.TryGetValue(translation.Key, out Dictionary<string, string>? categories))
            {
                categories = new Dictionary<string, string>(StringComparer.Ordinal);
                pluralValues.Add(translation.Key, categories);
            }

            if (!categories.TryAdd(category.ToString(), translation.Value))
            {
                throw new InvalidDataException(path + " 的复数分类重复: " + category);
            }
        }
    }

    /// <summary>读取一个变体内每个已声明语言的字符串值，缺失字段视为 Luban target 省略的空单元格。</summary>
    /// <param name="translations">当前 variants map 的一个翻译 bean。</param>
    /// <param name="languages">需要读取的语言列。</param>
    /// <param name="path">用于生成精确诊断的记录路径。</param>
    /// <returns>已出现语言字段的文本值。</returns>
    private static IReadOnlyDictionary<string, string> ReadLubanValues(
        JsonElement translations,
        IReadOnlyList<LocalizationLanguageRecord> languages,
        string path)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (LocalizationLanguageRecord language in languages)
        {
            if (!TryGetPropertyIgnoreCase(translations, language.Id, out JsonElement value))
            {
                continue;
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException(path + "." + language.Id + " 必须是字符串。");
            }

            values.Add(language.Id, value.GetString() ?? string.Empty);
        }

        return values;
    }
}
