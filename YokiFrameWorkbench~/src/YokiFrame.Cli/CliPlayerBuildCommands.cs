using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Cli;

/// <summary>
/// 为脚本和 AI 提供 Godot Player headless 导出入口，并保留稳定 JSON 与日志证据。
/// </summary>
internal static class CliPlayerBuildCommands
{
    private const string GODOT_ENGINE = "godot";
    private const string DEBUG_CONFIGURATION = "debug";
    private const string RELEASE_CONFIGURATION = "release";
    private static readonly HashSet<string> sAllowedOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "configuration",
        "engine",
        "godot",
        "output",
        "preset",
        "project"
    };

    /// <summary>判断命令是否属于 Player 构建入口，供 Program 在创建 FileBridge client 前分流。</summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <returns>首个动词为 player 时返回 true。</returns>
    public static bool IsPlayerBuildCommand(CliCommandLine commandLine)
    {
        return commandLine.Verbs.Count > 0
            && string.Equals(commandLine.Verbs[0], "player", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>验证 Godot 导出参数、执行 headless export，并输出可机器解析的构建结果。</summary>
    /// <param name="commandLine">已解析 Player 命令。</param>
    /// <param name="projectRoot">规范化目标项目根。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>进程退出码。</returns>
    public static async Task<int> DispatchAsync(
        CliCommandLine commandLine,
        string projectRoot,
        CancellationToken cancellationToken)
    {
        if (!commandLine.IsCommand("player", "build"))
        {
            throw CreateInputException(
                "UnknownPlayerCommand",
                "Unsupported player command.",
                "Use player build --engine godot.");
        }

        ValidateOptionSchema(commandLine);
        var options = CreateOptions(commandLine, projectRoot);
        var result = await RunGodotExportAsync(options, cancellationToken).ConfigureAwait(false);
        return WriteResult(result);
    }

    /// <summary>只允许稳定公开参数，避免拼写错误被静默忽略。</summary>
    /// <param name="commandLine">已解析命令行。</param>
    private static void ValidateOptionSchema(CliCommandLine commandLine)
    {
        foreach (var optionName in commandLine.OptionNames)
        {
            if (!sAllowedOptions.Contains(optionName))
            {
                throw CreateInputException(
                    "UnknownPlayerBuildOption",
                    "Unsupported player build option: --" + optionName + ".",
                    "Use --project, --engine, --godot, --preset, --output or --configuration.");
            }
        }
    }

    /// <summary>把 CLI 文本参数转换为经过路径和项目文件校验的 Godot 导出选项。</summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="projectRoot">目标项目根。</param>
    /// <returns>不可变构建选项。</returns>
    private static GodotPlayerBuildOptions CreateOptions(CliCommandLine commandLine, string projectRoot)
    {
        var engine = RequireOption(commandLine, "engine");
        if (!string.Equals(engine, GODOT_ENGINE, StringComparison.OrdinalIgnoreCase))
        {
            throw CreateInputException(
                "UnsupportedPlayerEngine",
                "Player build currently supports the Godot engine only.",
                "Use --engine godot; the YokiFrame CLI does not currently build Unity Players. "
                + "Use the Unity Editor or an external automation tool.");
        }

        var normalizedProjectRoot = Path.GetFullPath(projectRoot);
        ValidateProjectFile(normalizedProjectRoot, "project.godot", "GodotProjectMissing");
        ValidateProjectFile(normalizedProjectRoot, "export_presets.cfg", "GodotExportPresetFileMissing");
        var configuration = ReadConfiguration(commandLine);
        var outputPath = ResolveOutputPath(normalizedProjectRoot, RequireOption(commandLine, "output"));
        var logPath = CreateLogPath(normalizedProjectRoot, outputPath);
        return new GodotPlayerBuildOptions(
            normalizedProjectRoot,
            RequireOption(commandLine, "godot"),
            RequireOption(commandLine, "preset"),
            outputPath,
            logPath,
            configuration);
    }

    /// <summary>校验 Godot 项目必须包含的入口文件，并把缺失转成稳定错误。</summary>
    /// <param name="projectRoot">项目根。</param>
    /// <param name="fileName">必需文件名。</param>
    /// <param name="errorCode">缺失时错误码。</param>
    private static void ValidateProjectFile(string projectRoot, string fileName, string errorCode)
    {
        var path = Path.Combine(projectRoot, fileName);
        if (!File.Exists(path))
        {
            throw new YokiFrameProtocolException(new YokiFrameError(
                errorCode,
                "Godot project file is missing: " + fileName + ".",
                "Create or restore " + fileName + " before building the Player.",
                new[] { path }));
        }
    }

    /// <summary>解析 debug/release 配置，拒绝可能被误解释的任意文本。</summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <returns>规范化小写配置。</returns>
    private static string ReadConfiguration(CliCommandLine commandLine)
    {
        var configuration = commandLine.GetOption("configuration", DEBUG_CONFIGURATION).ToLowerInvariant();
        if (configuration is DEBUG_CONFIGURATION or RELEASE_CONFIGURATION)
        {
            return configuration;
        }

        throw CreateInputException(
            "InvalidPlayerConfiguration",
            "Player build configuration must be debug or release.",
            "Use --configuration debug or --configuration release.");
    }

    /// <summary>解析导出产物并限制在项目根内，避免 CLI 覆盖项目外文件。</summary>
    /// <param name="projectRoot">规范化项目根。</param>
    /// <param name="requestedPath">绝对或项目相对输出路径。</param>
    /// <returns>项目内完整输出路径。</returns>
    private static string ResolveOutputPath(string projectRoot, string requestedPath)
    {
        var outputPath = Path.GetFullPath(
            Path.IsPathRooted(requestedPath) ? requestedPath : Path.Combine(projectRoot, requestedPath));
        var relativePath = Path.GetRelativePath(projectRoot, outputPath);
        if (relativePath == "."
            || relativePath == ".."
            || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath))
        {
            throw CreateInputException(
                "PlayerOutputOutsideProject",
                "Player output must stay inside the target project.",
                "Use a project-relative path such as Builds/Game.exe.");
        }

        return outputPath;
    }

    /// <summary>为本次导出创建位于 `.yokiframe/builds/godot/logs` 的稳定日志路径。</summary>
    /// <param name="projectRoot">目标项目根。</param>
    /// <param name="outputPath">导出产物路径。</param>
    /// <returns>完整日志路径。</returns>
    private static string CreateLogPath(string projectRoot, string outputPath)
    {
        var outputName = Path.GetFileNameWithoutExtension(outputPath);
        var safeName = string.Create(outputName.Length, outputName, static (span, name) =>
        {
            for (var i = 0; i < name.Length; i++)
                span[i] = char.IsLetterOrDigit(name[i]) || name[i] is '-' or '_' ? name[i] : '_';
        });
        return Path.Combine(
            projectRoot,
            ".yokiframe",
            "builds",
            "godot",
            "logs",
            DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "-" + safeName + ".log");
    }

    /// <summary>运行 Godot headless export，等待终态并校验导出产物真实存在。</summary>
    /// <param name="options">已验证构建选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>构建结果。</returns>
    private static async Task<GodotPlayerBuildResult> RunGodotExportAsync(
        GodotPlayerBuildOptions options,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(options.OutputPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(options.LogPath)!);
        var startInfo = CreateStartInfo(options);
        var stopwatch = Stopwatch.StartNew();
        using var process = StartGodotProcess(startInfo, options);
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var standardErrorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (Exception) { }
            throw;
        }
        finally
        {
            var standardOutput = await standardOutputTask.ConfigureAwait(false);
            var standardError = await standardErrorTask.ConfigureAwait(false);
            stopwatch.Stop();
            AppendProcessOutput(options.LogPath, standardOutput, standardError);
        }

        ValidateGodotResult(process.ExitCode, options);
        return new GodotPlayerBuildResult(options, new FileInfo(options.OutputPath).Length, stopwatch.ElapsedMilliseconds);
    }

    /// <summary>构造不经 shell 拼接的 Godot 参数，避免路径和 preset 文本被二次解释。</summary>
    /// <param name="options">已验证构建选项。</param>
    /// <returns>可直接启动的进程配置。</returns>
    private static ProcessStartInfo CreateStartInfo(GodotPlayerBuildOptions options)
    {
        ProcessStartInfo startInfo = new(options.GodotExecutable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--path");
        startInfo.ArgumentList.Add(options.ProjectRoot);
        startInfo.ArgumentList.Add("--log-file");
        startInfo.ArgumentList.Add(options.LogPath);
        startInfo.ArgumentList.Add(options.Configuration == DEBUG_CONFIGURATION ? "--export-debug" : "--export-release");
        startInfo.ArgumentList.Add(options.Preset);
        startInfo.ArgumentList.Add(options.OutputPath);
        return startInfo;
    }

    /// <summary>启动 Godot；可执行文件不存在或不可执行时返回稳定诊断。</summary>
    /// <param name="startInfo">进程配置。</param>
    /// <param name="options">证据路径来源。</param>
    /// <returns>已启动进程。</returns>
    private static Process StartGodotProcess(ProcessStartInfo startInfo, GodotPlayerBuildOptions options)
    {
        try
        {
            return Process.Start(startInfo)
                ?? throw new InvalidOperationException("Godot process did not start.");
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            try { File.WriteAllText(options.LogPath, exception.ToString()); } catch (Exception) { }
            throw new YokiFrameProtocolException(new YokiFrameError(
                "GodotExecutableNotFound",
                "Godot executable could not be started: " + exception.Message,
                "Pass --godot with the Godot 4.7 .NET executable path.",
                new[] { options.GodotExecutable, options.LogPath }));
        }
    }

    /// <summary>把进程输出追加到 Godot 自身日志，保留 stdout/stderr 证据且不污染 CLI JSON。</summary>
    /// <param name="logPath">Godot 日志路径。</param>
    /// <param name="standardOutput">标准输出。</param>
    /// <param name="standardError">标准错误。</param>
    private static void AppendProcessOutput(string logPath, string standardOutput, string standardError)
    {
        StringBuilder builder = new();
        if (!string.IsNullOrWhiteSpace(standardOutput)) builder.AppendLine().AppendLine("[stdout]").Append(standardOutput);
        if (!string.IsNullOrWhiteSpace(standardError)) builder.AppendLine().AppendLine("[stderr]").Append(standardError);
        if (builder.Length > 0) File.AppendAllText(logPath, builder.ToString());
    }

    /// <summary>校验 Godot 退出码和主产物；失败时返回日志与目标路径证据。</summary>
    /// <param name="exitCode">Godot 进程退出码。</param>
    /// <param name="options">构建选项。</param>
    private static void ValidateGodotResult(int exitCode, GodotPlayerBuildOptions options)
    {
        if (exitCode != 0)
        {
            throw new YokiFrameProtocolException(new YokiFrameError(
                "GodotExportFailed",
                "Godot Player export failed with exit code " + exitCode + ".",
                "Inspect the build log, install the matching export templates, and retry.",
                new[] { options.LogPath, options.OutputPath }));
        }

        if (!File.Exists(options.OutputPath))
        {
            throw new YokiFrameProtocolException(new YokiFrameError(
                "GodotExportArtifactMissing",
                "Godot reported success but the Player artifact is missing.",
                "Inspect the export preset and build log before retrying.",
                new[] { options.LogPath, options.OutputPath }));
        }
    }

    /// <summary>输出成功构建的稳定 compact JSON。</summary>
    /// <param name="result">Godot 构建结果。</param>
    /// <returns>成功退出码。</returns>
    private static int WriteResult(GodotPlayerBuildResult result)
    {
        JsonObject payload = new()
        {
            ["command"] = "player build",
            ["engine"] = "Godot",
            ["projectRoot"] = result.Options.ProjectRoot,
            ["configuration"] = result.Options.Configuration,
            ["preset"] = result.Options.Preset,
            ["outputPath"] = result.Options.OutputPath,
            ["logPath"] = result.Options.LogPath,
            ["godotExecutable"] = result.Options.GodotExecutable,
            ["artifactBytes"] = result.ArtifactBytes,
            ["durationMs"] = result.DurationMilliseconds
        };
        return CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>读取非空必填参数。</summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="name">参数名。</param>
    /// <returns>非空参数值。</returns>
    private static string RequireOption(CliCommandLine commandLine, string name)
    {
        var value = commandLine.GetOption(name, string.Empty);
        if (!string.IsNullOrWhiteSpace(value)) return value;
        throw CreateInputException(
            "MissingPlayerBuildOption",
            "Player build option --" + name + " is required.",
            "Provide --" + name + " with a valid value.");
    }

    /// <summary>创建由 Program 统一输出的标准输入错误。</summary>
    /// <param name="code">稳定错误码。</param>
    /// <param name="message">错误说明。</param>
    /// <param name="suggestion">修复建议。</param>
    /// <returns>协议异常。</returns>
    private static YokiFrameProtocolException CreateInputException(
        string code,
        string message,
        string suggestion)
    {
        return new YokiFrameProtocolException(new YokiFrameError(
            code,
            message,
            suggestion,
            Array.Empty<string>()));
    }

    /// <summary>保存一次经过验证的 Godot Player 构建输入。</summary>
    private sealed record GodotPlayerBuildOptions(
        string ProjectRoot,
        string GodotExecutable,
        string Preset,
        string OutputPath,
        string LogPath,
        string Configuration);

    /// <summary>保存 Godot Player 构建成功后的产物证据。</summary>
    private sealed record GodotPlayerBuildResult(
        GodotPlayerBuildOptions Options,
        long ArtifactBytes,
        long DurationMilliseconds);
}
