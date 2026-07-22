using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using YokiFrame;
using YokiFrame.Tooling.Application.Models.LocalizationKit;
using YokiFrame.Tooling.Application.Models.Luban;

namespace YokiFrame.Tooling.Application.Services.LocalizationKit;

/// <summary>承载 Luban JSON 到 LocalizationKit 强类型目录的解析与校验，避免页面层接触 wire JSON。</summary>
public sealed partial class LocalizationKitApplicationService
{
    /// <summary>把 Luban 预览中的唯一表投影为统一的 LocalizationCatalog。</summary>
    /// <param name="tables">中立 Luban 服务已经限制大小的 JSON 表。</param>
    /// <param name="workbookPath">作者 Excel 的绝对路径，用于来源显示。</param>
    /// <param name="diagnostics">生成过程的非阻断预览诊断。</param>
    /// <param name="schemaLanguages">由 XML schema 固定的语言列；为空时从 JSON 映射值推导。</param>
    /// <returns>按文本 ID 排序、可直接筛选的目录。</returns>
    internal static LocalizationCatalog ParseLubanCatalog(
        IReadOnlyList<LubanJsonPreviewTable> tables,
        string workbookPath,
        IReadOnlyList<string>? diagnostics = null,
        IReadOnlyList<LanguageId>? schemaLanguages = null)
    {
        ArgumentNullException.ThrowIfNull(tables);
        IReadOnlyList<JsonElement> rows = ReadLubanRows(FindLubanTable(tables, LUBAN_ENTRY_TABLE_NAME));
        List<LocalizationLanguageRecord> languages = ResolveLubanLanguages(rows, schemaLanguages);
        LocalizationEntryRecord[] records = ParseLubanEntries(rows, languages)
            .OrderBy(static entry => entry.Id)
            .ToArray();
        return new LocalizationCatalog
        {
            Provider = "Luban",
            SourcePath = workbookPath,
            FormatVersion = LocalizationSchema.CurrentFormatVersion,
            Languages = languages,
            Entries = records,
            Diagnostics = diagnostics ?? Array.Empty<string>()
        };
    }

    /// <summary>从 Luban 临时目录读取唯一表和 schema 语言列，空数据表仍可显示作者配置的语言。</summary>
    /// <param name="previewDirectory">中立 Luban 服务仍持有目录锁的 JSON 输出目录。</param>
    /// <param name="workbookPath">作者 Excel 的绝对路径，用于来源显示。</param>
    /// <param name="schemaPath">当前 XML schema，用于恢复语言列顺序。</param>
    /// <param name="diagnostics">生成过程的非阻断预览诊断。</param>
    /// <returns>与 standalone JSON 共享的本地化目录模型。</returns>
    private static LocalizationCatalog ParseLubanCatalogFromPreviewDirectory(
        string previewDirectory,
        string workbookPath,
        string schemaPath,
        IReadOnlyList<string>? diagnostics)
    {
        string[] previewFiles = Directory.EnumerateFiles(previewDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();
        LubanJsonPreviewTable[] tables =
        {
            ReadLubanPreviewTable(previewFiles, LUBAN_ENTRY_TABLE_NAME)
        };
        return ParseLubanCatalog(tables, workbookPath, diagnostics, ReadLubanSchemaLanguages(schemaPath));
    }

    /// <summary>读取模板 XML 的翻译 bean，确保空表预览也保留已配置的语言列与顺序。</summary>
    /// <param name="schemaPath">单表 XML schema 的绝对路径。</param>
    /// <returns>已校验且去重后的语言列。</returns>
    private static IReadOnlyList<LanguageId> ReadLubanSchemaLanguages(string schemaPath)
    {
        try
        {
            XmlReaderSettings settings = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using FileStream stream = new(schemaPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using XmlReader reader = XmlReader.Create(stream, settings);
            XDocument document = XDocument.Load(reader, LoadOptions.None);
            XElement? translationsBean = document.Root?
                .Elements("bean")
                .SingleOrDefault(element => string.Equals((string?)element.Attribute("name"), LUBAN_TRANSLATIONS_BEAN_NAME, StringComparison.Ordinal));
            if (translationsBean == null)
            {
                throw new InvalidDataException("LocalizationKit XML 缺少 " + LUBAN_TRANSLATIONS_BEAN_NAME + " bean。");
            }

            List<LanguageId> languages = new();
            HashSet<LanguageId> seen = new();
            foreach (XElement field in translationsBean.Elements("var"))
            {
                string fieldName = (string?)field.Attribute("name") ?? string.Empty;
                if (!LocalizationSchema.TryParseLanguageId(fieldName, out LanguageId language))
                {
                    throw new InvalidDataException(LUBAN_TRANSLATIONS_BEAN_NAME + " 包含无效语言列: " + fieldName);
                }

                if (!seen.Add(language))
                {
                    throw new InvalidDataException(LUBAN_TRANSLATIONS_BEAN_NAME + " 重复语言列: " + language);
                }

                languages.Add(language);
            }

            if (languages.Count == 0)
            {
                throw new InvalidDataException(LUBAN_TRANSLATIONS_BEAN_NAME + " 至少需要一个 LanguageId 语言列。");
            }

            return languages;
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("LocalizationKit XML 无法读取: " + exception.Message, exception);
        }
    }

    /// <summary>读取指定表的原始 JSON 文本；该调用发生在共享目录锁内，不会被并发预览清理。</summary>
    /// <param name="previewFiles">已经一次性枚举的 Luban JSON 输出文件。</param>
    /// <param name="expectedTableName">XML 中声明的稳定表名。</param>
    /// <returns>保留实际文件名和原始 JSON 的表投影。</returns>
    private static LubanJsonPreviewTable ReadLubanPreviewTable(
        IReadOnlyList<string> previewFiles,
        string expectedTableName)
    {
        string normalizedExpectedName = NormalizeLubanName(expectedTableName);
        string[] matches = previewFiles
            .Where(path => NormalizeLubanName(Path.GetFileNameWithoutExtension(path))
                .Contains(normalizedExpectedName, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidDataException(
                "Luban 预览未找到唯一的本地化表 " + expectedTableName
                + "。实际表: " + string.Join(", ", previewFiles.Select(static path => Path.GetFileNameWithoutExtension(path))));
        }

        string path = matches[0];
        return new LubanJsonPreviewTable
        {
            Name = Path.GetFileNameWithoutExtension(path),
            PreviewJson = File.ReadAllText(path)
        };
    }

    /// <summary>从预览表集合中定位唯一单表，兼容 Luban 输出中的下划线与大小写转换。</summary>
    /// <param name="tables">Luban JSON 表集合。</param>
    /// <param name="expectedName">XML 中声明的稳定表名。</param>
    /// <returns>唯一匹配的 JSON 表。</returns>
    private static LubanJsonPreviewTable FindLubanTable(IReadOnlyList<LubanJsonPreviewTable> tables, string expectedName)
    {
        string normalizedExpectedName = NormalizeLubanName(expectedName);
        LubanJsonPreviewTable[] matches = tables
            .Where(table => NormalizeLubanName(table.Name).Contains(normalizedExpectedName, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidDataException("Luban 预览未找到唯一的本地化表 " + expectedName + "。实际表: " + string.Join(", ", tables.Select(static table => table.Name)));
        }

        return matches[0];
    }

    /// <summary>把文件名或表名规整为仅由小写字母和数字组成的比较键。</summary>
    /// <param name="value">待比较的表名。</param>
    /// <returns>可跨下划线、短横线与大小写匹配的名称。</returns>
    private static string NormalizeLubanName(string value) => new(value
        .Where(char.IsLetterOrDigit)
        .Select(char.ToLowerInvariant)
        .ToArray());

    /// <summary>读取 JSON 表根数组；允许常见 data/items/rows/list 包装以兼容 Luban target 差异。</summary>
    /// <param name="table">已经经过大小限制的预览表。</param>
    /// <returns>当前表的记录 JSON 元素快照。</returns>
    private static IReadOnlyList<JsonElement> ReadLubanRows(LubanJsonPreviewTable table)
    {
        using JsonDocument document = JsonDocument.Parse(table.PreviewJson);
        JsonElement root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray().Select(static row => row.Clone()).ToArray();
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (string property in new[] { "data", "items", "rows", "list" })
            {
                if (TryGetPropertyIgnoreCase(root, property, out JsonElement rows) && rows.ValueKind == JsonValueKind.Array)
                {
                    return rows.EnumerateArray().Select(static row => row.Clone()).ToArray();
                }
            }
        }

        throw new InvalidDataException("Luban 表不是记录数组: " + table.Name);
    }

    /// <summary>合并 schema 与 JSON 映射值发现的语言字段，创建不含 Runtime 资源元数据的语言列表。</summary>
    /// <param name="rows">单表 JSON 记录。</param>
    /// <param name="schemaLanguages">XML 中声明的语言列，可为空。</param>
    /// <returns>按 schema 优先、随后按 JSON 首次出现顺序排列的语言记录。</returns>
    private static List<LocalizationLanguageRecord> ResolveLubanLanguages(
        IReadOnlyList<JsonElement> rows,
        IReadOnlyList<LanguageId>? schemaLanguages)
    {
        List<LocalizationLanguageRecord> result = new();
        HashSet<LanguageId> seen = new();
        foreach (LanguageId language in schemaLanguages ?? Array.Empty<LanguageId>())
        {
            AddLubanLanguage(result, seen, language);
        }

        for (int index = 0; index < rows.Count; index++)
        {
            string path = LUBAN_ENTRY_BEAN_NAME + "[" + index + "]";
            JsonElement row = RequireLubanObject(rows[index], path);
            if (!TryGetPropertyIgnoreCase(row, LUBAN_VARIANTS_FIELD_NAME, out JsonElement variants))
            {
                continue;
            }

            foreach ((_, JsonElement translations, string variantPath) in EnumerateLubanVariants(
                variants,
                path + "." + LUBAN_VARIANTS_FIELD_NAME))
            {
                foreach (JsonProperty translation in RequireLubanObject(translations, variantPath).EnumerateObject())
                {
                    if (LocalizationSchema.TryParseLanguageId(translation.Name, out LanguageId language))
                    {
                        AddLubanLanguage(result, seen, language);
                    }
                }
            }
        }

        if (result.Count == 0)
        {
            throw new InvalidDataException(LUBAN_TRANSLATIONS_BEAN_NAME + " 至少需要一个 LanguageId 语言列。");
        }

        return result;
    }

    /// <summary>将尚未出现的语言加入目录，保留 schema 或 JSON 的首次出现顺序。</summary>
    /// <param name="languages">正在构建的目录语言列表。</param>
    /// <param name="seen">用于去重的语言集合。</param>
    /// <param name="language">待加入的语言。</param>
    private static void AddLubanLanguage(
        ICollection<LocalizationLanguageRecord> languages,
        ISet<LanguageId> seen,
        LanguageId language)
    {
        if (seen.Add(language))
        {
            languages.Add(new LocalizationLanguageRecord { Id = language.ToString() });
        }
    }

    /// <summary>解析一行一个 Id 的表记录，并将 Text 与复数枚举键投影为现有目录的普通和复数值。</summary>
    /// <param name="rows">单表 JSON 记录。</param>
    /// <param name="languages">已校验的语言列。</param>
    /// <returns>尚未排序的本地化目录条目。</returns>
    private static IReadOnlyList<LocalizationEntryRecord> ParseLubanEntries(
        IReadOnlyList<JsonElement> rows,
        IReadOnlyList<LocalizationLanguageRecord> languages)
    {
        List<LocalizationEntryRecord> result = new(rows.Count);
        HashSet<int> textIds = new();
        for (int index = 0; index < rows.Count; index++)
        {
            string path = LUBAN_ENTRY_BEAN_NAME + "[" + index + "]";
            JsonElement row = RequireLubanObject(rows[index], path);
            int textId = ReadRequiredLubanInt(row, "id", path);
            if (!textIds.Add(textId))
            {
                throw new InvalidDataException(path + " 的 id 重复: " + textId);
            }

            Dictionary<string, string> values = new(StringComparer.Ordinal);
            Dictionary<string, Dictionary<string, string>> pluralValues = new(StringComparer.Ordinal);
            ReadLubanVariants(row, languages, values, pluralValues, path);
            result.Add(CreateLubanEntryRecord(
                textId,
                ReadOptionalLubanString(row, "key"),
                values,
                pluralValues,
                languages));
        }

        return result;
    }

    /// <summary>组装不可变目录记录，并按照完整语言列计算缺失项。</summary>
    /// <param name="textId">稳定文本 ID。</param>
    /// <param name="key">可选业务键。</param>
    /// <param name="values">已解析的普通文本。</param>
    /// <param name="pluralValues">已解析的复数文本。</param>
    /// <param name="languages">完整语言列。</param>
    /// <returns>可供筛选和覆盖率统计直接消费的条目。</returns>
    private static LocalizationEntryRecord CreateLubanEntryRecord(
        int textId,
        string key,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, Dictionary<string, string>> pluralValues,
        IReadOnlyList<LocalizationLanguageRecord> languages)
    {
        Dictionary<string, IReadOnlyDictionary<string, string>> projectedPluralValues = pluralValues.ToDictionary(
            static entry => entry.Key,
            static entry => (IReadOnlyDictionary<string, string>)entry.Value,
            StringComparer.Ordinal);
        LocalizationEntryRecord record = new()
        {
            Id = textId,
            Key = key,
            Values = values,
            PluralValues = projectedPluralValues
        };
        return record with
        {
            MissingLanguages = languages
                .Where(language => !record.HasValueFor(language.Id))
                .Select(static language => language.Id)
                .ToArray()
        };
    }

    /// <summary>读取必需对象记录，避免后续字段读取对数组或标量产生含糊错误。</summary>
    /// <param name="element">待校验的 JSON 元素。</param>
    /// <param name="path">用于错误信息的表和行路径。</param>
    /// <returns>对象形式的记录。</returns>
    private static JsonElement RequireLubanObject(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(path + " 必须是对象。");
        }

        return element;
    }

    /// <summary>读取必需整数列，接受 JSON 整数和 invariant 整数字符串。</summary>
    /// <param name="row">JSON 记录。</param>
    /// <param name="property">字段名称。</param>
    /// <param name="path">用于错误信息的记录路径。</param>
    /// <returns>解析后的整数。</returns>
    private static int ReadRequiredLubanInt(JsonElement row, string property, string path)
    {
        if (!TryGetPropertyIgnoreCase(row, property, out JsonElement value))
        {
            throw new InvalidDataException(path + " 缺少整数属性: " + property);
        }

        return ReadLubanInt(value, path + "." + property);
    }

    /// <summary>把 JSON 整数或 invariant 整数字符串转换为 Int32。</summary>
    /// <param name="value">待转换的 JSON 值。</param>
    /// <param name="path">字段路径。</param>
    /// <returns>合法整数。</returns>
    private static int ReadLubanInt(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int numericValue))
        {
            return numericValue;
        }

        if (value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numericValue))
        {
            return numericValue;
        }

        throw new InvalidDataException("整数属性无效: " + path);
    }

    /// <summary>读取可选字符串字段，字段缺失时返回空文本。</summary>
    /// <param name="row">JSON 记录。</param>
    /// <param name="property">字段名称。</param>
    /// <returns>字段值或空文本。</returns>
    private static string ReadOptionalLubanString(JsonElement row, string property)
    {
        if (!TryGetPropertyIgnoreCase(row, property, out JsonElement value))
        {
            return string.Empty;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(property + " 必须是字符串。");
        }

        return value.GetString() ?? string.Empty;
    }

    /// <summary>按忽略大小写的字段名读取 JSON 对象属性，兼容不同 Luban JSON target 的命名输出。</summary>
    /// <param name="row">JSON 对象。</param>
    /// <param name="property">逻辑字段名称。</param>
    /// <param name="value">匹配成功时的属性值。</param>
    /// <returns>找到属性时返回 true。</returns>
    private static bool TryGetPropertyIgnoreCase(JsonElement row, string property, out JsonElement value)
    {
        foreach (JsonProperty candidate in row.EnumerateObject())
        {
            if (string.Equals(candidate.Name, property, StringComparison.OrdinalIgnoreCase))
            {
                value = candidate.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
