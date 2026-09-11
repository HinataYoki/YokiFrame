using System.Text.Json.Nodes;
using YokiFrame.Client;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Models.LocalizationKit;
using YokiFrame.Tooling.Application.Models.Luban;
using YokiFrame.Tooling.Application.Services.LocalizationKit;

namespace YokiFrame.Cli;

/// <summary>提供 LocalizationKit JSON standalone 与 Luban 模板、预览 CLI。</summary>
internal static class CliLocalizationKitCommands
{
    /// <summary>判断命令是否属于 LocalizationKit CLI。</summary>
    internal static bool IsLocalizationCommand(CliCommandLine commandLine)
    {
        return commandLine.IsCommand("localization", "search")
            || commandLine.IsCommand("localization", "check")
            || commandLine.IsCommand("localization", "add")
            || commandLine.IsCommand("localization", "template", "generate")
            || commandLine.IsCommand("localization", "preview");
    }

    /// <summary>执行 LocalizationKit 用例并输出 compact JSON。</summary>
    internal static async Task<int> DispatchAsync(CliCommandLine commandLine, IYokiFrameClient client, CancellationToken cancellationToken)
    {
        LocalizationKitApplicationService service = new();
        string projectRoot = client.Paths.ProjectRoot;
        bool dryRun = CliStatusProjection.IsDryRun(commandLine);
        if (commandLine.IsCommand("localization", "template", "generate")) return GenerateLubanTemplate(commandLine, projectRoot, service, dryRun);
        if (commandLine.IsCommand("localization", "preview")) return await PreviewLubanAsync(commandLine, projectRoot, service, cancellationToken).ConfigureAwait(false);
        LocalizationKitOptions options = new() { ProjectRoot = projectRoot, SourcePath = commandLine.GetOption("source", "Assets/Settings/YokiFrame/localization.json") };
        bool isAdd = commandLine.IsCommand("localization", "add");
        if (dryRun)
        {
            return isAdd
                ? WriteAddDryRun(commandLine, projectRoot, service, options)
                : throw new YokiFrameProtocolException(new YokiFrameError(
                    "UnsupportedDryRun",
                    "--dry-run is only supported for localization add and localization template generate.",
                    "Remove --dry-run from this read-only command.",
                    Array.Empty<string>()));
        }

        LocalizationOperationResult result = isAdd
            ? service.Add(CreateAddRequest(commandLine, options))
            : commandLine.IsCommand("localization", "check")
                ? service.Check(options)
                : service.Search(new LocalizationSearchRequest { Options = options, Keyword = commandLine.GetOption("keyword", string.Empty), MissingOnly = commandLine.GetBoolOption("missing-only", false), Limit = commandLine.GetIntOption("limit", 200) });
        if (!result.Succeeded)
        {
            throw new YokiFrameProtocolException(new YokiFrameError("LocalizationKitFailed", string.Join("; ", result.Diagnostics), "Check --source, project root, and JSON schema, then retry.", new[] { projectRoot }));
        }
        JsonObject payload = new()
        {
            ["command"] = string.Join(" ", commandLine.Verbs),
            ["projectRoot"] = projectRoot,
            ["source"] = result.Catalog?.SourcePath ?? string.Empty,
            ["languageCount"] = result.Catalog?.Languages.Count ?? 0,
            ["entryCount"] = result.Catalog?.Entries.Count ?? 0,
            ["missingEntryCount"] = result.Catalog?.MissingEntryCount ?? 0,
            ["entries"] = CliJsonOutput.ToJsonNode(result.Entries.ToArray()),
            ["files"] = CliJsonOutput.ToJsonNode(result.Files.ToArray())
        };
        return CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>创建文本补充请求并校验必需参数。</summary>
    private static LocalizationAddRequest CreateAddRequest(CliCommandLine commandLine, LocalizationKitOptions options)
    {
        string idText = commandLine.GetOption("text-id", string.Empty);
        if (!int.TryParse(idText, out int textId)) throw new YokiFrameProtocolException(new YokiFrameError("InvalidOptionValue", "--text-id must be an integer.", "Use --text-id 1001.", Array.Empty<string>()));
        string language = commandLine.GetOption("language", string.Empty);
        string value = commandLine.GetOption("value", string.Empty);
        if (string.IsNullOrWhiteSpace(language) || string.IsNullOrWhiteSpace(value)) throw new YokiFrameProtocolException(new YokiFrameError("MissingOption", "localization add requires --language and --value.", "Use --language English --value \"text\".", Array.Empty<string>()));
        return new LocalizationAddRequest { Options = options, TextId = textId, Language = language, Value = value, PluralCategory = commandLine.GetOption("plural", string.Empty), Force = commandLine.GetBoolOption("force", false) };
    }

    /// <summary>在不写入 JSON 源文件的前提下规划一条文本补充，复用 Add 的全部校验。</summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="projectRoot">当前项目根。</param>
    /// <param name="service">LocalizationKit 应用服务。</param>
    /// <param name="options">源文件选项。</param>
    /// <returns>CLI 退出码。</returns>
    private static int WriteAddDryRun(
        CliCommandLine commandLine,
        string projectRoot,
        LocalizationKitApplicationService service,
        LocalizationKitOptions options)
    {
        LocalizationOperationResult result = service.PlanAdd(CreateAddRequest(commandLine, options));
        if (!result.Succeeded)
        {
            JsonObject failed = CliStatusProjection.CreateDryRunEnvelope(
                "localization add",
                projectRoot,
                Array.Empty<(string, bool)>(),
                "localization add");
            return CliStatusProjection.WriteDryRunFailure(
                failed,
                "LocalizationAddRejected",
                string.Join("; ", result.Diagnostics),
                "localization add");
        }

        JsonObject payload = CliStatusProjection.CreateDryRunEnvelope(
            "localization add",
            projectRoot,
            result.PlannedWrites.Select(static write => (write.Path, write.OverwritesExistingValue)),
            "localization add");
        // 提示真实执行时是否需要 --force，避免 AI 先跑一次失败命令才发现冲突。
        payload["requiresForce"] = result.PlannedWrites.Any(static write => write.OverwritesExistingValue);
        return CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>生成由 XML schema 注册的 Luban 本地化 Excel 模板。</summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="projectRoot">当前项目根。</param>
    /// <param name="service">LocalizationKit 应用服务。</param>
    /// <param name="dryRun">是否只规划不写入。</param>
    /// <returns>CLI 退出码。</returns>
    private static int GenerateLubanTemplate(
        CliCommandLine commandLine,
        string projectRoot,
        LocalizationKitApplicationService service,
        bool dryRun)
    {
        string languageText = commandLine.GetOption("languages", "ChineseSimplified,English");
        LocalizationLubanTemplateRequest request = new()
        {
            ProjectRoot = projectRoot,
            Tool = CreateExplicitLubanTool(commandLine, projectRoot),
            Languages = languageText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Force = commandLine.GetBoolOption("force", false)
        };
        if (dryRun)
        {
            LocalizationOperationResult plan = service.PlanLubanTemplate(request);
            if (!plan.Succeeded)
            {
                JsonObject failed = CliStatusProjection.CreateDryRunEnvelope(
                    "localization template generate",
                    projectRoot,
                    Array.Empty<(string, bool)>(),
                    "localization template generate");
                return CliStatusProjection.WriteDryRunFailure(
                    failed,
                    "LocalizationLubanTemplateRejected",
                    string.Join("; ", plan.Diagnostics),
                    "localization template generate");
            }

            JsonObject planned = CliStatusProjection.CreateDryRunEnvelope(
                "localization template generate",
                projectRoot,
                plan.PlannedWrites.Select(static write => (write.Path, write.OverwritesExistingValue)),
                "localization template generate");
            planned["languages"] = CliJsonOutput.ToJsonNode(request.Languages.ToArray());
            planned["diagnostics"] = CliJsonOutput.ToJsonNode(plan.Diagnostics.ToArray());
            return CliJsonOutput.WriteSuccess(planned);
        }

        LocalizationOperationResult result = service.GenerateLubanTemplate(request);
        if (!result.Succeeded) throw new YokiFrameProtocolException(new YokiFrameError("LocalizationLubanTemplateFailed", string.Join("; ", result.Diagnostics), "Check Luban path, schemaFiles, and --force override option.", new[] { projectRoot }));
        JsonObject payload = new()
        {
            ["command"] = "localization template generate",
            ["projectRoot"] = projectRoot,
            ["files"] = CliJsonOutput.ToJsonNode(result.Files.ToArray()),
            ["languages"] = CliJsonOutput.ToJsonNode(request.Languages.ToArray()),
            ["diagnostics"] = CliJsonOutput.ToJsonNode(result.Diagnostics.ToArray())
        };
        return CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>通过 Luban 临时 JSON 输出读取当前 LocalizationKit Excel 目录。</summary>
    private static async Task<int> PreviewLubanAsync(CliCommandLine commandLine, string projectRoot, LocalizationKitApplicationService service, CancellationToken cancellationToken)
    {
        LocalizationOperationResult result = await service.PreviewLubanAsync(new LocalizationLubanPreviewRequest
        {
            ProjectRoot = projectRoot,
            Tool = CreateExplicitLubanTool(commandLine, projectRoot)
        }, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new YokiFrameProtocolException(new YokiFrameError("LocalizationLubanPreviewFailed", string.Join("; ", result.Diagnostics), "Confirm the XML is registered in schemaFiles, and check the Luban tool and target.", new[] { projectRoot }));
        }

        JsonObject payload = new()
        {
            ["command"] = "localization preview",
            ["projectRoot"] = projectRoot,
            ["source"] = result.Catalog?.SourcePath ?? string.Empty,
            ["previewDirectory"] = result.PreviewDirectory,
            ["languageCount"] = result.Catalog?.Languages.Count ?? 0,
            ["entryCount"] = result.Catalog?.Entries.Count ?? 0,
            ["missingEntryCount"] = result.Catalog?.MissingEntryCount ?? 0,
            ["entries"] = CliJsonOutput.ToJsonNode(result.Entries.ToArray()),
            ["diagnostics"] = CliJsonOutput.ToJsonNode(result.Diagnostics.ToArray())
        };
        return CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>解析可选的 Luban 覆盖参数；未提供任何参数时交由项目发现服务选择唯一工具。</summary>
    private static LubanToolOptions? CreateExplicitLubanTool(CliCommandLine commandLine, string projectRoot)
    {
        string configPath = commandLine.GetOption("luban-config", string.Empty);
        string executablePath = commandLine.GetOption("luban", string.Empty);
        string workDirectory = commandLine.GetOption("luban-workdir", string.Empty);
        string targetName = commandLine.GetOption("target", "client");
        bool hasOverride = !string.IsNullOrWhiteSpace(configPath)
            || !string.IsNullOrWhiteSpace(executablePath)
            || !string.IsNullOrWhiteSpace(workDirectory)
            || commandLine.HasOption("target");
        if (!hasOverride)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(configPath) || string.IsNullOrWhiteSpace(executablePath))
        {
            throw new YokiFrameProtocolException(new YokiFrameError("MissingOption", "Explicit Luban options require both --luban-config and --luban.", "Use --luban-config Luban/luban.conf --luban Luban/Tools/Luban/Luban.dll.", Array.Empty<string>()));
        }

        return new LubanToolOptions
        {
            ProjectRoot = projectRoot,
            LubanConfigPath = configPath,
            LubanExecutablePath = executablePath,
            LubanWorkDir = workDirectory,
            TargetName = targetName
        };
    }
}
