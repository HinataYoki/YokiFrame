namespace YokiFrame.Tooling.Application.Models.TableKit;

/// <summary>表示生成契约中已经解析完成的运行时资源模板。</summary>
public sealed record TableKitRuntimeLocation
{
    /// <summary>是否直接使用 Luban 传入的表名作为资源地址。</summary>
    public required bool IsAddressable { get; init; }
    /// <summary>资源路径模板；可寻址模式固定为 `{0}`。</summary>
    public required string PathPattern { get; init; }
}

/// <summary>描述一次 Luban TableKit 验证或生成所需的 Workbench 配置。</summary>
public sealed record TableKitOptions
{
    /// <summary>当前宿主项目根目录。</summary>
    public required string ProjectRoot { get; init; }
    /// <summary>luban.conf 绝对路径。</summary>
    public required string LubanConfigPath { get; init; }
    /// <summary>Luban 工作目录；为空时使用配置文件目录。</summary>
    public string LubanWorkDir { get; init; } = string.Empty;
    /// <summary>Luban 可执行文件或 Luban.dll 路径。</summary>
    public string LubanExecutablePath { get; init; } = string.Empty;
    /// <summary>目标名称；默认生成客户端字段分组。</summary>
    public string TargetName { get; init; } = "client";
    /// <summary>代码生成 target，例如 cs-bin、cs-dotnet-json。</summary>
    public string CodeTarget { get; init; } = "cs-bin";
    /// <summary>数据生成 target；允许 Luban 支持的任意值。</summary>
    public string DataTarget { get; init; } = "bin";
    /// <summary>数据输出目录，可为项目根相对路径。</summary>
    public string OutputDataDir { get; init; } = "Assets/Resources/Art/Table/";
    /// <summary>代码输出目录，可为项目根相对路径。</summary>
    public string OutputCodeDir { get; init; } = "Assets/Scripts/TableKit/";
    /// <summary>是否由项目资源系统直接使用表名寻址；关闭时按运行时路径模板读取。</summary>
    public bool IsAddressable { get; init; }
    /// <summary>关闭可寻址时使用的运行时路径模板；为空时由宿主输出目录推导。</summary>
    public string RuntimePathPattern { get; init; } = string.Empty;
    /// <summary>生成的 Runtime 是否提供异步加载入口。</summary>
    public bool UseAsyncLoading { get; init; }
    /// <summary>是否允许加载器直接读取原始资源。</summary>
    public bool UseRawResourceLoading { get; init; } = true;
    /// <summary>是否按 Luban mapper 生成外部类型 helper；属性名保留为设置键。</summary>
    public bool GenerateExternalTypeUtil { get; init; }
    /// <summary>Unity 项目是否生成独立 asmdef；Godot 使用独立 csproj 边界。</summary>
    public bool UseAssemblyDefinition { get; init; }
    /// <summary>Unity asmdef 或 Godot csproj 使用的生成程序集名称。</summary>
    public string AssemblyName { get; init; } = "YokiFrame.TableKit";
    /// <summary>额外 Luban 输出目标列表。</summary>
    public IReadOnlyList<TableKitExtraOutput> ExtraOutputTargets { get; init; } = Array.Empty<TableKitExtraOutput>();
    /// <summary>是否使用自定义编辑器数据路径。</summary>
    public bool CustomEditorDataPath { get; init; }
    /// <summary>编辑器读取的配置数据路径。</summary>
    public string EditorDataPath { get; init; } = "Assets/Resources/Art/Table/";
}

/// <summary>描述一个额外的 Luban 代码与数据导出目标。</summary>
public sealed record TableKitExtraOutput
{
    /// <summary>额外输出使用的 Luban target。</summary>
    public string TargetName { get; init; } = "server";
    /// <summary>额外输出使用的代码 target。</summary>
    public string CodeTarget { get; init; } = "java-json";
    /// <summary>额外输出使用的数据 target。</summary>
    public string DataTarget { get; init; } = "json";
    /// <summary>额外数据输出目录。</summary>
    public string OutputDataDir { get; init; } = "Temp/LubanExtra/server/data";
    /// <summary>额外代码输出目录。</summary>
    public string OutputCodeDir { get; init; } = "Temp/LubanExtra/server/code";
}

/// <summary>表示一次验证生成的 JSON 预览表。</summary>
public sealed record TableKitPreviewTable
{
    /// <summary>表文件名。</summary>
    public required string Name { get; init; }
    /// <summary>表中记录数量。</summary>
    public int Count { get; init; }
    /// <summary>格式化后的 JSON 预览文本。</summary>
    public required string PreviewJson { get; init; }
}

/// <summary>表示 Luban 配置中解析出的实际表管理类型契约。</summary>
public sealed record TableKitContract
{
    /// <summary>配置文件绝对路径。</summary>
    public required string ConfigPath { get; init; }
    /// <summary>实际 target 名称。</summary>
    public required string TargetName { get; init; }
    /// <summary>实际 topModule 命名空间。</summary>
    public required string TopModule { get; init; }
    /// <summary>实际 manager 类型名。</summary>
    public required string Manager { get; init; }
    /// <summary>完整表管理器类型名。</summary>
    public string TablesType => TopModule + "." + Manager;
    /// <summary>是否由项目资源系统直接使用表名寻址。</summary>
    public bool IsAddressable { get; init; }
    /// <summary>生成门面传给 Loader 的运行时路径模板。</summary>
    public string RuntimePathPattern { get; init; } = "Art/Table/{0}";
    /// <summary>代码 target。</summary>
    public required string CodeTarget { get; init; }
    /// <summary>数据 target。</summary>
    public required string DataTarget { get; init; }
    /// <summary>数据文件扩展名。</summary>
    public required string DataExtension { get; init; }
    /// <summary>代码输出绝对目录。</summary>
    public required string OutputCodeDirectory { get; init; }
    /// <summary>数据输出绝对目录。</summary>
    public required string OutputDataDirectory { get; init; }
    /// <summary>契约版本。</summary>
    public int ContractVersion { get; init; } = 1;
    /// <summary>是否按 Luban mapper 生成外部类型 helper。</summary>
    public bool GenerateExternalTypeUtil { get; init; } = true;
    /// <summary>Unity 项目是否生成 asmdef；Godot 不消费该开关。</summary>
    public bool UseAssemblyDefinition { get; init; } = true;
    /// <summary>Unity asmdef 或 Godot csproj 使用的生成程序集名称。</summary>
    public string AssemblyName { get; init; } = "YokiFrame.TableKit";
    /// <summary>是否生成异步加载入口。</summary>
    public bool UseAsyncLoading { get; init; } = true;
    /// <summary>是否允许原始资源读取。</summary>
    public bool UseRawResourceLoading { get; init; }
    /// <summary>当前 Luban schema 中匹配 target/codeTarget 的外部类型映射。</summary>
    public IReadOnlyList<TableKitExternalTypeMapping> ExternalTypeMappings { get; init; } = Array.Empty<TableKitExternalTypeMapping>();
}

/// <summary>描述 Luban bean mapper 对应的外部类型构造入口。</summary>
public sealed record TableKitExternalTypeMapping
{
    /// <summary>生成后的 Luban bean 完整类型名。</summary>
    public required string SourceTypeName { get; init; }
    /// <summary>Luban mapper 的目标类型表达式。</summary>
    public required string TargetTypeName { get; init; }
    /// <summary>构造函数所属的命名空间。</summary>
    public required string HelperNamespace { get; init; }
    /// <summary>构造函数所属的静态类型名。</summary>
    public required string HelperTypeName { get; init; }
    /// <summary>构造函数方法名。</summary>
    public required string HelperMethodName { get; init; }
    /// <summary>bean 字段在 Luban C# 类型中的成员名。</summary>
    public required IReadOnlyList<string> MemberNames { get; init; }
}

/// <summary>Workbench TableKit 操作的结构化结果。</summary>
public sealed record TableKitOperationResult
{
    /// <summary>操作是否成功。</summary>
    public required bool Succeeded { get; init; }
    /// <summary>退出码；未启动进程时为 -1。</summary>
    public int ExitCode { get; init; } = -1;
    /// <summary>解析出的生成契约。</summary>
    public TableKitContract? Contract { get; init; }
    /// <summary>标准输出和错误输出合并日志。</summary>
    public string Log { get; init; } = string.Empty;
    /// <summary>生成或更新的文件清单。</summary>
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
    /// <summary>失败或非阻断诊断。</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
    /// <summary>验证阶段读取的临时 JSON 预览。</summary>
    public IReadOnlyList<TableKitPreviewTable> PreviewTables { get; init; } = Array.Empty<TableKitPreviewTable>();
    /// <summary>验证预览目录，便于诊断实际输出。</summary>
    public string PreviewDirectory { get; init; } = string.Empty;
}
