using System.Collections.ObjectModel;
using YokiFrame.Tooling.Application.Models.Luban;

namespace YokiFrame.Tooling.Application.Models.LocalizationKit;

/// <summary>描述一次 LocalizationKit 项目目录扫描。</summary>
public sealed record LocalizationKitOptions
{
    /// <summary>项目根目录。</summary>
    public required string ProjectRoot { get; init; }
    /// <summary>JSON 源文件路径；支持项目根相对路径。</summary>
    public string SourcePath { get; init; } = "Assets/Settings/YokiFrame/localization.json";
}

/// <summary>描述一个语言配置。</summary>
public sealed record LocalizationLanguageRecord
{
    /// <summary>稳定语言标识。</summary>
    public required string Id { get; init; }
    /// <summary>显示名文本编号。</summary>
    public int DisplayNameTextId { get; init; }
    /// <summary>原生名文本编号。</summary>
    public int NativeNameTextId { get; init; }
    /// <summary>图标资源编号。</summary>
    public int IconSpriteId { get; init; }
}

/// <summary>描述一个普通或复数本地化条目。</summary>
public sealed record LocalizationEntryRecord
{
    /// <summary>稳定文本编号。</summary>
    public required int Id { get; init; }
    /// <summary>可选策划键。</summary>
    public string Key { get; init; } = string.Empty;
    /// <summary>普通文本值，键为语言标识。</summary>
    public IReadOnlyDictionary<string, string> Values { get; init; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
    /// <summary>复数文本值，键依次为语言标识和分类。</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> PluralValues { get; init; } = new ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>(new Dictionary<string, IReadOnlyDictionary<string, string>>());
    /// <summary>当前条目缺失的语言列表。</summary>
    public IReadOnlyList<string> MissingLanguages { get; init; } = Array.Empty<string>();
    /// <summary>条目是否包含复数配置。</summary>
    public bool HasPlural => PluralValues.Count > 0;
    /// <summary>条目是否缺失至少一个语言。</summary>
    public bool HasMissing => MissingLanguages.Count > 0;

    /// <summary>判断指定语言是否存在非空普通文本或至少一个非空复数分类。</summary>
    /// <param name="languageId">需要检查的规范语言标识。</param>
    /// <returns>该语言存在可显示文本时返回 true。</returns>
    public bool HasValueFor(string languageId)
    {
        if (string.IsNullOrWhiteSpace(languageId))
        {
            return false;
        }

        string? value;
        if (Values.TryGetValue(languageId, out value) && !string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        IReadOnlyDictionary<string, string>? categories;
        return PluralValues.TryGetValue(languageId, out categories)
            && categories.Values.Any(static categoryValue => !string.IsNullOrWhiteSpace(categoryValue));
    }
}

/// <summary>表示可供 Workbench 和 CLI 消费的完整本地化目录。</summary>
public sealed record LocalizationCatalog
{
    /// <summary>Provider 名称。</summary>
    public string Provider { get; init; } = "Json";
    /// <summary>源文件绝对路径。</summary>
    public required string SourcePath { get; init; }
    /// <summary>数据格式版本。</summary>
    public int FormatVersion { get; init; } = 1;
    /// <summary>语言列表。</summary>
    public IReadOnlyList<LocalizationLanguageRecord> Languages { get; init; } = Array.Empty<LocalizationLanguageRecord>();
    /// <summary>文本条目列表。</summary>
    public IReadOnlyList<LocalizationEntryRecord> Entries { get; init; } = Array.Empty<LocalizationEntryRecord>();
    /// <summary>扫描诊断。</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
    /// <summary>完全没有缺失语言的条目数量。</summary>
    public int CompleteEntryCount => Entries.Count(entry => entry.MissingLanguages.Count == 0);
    /// <summary>存在缺失语言的条目数量。</summary>
    public int MissingEntryCount => Entries.Count - CompleteEntryCount;
}

/// <summary>描述一次搜索请求。</summary>
public sealed record LocalizationSearchRequest
{
    /// <summary>项目扫描配置。</summary>
    public required LocalizationKitOptions Options { get; init; }
    /// <summary>关键字；为空时返回全部条目。</summary>
    public string Keyword { get; init; } = string.Empty;
    /// <summary>只返回存在缺失语言的条目。</summary>
    public bool MissingOnly { get; init; }
    /// <summary>最多返回条目数量。</summary>
    public int Limit { get; init; } = 200;
}

/// <summary>描述补充一条本地化配置的请求。</summary>
public sealed record LocalizationAddRequest
{
    /// <summary>项目扫描配置。</summary>
    public required LocalizationKitOptions Options { get; init; }
    /// <summary>文本编号。</summary>
    public required int TextId { get; init; }
    /// <summary>目标语言标识。</summary>
    public required string Language { get; init; }
    /// <summary>要写入的文本。</summary>
    public required string Value { get; init; }
    /// <summary>可选复数分类；为空表示普通文本。</summary>
    public string PluralCategory { get; init; } = string.Empty;
    /// <summary>已有值时是否允许覆盖。</summary>
    public bool Force { get; init; }
}

/// <summary>描述创建 Luban XML schema 和 Excel 本地化模板的请求。</summary>
public sealed record LocalizationLubanTemplateRequest
{
    /// <summary>当前项目根目录。</summary>
    public required string ProjectRoot { get; init; }
    /// <summary>显式 Luban 参数；为空时按项目常见目录发现唯一工具。</summary>
    public LubanToolOptions? Tool { get; init; }
    /// <summary>可选的项目内 Luban 工作目录；为空时自动发现。</summary>
    public string LubanWorkDir { get; init; } = string.Empty;
    /// <summary>模板应生成的语言列；为空时使用简体中文和英文。</summary>
    public IReadOnlyList<string> Languages { get; init; } = new[] { "ChineseSimplified", "English" };
    /// <summary>已有 XML 或 Excel 时是否允许覆盖两个作者文件。</summary>
    public bool Force { get; init; }
}

/// <summary>描述一次从 Luban 临时 JSON 输出构建 LocalizationKit 目录的请求。</summary>
public sealed record LocalizationLubanPreviewRequest
{
    /// <summary>当前项目根目录。</summary>
    public required string ProjectRoot { get; init; }
    /// <summary>显式 Luban 参数；为空时按项目常见目录发现唯一工具。</summary>
    public LubanToolOptions? Tool { get; init; }
    /// <summary>可选的项目内 Luban 工作目录；为空时自动发现。</summary>
    public string LubanWorkDir { get; init; } = string.Empty;
}

/// <summary>保存 LocalizationKit Workbench 独占的项目级作者目录配置。</summary>
public sealed record LocalizationKitWorkbenchSettings
{
    /// <summary>用户选择的项目内 Luban 工作目录；为空时回落自动发现。</summary>
    public string LubanWorkDir { get; init; } = string.Empty;
}

/// <summary>表示 LocalizationKit Luban 作者 Excel 的可定位工作区。</summary>
public sealed record LocalizationLubanWorkspaceResult
{
    /// <summary>是否成功解析配置和作者目录位置。</summary>
    public required bool Succeeded { get; init; }
    /// <summary>有效 luban.conf 所在工作目录。</summary>
    public string WorkDirectory { get; init; } = string.Empty;
    /// <summary>LocalizationKit Excel 所在目录。</summary>
    public string WorkbookDirectory { get; init; } = string.Empty;
    /// <summary>LocalizationKit Excel 完整路径。</summary>
    public string WorkbookPath { get; init; } = string.Empty;
    /// <summary>无法定位时的可显示诊断。</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
}

/// <summary>统一返回搜索、检查、写入和模板操作结果。</summary>
public sealed record LocalizationOperationResult
{
    /// <summary>操作是否成功。</summary>
    public required bool Succeeded { get; init; }
    /// <summary>操作尝试或成功读取的数据源；失败时供入口层保持来源状态准确。</summary>
    public string Provider { get; init; } = string.Empty;
    /// <summary>当前目录快照。</summary>
    public LocalizationCatalog? Catalog { get; init; }
    /// <summary>筛选后的条目。</summary>
    public IReadOnlyList<LocalizationEntryRecord> Entries { get; init; } = Array.Empty<LocalizationEntryRecord>();
    /// <summary>诊断或失败原因。</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
    /// <summary>实际写入的文件。</summary>
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
    /// <summary>Luban 预览成功时对应的临时输出目录。</summary>
    public string PreviewDirectory { get; init; } = string.Empty;
}
