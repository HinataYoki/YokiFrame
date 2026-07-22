using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame;
using YokiFrame.Tooling.Application.Models.LocalizationKit;

namespace YokiFrame.Tooling.Application.Services.LocalizationKit;

/// <summary>提供 LocalizationKit 目录扫描、搜索、缺失诊断、补充写入和 Luban 模板生成用例。</summary>
public sealed partial class LocalizationKitApplicationService
{
    private const int DEFAULT_LIMIT = 200;

    /// <summary>读取项目内 JSON 目录并生成稳定的强类型目录模型。</summary>
    /// <param name="options">项目和源文件选项。</param>
    /// <returns>本地化目录。</returns>
    public LocalizationCatalog Load(LocalizationKitOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        string sourcePath = ResolveContainedPath(options.ProjectRoot, options.SourcePath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("找不到 LocalizationKit JSON 源文件。", sourcePath);
        }

        return ParseCatalog(sourcePath, File.ReadAllText(sourcePath));
    }

    /// <summary>按关键字和缺失状态筛选本地化条目。</summary>
    /// <param name="request">搜索请求。</param>
    /// <returns>搜索结果和目录统计。</returns>
    public LocalizationOperationResult Search(LocalizationSearchRequest request)
    {
        try
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            LocalizationCatalog catalog = Load(request.Options);
            return new LocalizationOperationResult
            {
                Succeeded = true,
                Catalog = catalog,
                Entries = Filter(catalog, request.Keyword, request.MissingOnly, request.Limit)
            };
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or JsonException or UnauthorizedAccessException)
        {
            return new LocalizationOperationResult { Succeeded = false, Diagnostics = new[] { exception.Message } };
        }
    }

    /// <summary>在已加载目录中执行纯内存筛选，避免页面筛选重复读取 JSON 文件。</summary>
    /// <param name="catalog">已经通过 schema 校验的目录。</param>
    /// <param name="keyword">要匹配的关键字；为空时不限制。</param>
    /// <param name="missingOnly">是否只保留缺失语言的条目。</param>
    /// <param name="limit">最多返回的条目数；非正数时使用默认上限。</param>
    /// <returns>符合筛选条件的稳定条目列表。</returns>
    public IReadOnlyList<LocalizationEntryRecord> Filter(
        LocalizationCatalog catalog,
        string? keyword,
        bool missingOnly,
        int limit = DEFAULT_LIMIT)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));

        string normalizedKeyword = keyword?.Trim() ?? string.Empty;
        int normalizedLimit = limit <= 0 ? DEFAULT_LIMIT : limit;
        return catalog.Entries
            .Where(entry => !missingOnly || entry.HasMissing)
            .Where(entry => Matches(entry, normalizedKeyword))
            .Take(normalizedLimit)
            .ToArray();
    }

    /// <summary>检查所有条目的语言覆盖率，并返回缺失条目。</summary>
    /// <param name="options">项目和源文件选项。</param>
    /// <returns>包含缺失条目的检查结果。</returns>
    public LocalizationOperationResult Check(LocalizationKitOptions options)
    {
        return Search(new LocalizationSearchRequest
        {
            Options = options,
            MissingOnly = true,
            Limit = int.MaxValue
        });
    }

    /// <summary>向 JSON 源文件补充一条文本，并在原子写入前重新验证完整 schema。</summary>
    /// <param name="request">补充请求。</param>
    /// <returns>写入结果和更新后的目录。</returns>
    public LocalizationOperationResult Add(LocalizationAddRequest request)
    {
        try
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (request.Options is null) throw new ArgumentException("LocalizationKit 选项不能为空。", nameof(request));

            string sourcePath = ResolveContainedPath(request.Options.ProjectRoot, request.Options.SourcePath);
            using SourceWriteLock writeLock = AcquireSourceWriteLock(sourcePath);
            sourcePath = ResolveContainedPath(request.Options.ProjectRoot, request.Options.SourcePath);
            return AddLocked(request, sourcePath);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or InvalidOperationException or JsonException or UnauthorizedAccessException)
        {
            return new LocalizationOperationResult { Succeeded = false, Diagnostics = new[] { exception.Message } };
        }
    }

    /// <summary>在源文件独占锁内重读最新内容、合并单条文本并原子提交，避免并发调用覆盖彼此更新。</summary>
    /// <param name="request">已经完成基础空值校验的补充请求。</param>
    /// <param name="sourcePath">已通过项目根和重解析点校验的绝对源路径。</param>
    /// <returns>写入结果和提交后的目录快照。</returns>
    private static LocalizationOperationResult AddLocked(LocalizationAddRequest request, string sourcePath)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("找不到 LocalizationKit JSON 源文件。", sourcePath);

        string sourceContent = File.ReadAllText(sourcePath);
        LocalizationCatalog currentCatalog = ParseCatalog(sourcePath, sourceContent);
        string languageId = NormalizeLanguageId(request.Language, "目标语言");
        string pluralCategory = NormalizePluralCategory(request.PluralCategory);
        if (string.IsNullOrWhiteSpace(request.Value)) throw new ArgumentException("Value 不能为空。", nameof(request));
        if (!currentCatalog.Languages.Any(language => string.Equals(language.Id, languageId, StringComparison.Ordinal)))
            throw new InvalidDataException("目标语言未在 languages 中声明: " + languageId);

        JsonObject rootObject = JsonNode.Parse(sourceContent) as JsonObject
            ?? throw new InvalidDataException("LocalizationKit JSON 根节点必须是对象。");
        JsonArray texts = rootObject["texts"] as JsonArray
            ?? throw new InvalidDataException("LocalizationKit JSON 缺少 texts 数组。");
        JsonObject entry = texts.OfType<JsonObject>().FirstOrDefault(candidate => ReadNodeInt(candidate["id"]) == request.TextId)
            ?? CreateEntry(texts, request.TextId);
        JsonObject target = pluralCategory.Length == 0
            ? GetOrCreateObject(entry, "values")
            : GetOrCreateObject(GetOrCreateObject(entry, "plural"), languageId);
        string key = pluralCategory.Length == 0 ? languageId : pluralCategory;
        if (target[key] is not null && !request.Force)
            throw new InvalidOperationException("目标文本已存在，使用 force 才能覆盖。");
        target[key] = request.Value;

        string content = rootObject.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        LocalizationCatalog updatedCatalog = ParseCatalog(sourcePath, content);
        WriteAtomically(sourcePath, content);
        return new LocalizationOperationResult { Succeeded = true, Catalog = updatedCatalog, Files = new[] { sourcePath } };
    }

    /// <summary>向 texts 数组追加一个只包含稳定编号的新条目，并返回供当前写入继续填充的对象。</summary>
    /// <param name="texts">已通过 schema 校验的文本数组。</param>
    /// <param name="textId">新条目的文本编号。</param>
    /// <returns>已经加入数组的新条目。</returns>
    private static JsonObject CreateEntry(JsonArray texts, int textId)
    {
        JsonObject entry = new() { ["id"] = textId };
        texts.Add(entry);
        return entry;
    }

    /// <summary>从 JSON 文本解析并验证完整目录，使加载和写入提交使用相同 schema 规则。</summary>
    /// <param name="sourcePath">归档显示和错误定位使用的绝对源路径。</param>
    /// <param name="json">待解析的 JSON 文本。</param>
    /// <returns>不可变视角的目录快照。</returns>
    private static LocalizationCatalog ParseCatalog(string sourcePath, string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("LocalizationKit JSON 根节点必须是对象。");

        int formatVersion = ReadFormatVersion(root);
        List<LocalizationLanguageRecord> languages = ParseLanguages(root, out HashSet<string> languageSet);
        List<LocalizationEntryRecord> entries = ParseEntries(root, languages, languageSet);
        return new LocalizationCatalog
        {
            SourcePath = sourcePath,
            FormatVersion = formatVersion,
            Languages = languages,
            Entries = entries
        };
    }

    /// <summary>读取并校验当前 JSON 格式版本，缺省时保持 v1 行为。</summary>
    private static int ReadFormatVersion(JsonElement root)
    {
        int formatVersion = root.TryGetProperty("formatVersion", out JsonElement value)
            ? ReadInt(value, "formatVersion")
            : LocalizationSchema.CurrentFormatVersion;
        if (formatVersion != LocalizationSchema.CurrentFormatVersion)
            throw new InvalidDataException("不支持的 LocalizationKit formatVersion: " + formatVersion);
        return formatVersion;
    }

    /// <summary>解析语言表，规范化枚举名称并拒绝重复语言。</summary>
    private static List<LocalizationLanguageRecord> ParseLanguages(JsonElement root, out HashSet<string> languageSet)
    {
        if (!root.TryGetProperty("languages", out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("LocalizationKit JSON 缺少 languages 数组。");

        List<LocalizationLanguageRecord> result = new();
        languageSet = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement language in values.EnumerateArray())
        {
            if (language.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("languages[" + index + "] 必须是对象。");

            if (!language.TryGetProperty("id", out JsonElement idValue))
                throw new InvalidDataException("languages[" + index + "] 缺少 id。");
            string id = ReadLanguageId(idValue, "languages[" + index + "].id");
            if (!languageSet.Add(id)) throw new InvalidDataException("重复语言: " + id);
            result.Add(new LocalizationLanguageRecord
            {
                Id = id,
                DisplayNameTextId = ReadOptionalInt(language, "displayNameTextId"),
                NativeNameTextId = ReadOptionalInt(language, "nativeNameTextId"),
                IconSpriteId = ReadOptionalInt(language, "iconSpriteId")
            });
            index++;
        }

        return result;
    }

    /// <summary>解析普通值、复数值并按声明语言顺序计算缺失项。</summary>
    private static List<LocalizationEntryRecord> ParseEntries(
        JsonElement root,
        IReadOnlyList<LocalizationLanguageRecord> languages,
        HashSet<string> languageSet)
    {
        if (!root.TryGetProperty("texts", out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("LocalizationKit JSON 缺少 texts 数组。");

        List<LocalizationEntryRecord> result = new();
        HashSet<int> textIds = new();
        int index = 0;
        foreach (JsonElement entry in values.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("texts[" + index + "] 必须是对象。");

            int id = ReadRequiredInt(entry, "id", "texts[" + index + "]");
            if (!textIds.Add(id)) throw new InvalidDataException("重复文本 ID: " + id);
            LocalizationEntryRecord record = new()
            {
                Id = id,
                Key = ReadOptionalString(entry, "key", "texts[" + index + "]"),
                Values = ReadStringMap(entry, "values", languageSet, "texts[" + index + "]"),
                PluralValues = ReadPluralMap(entry, languageSet, "texts[" + index + "]")
            };
            result.Add(record with
            {
                MissingLanguages = languages
                    .Where(language => !record.HasValueFor(language.Id))
                    .Select(static language => language.Id)
                    .ToArray()
            });
            index++;
        }

        return result;
    }

    /// <summary>判断条目是否命中关键字。</summary>
    private static bool Matches(LocalizationEntryRecord entry, string keyword)
    {
        if (keyword.Length == 0) return true;
        if (entry.Id.ToString(CultureInfo.InvariantCulture).Contains(keyword, StringComparison.OrdinalIgnoreCase)) return true;
        if (entry.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return true;
        return entry.Values.Any(pair => pair.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || pair.Value.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            || entry.PluralValues.Any(language => language.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || language.Value.Values.Any(value => value.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>读取普通文本映射，并拒绝未知语言、重复语言和非字符串值。</summary>
    private static Dictionary<string, string> ReadStringMap(
        JsonElement parent,
        string property,
        HashSet<string> languageSet,
        string path)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        if (!parent.TryGetProperty(property, out JsonElement values)) return result;
        if (values.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(path + "." + property + " 必须是对象。");

        foreach (JsonProperty value in values.EnumerateObject())
        {
            string languageId = NormalizeLanguageId(value.Name, path + "." + property);
            if (!languageSet.Contains(languageId)) throw new InvalidDataException("文本引用了未声明语言: " + languageId);
            if (value.Value.ValueKind != JsonValueKind.String)
                throw new InvalidDataException(path + "." + property + "." + value.Name + " 必须是字符串。");
            if (!result.TryAdd(languageId, value.Value.GetString() ?? string.Empty))
                throw new InvalidDataException("文本重复声明语言: " + languageId);
        }

        return result;
    }

    /// <summary>读取复数文本映射，并拒绝未知语言、无效分类和非字符串值。</summary>
    private static Dictionary<string, IReadOnlyDictionary<string, string>> ReadPluralMap(
        JsonElement parent,
        HashSet<string> languageSet,
        string path)
    {
        Dictionary<string, IReadOnlyDictionary<string, string>> result = new(StringComparer.Ordinal);
        if (!parent.TryGetProperty("plural", out JsonElement values)) return result;
        if (values.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(path + ".plural 必须是对象。");

        foreach (JsonProperty language in values.EnumerateObject())
        {
            string languageId = NormalizeLanguageId(language.Name, path + ".plural");
            if (!languageSet.Contains(languageId)) throw new InvalidDataException("复数文本引用了未声明语言: " + languageId);
            if (language.Value.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException(path + ".plural." + language.Name + " 必须是对象。");

            Dictionary<string, string> categories = new(StringComparer.Ordinal);
            foreach (JsonProperty category in language.Value.EnumerateObject())
            {
                string categoryName = NormalizePluralCategory(category.Name);
                if (category.Value.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException(path + ".plural." + language.Name + "." + category.Name + " 必须是字符串。");
                if (!categories.TryAdd(categoryName, category.Value.GetString() ?? string.Empty))
                    throw new InvalidDataException("复数文本重复声明分类: " + categoryName);
            }

            if (!result.TryAdd(languageId, categories))
                throw new InvalidDataException("复数文本重复声明语言: " + languageId);
        }

        return result;
    }

    /// <summary>读取必需整数属性，并保留字段路径用于诊断。</summary>
    private static int ReadRequiredInt(JsonElement parent, string property, string path)
    {
        if (!parent.TryGetProperty(property, out JsonElement value))
            throw new InvalidDataException(path + " 缺少整数属性: " + property);
        return ReadInt(value, path + "." + property);
    }

    /// <summary>读取可选整数属性；缺失时返回零。</summary>
    private static int ReadOptionalInt(JsonElement parent, string property)
    {
        return parent.TryGetProperty(property, out JsonElement value)
            ? ReadInt(value, property)
            : 0;
    }

    /// <summary>读取 JSON 整数或 invariant 数字字符串。</summary>
    private static int ReadInt(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)) return number;
        if (value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        throw new InvalidDataException("整数属性无效: " + path);
    }

    /// <summary>读取可选字符串属性，缺失时返回空文本。</summary>
    private static string ReadOptionalString(JsonElement parent, string property, string path)
    {
        if (!parent.TryGetProperty(property, out JsonElement value)) return string.Empty;
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException(path + "." + property + " 必须是字符串。");
        return value.GetString() ?? string.Empty;
    }

    /// <summary>读取语言 JSON 值并转换为公开枚举的规范名称。</summary>
    private static string ReadLanguageId(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return NormalizeLanguageId(value.GetString() ?? string.Empty, path);
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int numericValue)
            && LocalizationSchema.TryParseLanguageId(numericValue, out LanguageId languageId))
        {
            return languageId.ToString();
        }

        throw new InvalidDataException("语言标识无效: " + path);
    }

    /// <summary>规范化名称或数字形式的语言标识，并拒绝 Runtime 不支持的值。</summary>
    private static string NormalizeLanguageId(string? value, string path)
    {
        if (!LocalizationSchema.TryParseLanguageId(value?.Trim() ?? string.Empty, out LanguageId languageId))
            throw new InvalidDataException("语言标识无效: " + path);
        return languageId.ToString();
    }

    /// <summary>规范化可选复数分类；空值表示普通文本。</summary>
    private static string NormalizePluralCategory(string? value)
    {
        string category = value?.Trim() ?? string.Empty;
        if (category.Length == 0) return string.Empty;
        if (!LocalizationSchema.TryParsePluralCategory(category, out PluralCategory parsedCategory))
            throw new InvalidDataException("复数分类无效: " + category);
        return parsedCategory.ToString();
    }

    /// <summary>读取 JSON 节点整数；调用前已由完整 schema 校验保证条目结构。</summary>
    private static int ReadNodeInt(JsonNode? value)
    {
        if (value is not JsonValue jsonValue)
        {
            return int.MinValue;
        }

        if (jsonValue.TryGetValue<int>(out int number))
        {
            return number;
        }

        return jsonValue.TryGetValue<JsonElement>(out JsonElement element) && element.TryGetInt32(out number)
            ? number
            : int.MinValue;
    }

    /// <summary>获取或创建对象属性；已有非对象节点会被拒绝，避免写入时静默丢失结构。</summary>
    private static JsonObject GetOrCreateObject(JsonObject parent, string property)
    {
        if (parent[property] is null)
        {
            JsonObject result = new();
            parent[property] = result;
            return result;
        }

        if (parent[property] is JsonObject objectValue) return objectValue;
        throw new InvalidDataException("LocalizationKit JSON 属性必须是对象: " + property);
    }

}
