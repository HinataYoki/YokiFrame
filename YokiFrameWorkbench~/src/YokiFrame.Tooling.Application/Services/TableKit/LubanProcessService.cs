using System.Text;
using YokiFrame.Tooling.Application.Models.Luban;
using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Tooling.Application.Services.Luban;

namespace YokiFrame.Tooling.Application.Services.TableKit;

/// <summary>编排 TableKit 的代码生成命令，并复用中立 Luban JSON 预览能力。</summary>
public sealed class LubanProcessService
{
    private const string LUBAN_CODE_DIRECTORY_NAME = "Luban";
    private readonly LubanCommandRunner mCommandRunner;
    private readonly LubanJsonPreviewService mJsonPreviewService;

    /// <summary>创建使用默认中立 Luban 服务的 TableKit 进程编排器。</summary>
    public LubanProcessService() : this(new LubanCommandRunner(), new LubanJsonPreviewService())
    {
    }

    /// <summary>创建复用指定中立 Luban 服务的 TableKit 进程编排器。</summary>
    /// <param name="commandRunner">负责主代码和数据生成的进程执行器。</param>
    /// <param name="jsonPreviewService">负责临时 JSON 预览的共享服务。</param>
    public LubanProcessService(LubanCommandRunner commandRunner, LubanJsonPreviewService jsonPreviewService)
    {
        mCommandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
        mJsonPreviewService = jsonPreviewService ?? throw new ArgumentNullException(nameof(jsonPreviewService));
    }

    /// <summary>执行 Luban 的代码和数据生成命令。</summary>
    /// <param name="options">生成选项。</param>
    /// <param name="contract">已解析的目标契约。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>进程退出码及合并输出。</returns>
    public async Task<(int ExitCode, string Log)> GenerateAsync(TableKitOptions options, TableKitContract contract, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(contract);
        Directory.CreateDirectory(GetLubanCodeOutputDirectory(contract));
        IReadOnlyList<string> arguments = BuildMainArguments(options, contract);
        (int exitCode, string log) = await RunAsync(options, arguments, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0 || options.ExtraOutputTargets.Count == 0)
        {
            return (exitCode, log);
        }

        StringBuilder aggregateLog = new(log);
        foreach (TableKitExtraOutput extraOutput in options.ExtraOutputTargets)
        {
            IReadOnlyList<string> extraArguments = BuildExtraArguments(options, contract, extraOutput);
            if (extraArguments.Count == 4)
            {
                continue;
            }

            (int extraExitCode, string extraLog) = await RunAsync(options, extraArguments, cancellationToken).ConfigureAwait(false);
            aggregateLog.AppendLine(extraLog);
            if (extraExitCode != 0)
            {
                return (extraExitCode, aggregateLog.ToString());
            }
        }

        return (exitCode, aggregateLog.ToString());
    }

    /// <summary>构建主 target 参数，并把 Luban 可清空代码限制在 TableKit 根目录的 Luban 子目录。</summary>
    /// <param name="options">页面生成选项。</param>
    /// <param name="contract">主 target 解析契约。</param>
    /// <returns>可直接传给 Luban 进程的参数列表。</returns>
    internal static IReadOnlyList<string> BuildMainArguments(
        TableKitOptions options,
        TableKitContract contract)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(contract);
        string lubanCodeDirectory = GetLubanCodeOutputDirectory(contract);
        return new[]
        {
            "-t", contract.TargetName,
            "--conf", GetLubanConfigPath(options),
            "-c", options.CodeTarget,
            "-d", options.DataTarget,
            "-x", options.DataTarget + ".outputDataDir=" + contract.OutputDataDirectory,
            "-x", options.CodeTarget + ".outputCodeDir=" + lubanCodeDirectory
        };
    }

    /// <summary>取得允许 Luban 清理和重建的主代码输出目录。</summary>
    /// <param name="contract">主 target 解析契约。</param>
    /// <returns>TableKit 根目录下的 Luban 专用子目录。</returns>
    internal static string GetLubanCodeOutputDirectory(TableKitContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return Path.Combine(contract.OutputCodeDirectory, LUBAN_CODE_DIRECTORY_NAME);
    }

    /// <summary>构建单个额外输出目标的 Luban 参数，代码与数据目录保持彼此独立。</summary>
    /// <param name="options">页面生成选项。</param>
    /// <param name="contract">主 target 解析契约，用作旧设置的 target 回退。</param>
    /// <param name="extraOutput">额外输出目标。</param>
    /// <returns>可直接传给 Luban 进程的参数列表。</returns>
    internal static IReadOnlyList<string> BuildExtraArguments(
        TableKitOptions options,
        TableKitContract contract,
        TableKitExtraOutput extraOutput)
    {
        List<string> arguments = new()
        {
            "-t", string.IsNullOrWhiteSpace(extraOutput.TargetName) ? contract.TargetName : extraOutput.TargetName,
            "--conf", GetLubanConfigPath(options)
        };
        if (!string.IsNullOrWhiteSpace(extraOutput.DataTarget) && !string.IsNullOrWhiteSpace(extraOutput.OutputDataDir))
        {
            arguments.AddRange(new[]
            {
                "-d", extraOutput.DataTarget,
                "-x", extraOutput.DataTarget + ".outputDataDir=" + ResolveProjectPath(options.ProjectRoot, extraOutput.OutputDataDir)
            });
        }

        if (!string.IsNullOrWhiteSpace(extraOutput.CodeTarget) && !string.IsNullOrWhiteSpace(extraOutput.OutputCodeDir))
        {
            arguments.AddRange(new[]
            {
                "-c", extraOutput.CodeTarget,
                "-x", extraOutput.CodeTarget + ".outputCodeDir=" + ResolveProjectPath(options.ProjectRoot, extraOutput.OutputCodeDir)
            });
        }

        return arguments;
    }

    /// <summary>执行 Luban 的临时 JSON 验证，并把共享预览模型投影为 TableKit 页面模型。</summary>
    /// <param name="options">Workbench 生成选项。</param>
    /// <param name="contract">已解析的目标契约。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>退出码、日志、预览表和 TableKit 独占临时目录。</returns>
    public async Task<(int ExitCode, string Log, IReadOnlyList<TableKitPreviewTable> PreviewTables, string PreviewDirectory)> ValidateAsync(
        TableKitOptions options,
        TableKitContract contract,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(contract);
        string previewDirectory = Path.Combine(Path.GetFullPath(options.ProjectRoot), "Temp", "LubanValidate");
        LubanJsonPreviewResult preview = await mJsonPreviewService.GenerateAsync(
            CreateToolOptions(options, contract.TargetName), previewDirectory, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<TableKitPreviewTable> tables = preview.Tables
            .Select(static table => new TableKitPreviewTable
            {
                Name = table.Name,
                Count = table.Count,
                PreviewJson = table.PreviewJson
            })
            .ToArray();
        return (preview.ExitCode, preview.Log, tables, preview.PreviewDirectory);
    }

    /// <summary>通过中立执行器运行 TableKit 已构建的命令参数。</summary>
    /// <param name="options">TableKit 中保存的 Luban 参数。</param>
    /// <param name="arguments">已完成 TableKit 输出约束的命令参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>保留 TableKit 原有形状的退出码和日志。</returns>
    private async Task<(int ExitCode, string Log)> RunAsync(
        TableKitOptions options,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        LubanCommandResult result = await mCommandRunner.RunAsync(
            CreateToolOptions(options, options.TargetName), arguments, cancellationToken).ConfigureAwait(false);
        return (result.ExitCode, result.Log);
    }

    /// <summary>把 TableKit 的现有配置投影为共享 Luban 服务参数，不暴露 TableKit 领域模型给其它 Kit。</summary>
    /// <param name="options">TableKit 持久化配置。</param>
    /// <param name="targetName">当前调用使用的目标名称。</param>
    /// <returns>中立 Luban 调用参数。</returns>
    private static LubanToolOptions CreateToolOptions(TableKitOptions options, string targetName) => new()
    {
        ProjectRoot = options.ProjectRoot,
        LubanConfigPath = options.LubanConfigPath,
        LubanWorkDir = options.LubanWorkDir,
        LubanExecutablePath = options.LubanExecutablePath,
        TargetName = targetName
    };

    /// <summary>取得传给 Luban 的绝对配置路径，使自定义工作目录不会丢失 luban.conf。</summary>
    /// <param name="options">包含配置路径的页面选项。</param>
    /// <returns>规范化绝对配置路径。</returns>
    private static string GetLubanConfigPath(TableKitOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.LubanConfigPath))
        {
            throw new InvalidDataException("TableKit 未配置 luban.conf 路径。");
        }

        return Path.GetFullPath(options.LubanConfigPath);
    }

    /// <summary>解析额外输出目录并拒绝越出项目根的路径。</summary>
    /// <param name="projectRoot">项目根目录。</param>
    /// <param name="path">绝对或项目相对路径。</param>
    /// <returns>规范化绝对路径。</returns>
    private static string ResolveProjectPath(string projectRoot, string path)
    {
        string root = Path.GetFullPath(projectRoot);
        string full = Path.GetFullPath(Path.IsPathFullyQualified(path) ? path : Path.Combine(root, path));
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!full.StartsWith(prefix, comparison))
        {
            throw new InvalidDataException("TableKit 额外输出路径越出项目根。");
        }

        return full;
    }
}
