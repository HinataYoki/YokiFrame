using System.Text.Json;
using YokiFrame;
using YokiFrame.Tooling.Application.Models.LocalizationKit;
using YokiFrame.Tooling.Application.Models.Luban;
using YokiFrame.Tooling.Application.Services.Luban;

namespace YokiFrame.Tooling.Application.Services.LocalizationKit;

/// <summary>承载 LocalizationKit 自有 Luban 模板、注册检查和 JSON 预览编排用例。</summary>
public sealed partial class LocalizationKitApplicationService
{
    private const string LUBAN_SCHEMA_FILE_NAME = "LocalizationKit.xml";
    private const string LUBAN_WORKBOOK_DIRECTORY_NAME = "LocalizationKit";
    private const string LUBAN_WORKBOOK_FILE_NAME = "LocalizationKit.xlsx";
    private const string LUBAN_PREVIEW_DIRECTORY_NAME = "LocalizationKit";
    private const string LUBAN_ENTRY_TABLE_NAME = "LocalizationEntryTable";
    private const string LUBAN_ENTRY_BEAN_NAME = "LocalizationEntry";
    private const string LUBAN_TRANSLATIONS_BEAN_NAME = "LocalizationTranslations";
    private const string LUBAN_VALUE_KIND_ENUM_NAME = "LocalizationValueKind";
    private const string LUBAN_VARIANTS_FIELD_NAME = "variants";
    private const string LUBAN_NORMAL_VALUE_KIND_NAME = "Text";

    /// <summary>创建 LocalizationKit 自有的 Luban XML schema 和 Excel 模板，不修改 luban.conf。</summary>
    /// <param name="request">项目、可选工具参数、语言列和覆盖策略。</param>
    /// <returns>已写入文件和 schema 注册提示。</returns>
    public LocalizationOperationResult GenerateLubanTemplate(LocalizationLubanTemplateRequest request)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            LocalizationLubanPlan plan = ResolveLubanPlan(request.ProjectRoot, request.Tool, request.LubanWorkDir);
            IReadOnlyList<LanguageId> languages = NormalizeTemplateLanguages(request.Languages);
            WriteLubanTemplateFiles(plan, languages, request.Force);
            List<string> diagnostics = new();
            if (!plan.IsSchemaRegistered)
            {
                diagnostics.Add("XML 尚未被 luban.conf 的 schemaFiles 注册。添加后再执行预览: " + plan.RegistrationHint);
            }

            return new LocalizationOperationResult
            {
                Succeeded = true,
                Provider = "Luban",
                Files = new[] { plan.SchemaPath, plan.WorkbookPath },
                Diagnostics = diagnostics
            };
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new LocalizationOperationResult { Succeeded = false, Provider = "Luban", Diagnostics = new[] { exception.Message } };
        }
    }

    /// <summary>调用 Luban 导出单表临时 JSON，并投影为与 JSON standalone 相同的本地化目录模型。</summary>
    /// <param name="request">项目和可选显式 Luban 参数。</param>
    /// <param name="cancellationToken">取消时停止 Luban 子进程。</param>
    /// <returns>可供 Workbench 和 CLI 使用的目录或明确失败诊断。</returns>
    public async Task<LocalizationOperationResult> PreviewLubanAsync(
        LocalizationLubanPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            LocalizationLubanPlan plan = ResolveLubanPlan(request.ProjectRoot, request.Tool, request.LubanWorkDir);
            if (!File.Exists(plan.SchemaPath))
            {
                return Failed("未找到 LocalizationKit Luban schema: " + plan.SchemaPath);
            }

            if (!plan.IsSchemaRegistered)
            {
                return Failed("LocalizationKit XML 未被 luban.conf 注册: " + plan.RegistrationHint);
            }

            LubanToolOptions tool = plan.Tool
                ?? throw new InvalidDataException("LocalizationKit Luban 预览缺少可执行工具参数。");
            return await new LubanJsonPreviewService()
                .GenerateAndReadDirectoryAsync(tool, plan.PreviewDirectory, preview =>
                {
                    if (!preview.Succeeded)
                    {
                        return new LocalizationOperationResult
                        {
                            Succeeded = false,
                            Provider = "Luban",
                            Diagnostics = preview.Diagnostics.Concat(new[] { preview.Log.Trim() }).Where(static value => value.Length > 0).ToArray(),
                            PreviewDirectory = preview.PreviewDirectory
                        };
                    }

                    LocalizationCatalog catalog = ParseLubanCatalogFromPreviewDirectory(
                        preview.PreviewDirectory,
                        plan.WorkbookPath,
                        plan.SchemaPath,
                        preview.Diagnostics);
                    return new LocalizationOperationResult
                    {
                        Succeeded = true,
                        Provider = "Luban",
                        Catalog = catalog,
                        Entries = catalog.Entries,
                        Diagnostics = preview.Diagnostics,
                        PreviewDirectory = preview.PreviewDirectory
                    };
                }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
        {
            return new LocalizationOperationResult { Succeeded = false, Provider = "Luban", Diagnostics = new[] { exception.Message } };
        }
    }

    /// <summary>解析当前 Luban 工作目录与 LocalizationKit Excel 作者目录，不执行模板写入或外部进程。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="lubanWorkDir">可选的项目内工作目录；为空时自动发现。</param>
    /// <returns>可打开的作者目录，或明确配置诊断。</returns>
    public LocalizationLubanWorkspaceResult ResolveLubanWorkspace(string projectRoot, string lubanWorkDir = "")
    {
        try
        {
            string root = Path.GetFullPath(projectRoot);
            LubanToolDiscoveryResult discovery = new LubanProjectDiscoveryService().Discover(root, lubanWorkDir);
            if (discovery.Configuration == null)
            {
                return new LocalizationLubanWorkspaceResult
                {
                    Succeeded = false,
                    Diagnostics = discovery.Diagnostics
                };
            }

            LocalizationLubanPlan plan = CreateLubanPlan(root, discovery.Configuration, discovery.Options);
            return new LocalizationLubanWorkspaceResult
            {
                Succeeded = true,
                WorkDirectory = plan.WorkDirectory,
                WorkbookDirectory = Path.GetDirectoryName(plan.WorkbookPath)!,
                WorkbookPath = plan.WorkbookPath
            };
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new LocalizationLubanWorkspaceResult
            {
                Succeeded = false,
                Diagnostics = new[] { exception.Message }
            };
        }
    }

    /// <summary>优先读取已注册的 LocalizationKit Luban schema；不存在 schema 时保留 JSON standalone 行为。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="jsonSourcePath">standalone JSON 源文件路径。</param>
    /// <param name="cancellationToken">取消 Luban 预览的令牌。</param>
    /// <returns>优先来源的目录结果。</returns>
    public Task<LocalizationOperationResult> LoadPreferredAsync(
        string projectRoot,
        string jsonSourcePath,
        CancellationToken cancellationToken = default)
    {
        return LoadPreferredAsync(projectRoot, jsonSourcePath, string.Empty, cancellationToken);
    }

    /// <summary>按可选的项目内 Luban 工作目录优先读取本地化目录；显式目录失效时不回退旧 JSON。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="jsonSourcePath">standalone JSON 源文件路径。</param>
    /// <param name="lubanWorkDir">可选的项目内工作目录；为空时自动发现。</param>
    /// <param name="cancellationToken">取消 Luban 预览的令牌。</param>
    /// <returns>优先来源的目录结果。</returns>
    public async Task<LocalizationOperationResult> LoadPreferredAsync(
        string projectRoot,
        string jsonSourcePath,
        string lubanWorkDir,
        CancellationToken cancellationToken = default)
    {
        LubanToolDiscoveryResult discovery = new LubanProjectDiscoveryService().Discover(projectRoot, lubanWorkDir);
        if (discovery.Configuration == null)
        {
            if (!string.IsNullOrWhiteSpace(lubanWorkDir))
            {
                return new LocalizationOperationResult
                {
                    Succeeded = false,
                    Provider = "Luban",
                    Diagnostics = discovery.Diagnostics
                };
            }

            return Search(new LocalizationSearchRequest
            {
                Options = new LocalizationKitOptions { ProjectRoot = projectRoot, SourcePath = jsonSourcePath },
                Limit = int.MaxValue
            });
        }

        LocalizationLubanPlan plan = CreateLubanPlan(Path.GetFullPath(projectRoot), discovery.Configuration, discovery.Options);
        if (!File.Exists(plan.SchemaPath))
        {
            return Search(new LocalizationSearchRequest
            {
                Options = new LocalizationKitOptions { ProjectRoot = projectRoot, SourcePath = jsonSourcePath },
                Limit = int.MaxValue
            });
        }

        if (!discovery.Succeeded || discovery.Options == null)
        {
            return new LocalizationOperationResult { Succeeded = false, Provider = "Luban", Diagnostics = discovery.Diagnostics };
        }

        return await PreviewLubanAsync(new LocalizationLubanPreviewRequest
        {
            ProjectRoot = projectRoot,
            Tool = discovery.Options,
            LubanWorkDir = lubanWorkDir
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>解析请求提供的或自动发现的工具参数，并建立模板与预览使用的统一路径计划。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="explicitTool">调用方显式指定的 Luban 参数；为空时执行项目发现。</param>
    /// <param name="lubanWorkDir">可选的项目内工作目录；仅自动发现路径使用。</param>
    /// <returns>已完成 containment、target 和 schema 注册判断的计划。</returns>
    private static LocalizationLubanPlan ResolveLubanPlan(
        string projectRoot,
        LubanToolOptions? explicitTool,
        string lubanWorkDir)
    {
        string root = Path.GetFullPath(projectRoot);
        LubanToolOptions tool;
        if (explicitTool == null)
        {
            LubanToolDiscoveryResult discovery = new LubanProjectDiscoveryService().Discover(root, lubanWorkDir);
            if (!discovery.Succeeded || discovery.Options == null || discovery.Configuration == null)
            {
                throw new InvalidDataException(string.Join("; ", discovery.Diagnostics));
            }

            return CreateLubanPlan(root, discovery.Configuration, discovery.Options);
        }

        tool = NormalizeExplicitTool(root, explicitTool);
        LubanConfiguration configuration = new LubanConfigurationReader().Read(tool.LubanConfigPath);
        return CreateLubanPlan(root, configuration, tool);
    }

    /// <summary>规范化显式工具参数，并拒绝把 schema 与 Excel 写入当前项目之外。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="tool">调用方指定的 Luban 参数。</param>
    /// <returns>可传给中立 Luban 服务的绝对路径参数。</returns>
    private static LubanToolOptions NormalizeExplicitTool(string projectRoot, LubanToolOptions tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        LubanToolOptions normalizedTool = tool with { ProjectRoot = projectRoot };
        string configPath = ResolveContainedPath(
            projectRoot,
            LubanPathResolver.ResolveConfigPath(normalizedTool),
            "luban.conf");
        string workDirectory = string.IsNullOrWhiteSpace(tool.LubanWorkDir)
            ? Path.GetDirectoryName(configPath)!
            : ResolveContainedPath(
                projectRoot,
                LubanPathResolver.ResolveProjectPath(normalizedTool, tool.LubanWorkDir, "Luban 工作目录"),
                "Luban 工作目录");
        string executablePath = LubanPathResolver.ResolveProjectPath(normalizedTool, tool.LubanExecutablePath, "Luban 可执行文件");
        return normalizedTool with
        {
            LubanConfigPath = configPath,
            LubanWorkDir = workDirectory,
            LubanExecutablePath = executablePath
        };
    }

    /// <summary>根据 XML schema 目录和 dataDir 计算模板、预览与手动注册提示路径。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="configuration">已解析的 luban.conf 投影。</param>
    /// <param name="tool">可为空的 Luban 调用参数；仅模板计划允许为空。</param>
    /// <returns>LocalizationKit 自有文件与临时输出路径。</returns>
    private static LocalizationLubanPlan CreateLubanPlan(
        string projectRoot,
        LubanConfiguration configuration,
        LubanToolOptions? tool)
    {
        if (string.IsNullOrWhiteSpace(configuration.DataDirectory))
        {
            throw new InvalidDataException("LocalizationKit Luban 模板需要 luban.conf 配置 dataDir。");
        }

        string schemaDirectory = configuration.SchemaSources.FirstOrDefault(static source => source.IsDirectory)?.FullPath
            ?? Path.Combine(configuration.ConfigDirectory, "Defines");
        string schemaPath = ResolveContainedPath(projectRoot, Path.Combine(schemaDirectory, LUBAN_SCHEMA_FILE_NAME), "LocalizationKit XML schema");
        string workbookPath = ResolveContainedPath(
            projectRoot,
            Path.Combine(configuration.DataDirectory, LUBAN_WORKBOOK_DIRECTORY_NAME, LUBAN_WORKBOOK_FILE_NAME),
            "LocalizationKit Excel 模板");
        string workbookInputPath = Path.GetRelativePath(configuration.DataDirectory, workbookPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        bool registered = configuration.SchemaSources.Any(source => IsSchemaRegistered(source, schemaPath));
        string relativeSchemaPath = Path.GetRelativePath(configuration.ConfigDirectory, schemaPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        return new LocalizationLubanPlan(
            tool,
            configuration.ConfigDirectory,
            schemaPath,
            workbookPath,
            workbookInputPath,
            Path.Combine(projectRoot, "Temp", "LubanPreview", LUBAN_PREVIEW_DIRECTORY_NAME),
            registered,
            "{\"fileName\":\"" + relativeSchemaPath + "\",\"type\":\"\"}");
    }

    /// <summary>判断显式 XML 或 schemaFiles 目录是否已经覆盖模板 schema 路径。</summary>
    /// <param name="source">luban.conf 中的一条 schema 来源。</param>
    /// <param name="schemaPath">LocalizationKit 模板 XML 路径。</param>
    /// <returns>该来源会被 Luban 扫描时返回 true。</returns>
    private static bool IsSchemaRegistered(LubanSchemaSource source, string schemaPath)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!source.IsDirectory)
        {
            return string.Equals(source.FullPath, schemaPath, comparison);
        }

        string directory = source.FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return schemaPath.StartsWith(directory, comparison);
    }

    /// <summary>规范化模板语言名称，并拒绝 Runtime 不支持的语言或重复列。</summary>
    /// <param name="configuredLanguages">调用方指定的语言列。</param>
    /// <returns>按调用方顺序去重后的 LanguageId 列表。</returns>
    private static IReadOnlyList<LanguageId> NormalizeTemplateLanguages(IReadOnlyList<string>? configuredLanguages)
    {
        List<LanguageId> languages = new();
        HashSet<LanguageId> seen = new();
        foreach (string language in configuredLanguages ?? Array.Empty<string>())
        {
            if (!LocalizationSchema.TryParseLanguageId(language?.Trim() ?? string.Empty, out LanguageId languageId))
            {
                throw new InvalidDataException("模板语言无效: " + language);
            }

            if (seen.Add(languageId))
            {
                languages.Add(languageId);
            }
        }

        if (languages.Count == 0)
        {
            throw new InvalidDataException("至少需要一个有效的模板语言。");
        }

        return languages;
    }

    /// <summary>创建统一失败结果，调用方无需捕获预期环境或 schema 错误。</summary>
    /// <param name="diagnostic">面向用户的失败原因。</param>
    /// <returns>没有目录数据的失败结果。</returns>
    private static LocalizationOperationResult Failed(string diagnostic) => new()
    {
        Succeeded = false,
        Provider = "Luban",
        Diagnostics = new[] { diagnostic }
    };

    /// <summary>保存单次 Luban 模板、schema 注册和预览目录的内部路径计划。</summary>
    /// <param name="tool">真正执行预览时使用的 Luban 参数；模板规划阶段可以为空。</param>
    /// <param name="workDirectory">已解析 luban.conf 所在工作目录。</param>
    /// <param name="schemaPath">LocalizationKit XML schema 路径。</param>
    /// <param name="workbookPath">LocalizationKit Excel 作者文件路径。</param>
    /// <param name="workbookInputPath">相对于 Luban dataDir 的 Excel input 路径。</param>
    /// <param name="previewDirectory">独占临时 JSON 输出目录。</param>
    /// <param name="isSchemaRegistered">schemaFiles 是否会加载该 XML。</param>
    /// <param name="registrationHint">未注册时需要加入 schemaFiles 的 JSON 片段。</param>
    private sealed record LocalizationLubanPlan(
        LubanToolOptions? Tool,
        string WorkDirectory,
        string SchemaPath,
        string WorkbookPath,
        string WorkbookInputPath,
        string PreviewDirectory,
        bool IsSchemaRegistered,
        string RegistrationHint);
}
