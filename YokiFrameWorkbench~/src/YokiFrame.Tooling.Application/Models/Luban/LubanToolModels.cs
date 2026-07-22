namespace YokiFrame.Tooling.Application.Models.Luban;

/// <summary>描述一次 Luban 工具调用所需的项目与进程参数。</summary>
public sealed record LubanToolOptions
{
    /// <summary>当前宿主项目根目录，用于校验临时输出目录。</summary>
    public required string ProjectRoot { get; init; }

    /// <summary>luban.conf 的绝对或项目根相对路径。</summary>
    public string LubanConfigPath { get; init; } = string.Empty;

    /// <summary>Luban 进程工作目录；相对路径以项目根为基准，为空时使用 luban.conf 所在目录。</summary>
    public string LubanWorkDir { get; init; } = string.Empty;

    /// <summary>Luban 可执行文件或 Luban.dll 的绝对或项目根相对路径。</summary>
    public string LubanExecutablePath { get; init; } = string.Empty;

    /// <summary>本次调用的 Luban target 名称。</summary>
    public string TargetName { get; init; } = "client";
}

/// <summary>表示一次 Luban 外部进程调用的退出状态与合并日志。</summary>
public sealed record LubanCommandResult
{
    /// <summary>进程是否以零退出码结束。</summary>
    public required bool Succeeded { get; init; }

    /// <summary>进程退出码；未能启动时为 -1。</summary>
    public int ExitCode { get; init; } = -1;

    /// <summary>标准输出和错误输出的合并文本。</summary>
    public string Log { get; init; } = string.Empty;
}

/// <summary>描述 Luban 临时 JSON 输出中的一个可浏览表。</summary>
public sealed record LubanJsonPreviewTable
{
    /// <summary>JSON 文件去除扩展名后的稳定表名。</summary>
    public required string Name { get; init; }

    /// <summary>预览中推断出的记录数。</summary>
    public int Count { get; init; }

    /// <summary>限制大小并格式化后的 JSON 文本。</summary>
    public required string PreviewJson { get; init; }
}

/// <summary>表示一次 Luban JSON 预览生成的结构化结果。</summary>
public sealed record LubanJsonPreviewResult
{
    /// <summary>命令与预览读取是否都成功完成。</summary>
    public required bool Succeeded { get; init; }

    /// <summary>Luban 进程退出码；未启动时为 -1。</summary>
    public int ExitCode { get; init; } = -1;

    /// <summary>标准输出、错误输出与预览诊断的合并日志。</summary>
    public string Log { get; init; } = string.Empty;

    /// <summary>本次独占的临时预览目录。</summary>
    public string PreviewDirectory { get; init; } = string.Empty;

    /// <summary>已在预算内读取的 JSON 表预览。</summary>
    public IReadOnlyList<LubanJsonPreviewTable> Tables { get; init; } = Array.Empty<LubanJsonPreviewTable>();

    /// <summary>非阻断读取诊断或失败原因。</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
}

/// <summary>描述 luban.conf 中一个 schemaFiles 来源的解析结果。</summary>
public sealed record LubanSchemaSource
{
    /// <summary>luban.conf 中的原始 fileName 值。</summary>
    public required string FileName { get; init; }

    /// <summary>schemaFiles 中声明的类型；为空时由 Luban 自动识别。</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>相对于 luban.conf 解析后的完整路径。</summary>
    public required string FullPath { get; init; }

    /// <summary>该来源是否是包含多个 schema 的目录。</summary>
    public bool IsDirectory { get; init; }
}

/// <summary>表示供 Tooling 使用的 luban.conf 最小结构投影。</summary>
public sealed record LubanConfiguration
{
    /// <summary>规范化后的 luban.conf 绝对路径。</summary>
    public required string ConfigPath { get; init; }

    /// <summary>luban.conf 所在目录。</summary>
    public required string ConfigDirectory { get; init; }

    /// <summary>由 dataDir 解析得到的数据目录。</summary>
    public required string DataDirectory { get; init; }

    /// <summary>配置中可用 target 名称。</summary>
    public IReadOnlyList<string> TargetNames { get; init; } = Array.Empty<string>();

    /// <summary>schemaFiles 的路径投影。</summary>
    public IReadOnlyList<LubanSchemaSource> SchemaSources { get; init; } = Array.Empty<LubanSchemaSource>();

    /// <summary>schemaFiles 目录和显式文件展开后的 XML 定义文件。</summary>
    public IReadOnlyList<string> DefinitionFiles { get; init; } = Array.Empty<string>();
}

/// <summary>表示按项目常见目录发现 Luban 工具与配置的结果。</summary>
public sealed record LubanToolDiscoveryResult
{
    /// <summary>是否得到可执行的 Luban 工具参数。</summary>
    public required bool Succeeded { get; init; }

    /// <summary>发现成功时可直接用于预览或生成的调用选项。</summary>
    public LubanToolOptions? Options { get; init; }

    /// <summary>已定位到 luban.conf 时的配置投影；工具文件缺失时仍用于避免错误回退来源。</summary>
    public LubanConfiguration? Configuration { get; init; }

    /// <summary>发现失败或歧义时的具体诊断。</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
}
