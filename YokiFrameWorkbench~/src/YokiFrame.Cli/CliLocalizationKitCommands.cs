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
    internal static int Dispatch(CliCommandLine commandLine, IYokiFrameClient client)
    {
        LocalizationKitApplicationService service = new();
        string projectRoot = client.Paths.ProjectRoot;
        if (commandLine.IsCommand("localization", "template", "generate")) return GenerateLubanTemplate(commandLine, projectRoot, service);
        if (commandLine.IsCommand("localization", "preview")) return PreviewLuban(commandLine, projectRoot, service);
        LocalizationKitOptions options = new() { ProjectRoot = projectRoot, SourcePath = commandLine.GetOption("source", "Assets/Settings/YokiFrame/localization.json") };
        LocalizationOperationResult result = commandLine.IsCommand("localization", "add")
            ? service.Add(CreateAddRequest(commandLine, options))
            : commandLine.IsCommand("localization", "check")
                ? service.Check(options)
                : service.Search(new LocalizationSearchRequest { Options = options, Keyword = commandLine.GetOption("keyword", string.Empty), MissingOnly = commandLine.GetBoolOption("missing-only", false), Limit = commandLine.GetIntOption("limit", 200) });
        if (!result.Succeeded)
        {
            throw new YokiFrameProtocolException(new YokiFrameError("LocalizationKitFailed", string.Join("; ", result.Diagnostics), "检查 --source、项目根路径和 JSON schema 后重试。", new[] { projectRoot }));
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
        if (!int.TryParse(idText, out int textId)) throw new YokiFrameProtocolException(new YokiFrameError("InvalidOptionValue", "--text-id 必须是整数。", "使用 --text-id 1001。", Array.Empty<string>()));
        string language = commandLine.GetOption("language", string.Empty);
        string value = commandLine.GetOption("value", string.Empty);
        if (string.IsNullOrWhiteSpace(language) || string.IsNullOrWhiteSpace(value)) throw new YokiFrameProtocolException(new YokiFrameError("MissingOption", "localization add 需要 --language 和 --value。", "使用 --language English --value \"文本\"。", Array.Empty<string>()));
        return new LocalizationAddRequest { Options = options, TextId = textId, Language = language, Value = value, PluralCategory = commandLine.GetOption("plural", string.Empty), Force = commandLine.GetBoolOption("force", false) };
    }

    /// <summary>生成由 XML schema 注册的 Luban 本地化 Excel 模板。</summary>
    private static int GenerateLubanTemplate(CliCommandLine commandLine, string projectRoot, LocalizationKitApplicationService service)
    {
        string languageText = commandLine.GetOption("languages", "ChineseSimplified,English");
        LocalizationLubanTemplateRequest request = new()
        {
            ProjectRoot = projectRoot,
            Tool = CreateExplicitLubanTool(commandLine, projectRoot),
            Languages = languageText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Force = commandLine.GetBoolOption("force", false)
        };
        LocalizationOperationResult result = service.GenerateLubanTemplate(request);
        if (!result.Succeeded) throw new YokiFrameProtocolException(new YokiFrameError("LocalizationLubanTemplateFailed", string.Join("; ", result.Diagnostics), "检查 Luban 路径、schemaFiles 和 --force 覆盖选项。", new[] { projectRoot }));
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
    private static int PreviewLuban(CliCommandLine commandLine, string projectRoot, LocalizationKitApplicationService service)
    {
        LocalizationOperationResult result = service.PreviewLubanAsync(new LocalizationLubanPreviewRequest
        {
            ProjectRoot = projectRoot,
            Tool = CreateExplicitLubanTool(commandLine, projectRoot)
        }).GetAwaiter().GetResult();
        if (!result.Succeeded)
        {
            throw new YokiFrameProtocolException(new YokiFrameError("LocalizationLubanPreviewFailed", string.Join("; ", result.Diagnostics), "确认 XML 已注册到 schemaFiles，并检查 Luban 工具和 target。", new[] { projectRoot }));
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
            throw new YokiFrameProtocolException(new YokiFrameError("MissingOption", "显式 Luban 参数需要同时提供 --luban-config 和 --luban。", "使用 --luban-config Luban/luban.conf --luban Luban/Tools/Luban/Luban.dll。", Array.Empty<string>()));
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
