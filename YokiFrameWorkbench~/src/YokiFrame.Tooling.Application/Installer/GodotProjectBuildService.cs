using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 定义 Godot 安装提交后构建目标 C# 主项目的进程边界，便于 UI 编排和测试替换。
/// </summary>
internal interface IGodotProjectBuildService
{
    /// <summary>
    /// 在目标项目需要时执行 restore/build，并确认 Godot Editor 将加载的程序集已经生成。
    /// </summary>
    /// <param name="plan">已经完成 Core 提交的 Godot 安装计划。</param>
    /// <param name="cancellationToken">取消当前构建时使用的令牌。</param>
    /// <returns>构建完成任务。</returns>
    Task BuildIfRequiredAsync(
        GodotInstallPlan plan,
        CancellationToken cancellationToken);
}

/// <summary>
/// 使用当前 .NET SDK 构建 Godot 主项目，避免插件扫描发生在主程序集生成之前。
/// </summary>
internal sealed class GodotProjectBuildService : IGodotProjectBuildService
{
    private const string GODOT_MONO_DIRECTORY = ".godot";
    private const string GODOT_MONO_TEMP_DIRECTORY = "mono";
    private const string GODOT_BUILD_TEMP_DIRECTORY = "temp";
    private const string GODOT_DEBUG_OUTPUT_DIRECTORY = "Debug";

    /// <summary>
    /// 判断当前安装是否需要主动构建目标主项目。
    /// </summary>
    /// <param name="plan">Godot 安装计划。</param>
    /// <returns>目标已有 Godot .NET 工作区或刚生成主项目时返回 true。</returns>
    internal static bool NeedsBuild(GodotInstallPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.ProjectFileWasGenerated)
        {
            return true;
        }

        var monoRoot = Path.Combine(plan.ProjectRoot, GODOT_MONO_DIRECTORY, GODOT_MONO_TEMP_DIRECTORY);
        return Directory.Exists(monoRoot);
    }

    /// <summary>
    /// 按需顺序执行 Editor 目标的 restore 和 build，并验证 Godot Editor 的 Debug 主程序集输出。
    /// </summary>
    /// <param name="plan">已经完成 Core 提交的 Godot 安装计划。</param>
    /// <param name="cancellationToken">取消当前构建时使用的令牌。</param>
    /// <returns>构建完成任务。</returns>
    public async Task BuildIfRequiredAsync(
        GodotInstallPlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!NeedsBuild(plan))
        {
            return;
        }

        var projectPath = RequireProjectFile(plan.ProjectFilePath);
        await RunDotnetAsync(
            CreateRestoreStartInfo(projectPath, plan.ProjectRoot),
            "Godot 主项目 restore 失败",
            cancellationToken).ConfigureAwait(false);
        await RunDotnetAsync(
            CreateBuildStartInfo(projectPath, plan.ProjectRoot),
            "Godot 主项目 build 失败",
            cancellationToken).ConfigureAwait(false);

        var assemblyCandidates = GetAssemblyOutputCandidates(plan);
        if (!assemblyCandidates.Any(File.Exists))
        {
            throw new InvalidOperationException(
                "Godot 主项目构建完成，但未生成 Editor 程序集。已检查: "
                + string.Join(", ", assemblyCandidates));
        }
    }

    /// <summary>
    /// 创建不经过 shell 的 restore 进程配置，保留项目路径参数边界。
    /// </summary>
    /// <param name="projectPath">主 Godot csproj 完整路径。</param>
    /// <param name="workingDirectory">目标 Godot 项目根目录。</param>
    /// <returns>可直接启动的 restore 进程配置。</returns>
    internal static ProcessStartInfo CreateRestoreStartInfo(
        string projectPath,
        string workingDirectory)
    {
        return CreateStartInfo(
            workingDirectory,
            "restore",
            projectPath,
            "-p:GodotTarget=Editor",
            "--verbosity",
            "minimal");
    }

    /// <summary>
    /// 创建不经过 shell 的 build 进程配置，禁止重复 restore 并保留默认并行项目图。
    /// </summary>
    /// <param name="projectPath">主 Godot csproj 完整路径。</param>
    /// <param name="workingDirectory">目标 Godot 项目根目录。</param>
    /// <returns>可直接启动的 build 进程配置。</returns>
    internal static ProcessStartInfo CreateBuildStartInfo(
        string projectPath,
        string workingDirectory)
    {
        return CreateStartInfo(
            workingDirectory,
            "build",
            projectPath,
            "--no-restore",
            "--no-incremental",
            "-p:GodotTarget=Editor",
            "--verbosity",
            "minimal");
    }

    /// <summary>
    /// 解析主项目程序集名称并计算 Godot Editor Debug 输出路径。
    /// </summary>
    /// <param name="plan">Godot 安装计划。</param>
    /// <returns>预期主程序集完整路径。</returns>
    internal static string GetAssemblyOutputPath(GodotInstallPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return GetAssemblyOutputPath(
            plan.ProjectRoot,
            plan.ProjectFilePath,
            plan.ProjectSettingsPath);
    }

    /// <summary>
    /// 根据 Godot 项目根和主项目文件计算 Editor Debug 程序集路径。
    /// </summary>
    /// <param name="projectRoot">Godot 项目根目录。</param>
    /// <param name="projectFilePath">主 Godot csproj 完整路径。</param>
    /// <param name="projectSettingsPath">project.godot 完整路径。</param>
    /// <returns>Godot 4.7 .NET Editor 生成的主程序集完整路径。</returns>
    internal static string GetAssemblyOutputPath(
        string projectRoot,
        string projectFilePath,
        string projectSettingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectSettingsPath);
        var assemblyName = ReadAssemblyName(projectFilePath, projectSettingsPath);
        return Path.Combine(
            projectRoot,
            GODOT_MONO_DIRECTORY,
            GODOT_MONO_TEMP_DIRECTORY,
            GODOT_BUILD_TEMP_DIRECTORY,
            "bin",
            GODOT_DEBUG_OUTPUT_DIRECTORY,
            assemblyName + ".dll");
    }

    /// <summary>
    /// 获取 Godot SDK 与普通 dotnet 构建可能使用的全部主程序集输出位置。
    /// </summary>
    /// <param name="plan">Godot 安装计划。</param>
    /// <returns>按 Godot 默认优先级排列且已去重的候选路径。</returns>
    internal static IReadOnlyList<string> GetAssemblyOutputCandidates(GodotInstallPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var assemblyName = ReadAssemblyName(
            plan.ProjectFilePath,
            plan.ProjectSettingsPath);
        var godotOutputRoot = Path.Combine(
            plan.ProjectRoot,
            GODOT_MONO_DIRECTORY,
            GODOT_MONO_TEMP_DIRECTORY,
            "bin",
            GODOT_DEBUG_OUTPUT_DIRECTORY);
        var godotTempOutputRoot = Path.Combine(
            plan.ProjectRoot,
            GODOT_MONO_DIRECTORY,
            GODOT_MONO_TEMP_DIRECTORY,
            GODOT_BUILD_TEMP_DIRECTORY,
            "bin",
            GODOT_DEBUG_OUTPUT_DIRECTORY);
        var candidates = new[]
        {
            Path.Combine(godotTempOutputRoot, assemblyName + ".dll"),
            Path.Combine(godotTempOutputRoot, "net8.0", assemblyName + ".dll"),
            Path.Combine(godotOutputRoot, assemblyName + ".dll"),
            Path.Combine(godotOutputRoot, "net8.0", assemblyName + ".dll"),
            Path.Combine(plan.ProjectRoot, "bin", GODOT_DEBUG_OUTPUT_DIRECTORY, "net8.0", assemblyName + ".dll"),
            Path.Combine(plan.ProjectRoot, "bin", GODOT_DEBUG_OUTPUT_DIRECTORY, assemblyName + ".dll")
        };
        return candidates
            .Distinct(PathComparer)
            .ToArray();
    }

    /// <summary>
    /// 根据当前平台选择文件路径去重比较器，避免 Windows 大小写路径重复检查。
    /// </summary>
    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>
    /// 创建 dotnet 子进程配置并集中设置无 shell、无窗口和重定向输出约束。
    /// </summary>
    /// <param name="workingDirectory">子进程工作目录。</param>
    /// <param name="command">dotnet 子命令。</param>
    /// <param name="projectPath">主项目路径。</param>
    /// <param name="tailArguments">子命令附加参数。</param>
    /// <returns>可直接启动的 dotnet 进程配置。</returns>
    private static ProcessStartInfo CreateStartInfo(
        string workingDirectory,
        string command,
        string projectPath,
        params string[] tailArguments)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(command);
        startInfo.ArgumentList.Add(projectPath);
        foreach (var argument in tailArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    /// <summary>
    /// 启动 dotnet 子进程、收集诊断并在取消时终止其进程树。
    /// </summary>
    /// <param name="startInfo">已构造的 dotnet 进程配置。</param>
    /// <param name="failureTitle">失败时展示的业务标题。</param>
    /// <param name="cancellationToken">取消当前构建时使用的令牌。</param>
    /// <returns>子进程成功退出时完成。</returns>
    private static async Task RunDotnetAsync(
        ProcessStartInfo startInfo,
        string failureTitle,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using Process process = new() { StartInfo = startInfo, EnableRaisingEvents = true };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("无法启动 dotnet 进程。");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            throw new InvalidOperationException(failureTitle + ": " + exception.Message, exception);
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(CreateFailureMessage(failureTitle, output, error));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await StopProcessAsync(process).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// 从 MSBuild 项目和 project.godot 读取稳定程序集名，无法解析时回退文件名。
    /// </summary>
    /// <param name="projectPath">主 Godot csproj 完整路径。</param>
    /// <returns>用于 Debug 输出文件名的程序集名。</returns>
    private static string ReadAssemblyName(string projectPath, string projectSettingsPath)
    {
        try
        {
            var document = XDocument.Load(projectPath, LoadOptions.PreserveWhitespace);
            var assemblyName = document
                .Descendants()
                .FirstOrDefault(static element => element.Name.LocalName == "AssemblyName")
                ?.Value
                .Trim();
            if (!string.IsNullOrWhiteSpace(assemblyName)
                && !assemblyName.Contains("$(", StringComparison.Ordinal))
            {
                return assemblyName;
            }
        }
        catch (XmlException)
        {
            // Core 已在安装计划阶段验证 XML；这里保留文件名回退以避免诊断路径再次遮蔽原错误。
        }

        var godotAssemblyName = ReadGodotAssemblyName(projectSettingsPath);
        if (!string.IsNullOrWhiteSpace(godotAssemblyName)
            && string.Equals(Path.GetFileName(godotAssemblyName), godotAssemblyName, StringComparison.Ordinal))
        {
            return godotAssemblyName;
        }

        return Path.GetFileNameWithoutExtension(projectPath);
    }

    /// <summary>
    /// 读取 project.godot [dotnet] section 的 assembly_name，匹配 Godot 的主程序集输出命名。
    /// </summary>
    /// <param name="projectSettingsPath">project.godot 完整路径。</param>
    /// <returns>未转义程序集名；未声明时返回 null。</returns>
    private static string? ReadGodotAssemblyName(string projectSettingsPath)
    {
        var inDotNetSection = false;
        foreach (var rawLine in File.ReadLines(projectSettingsPath))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("[", StringComparison.Ordinal)
                && line.EndsWith("]", StringComparison.Ordinal))
            {
                inDotNetSection = string.Equals(line, "[dotnet]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inDotNetSection)
            {
                continue;
            }

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0
                || !string.Equals(
                    line[..equalsIndex].Trim(),
                    "project/assembly_name",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = line[(equalsIndex + 1)..].Trim();
            return value.Length >= 2 && value[0] == '"' && value[^1] == '"'
                ? value[1..^1]
                    .Replace("\\\"", "\"", StringComparison.Ordinal)
                    .Replace("\\\\", "\\", StringComparison.Ordinal)
                : value;
        }

        return null;
    }

    /// <summary>
    /// 校验主项目文件仍存在，并返回规范化完整路径。
    /// </summary>
    /// <param name="projectPath">安装计划记录的主项目路径。</param>
    /// <returns>规范化主项目路径。</returns>
    private static string RequireProjectFile(string projectPath)
    {
        var fullPath = Path.GetFullPath(projectPath);
        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException("Godot 主项目文件不存在。", fullPath);
    }

    /// <summary>
    /// 组合标准输出和错误输出，确保编译错误不会退化为“插件脚本可能存在代码错误”。
    /// </summary>
    /// <param name="failureTitle">失败标题。</param>
    /// <param name="output">标准输出。</param>
    /// <param name="error">标准错误。</param>
    /// <returns>面向 Installer 的完整失败说明。</returns>
    private static string CreateFailureMessage(string failureTitle, string output, string error)
    {
        var details = new StringBuilder(error).Append(output).ToString().Trim();
        return string.IsNullOrWhiteSpace(details)
            ? failureTitle + "，dotnet 未返回额外诊断。"
            : failureTitle + ":" + System.Environment.NewLine + details;
    }

    /// <summary>
    /// 终止仍在运行的 dotnet 进程树，并等待进程句柄进入终态。
    /// </summary>
    /// <param name="process">待终止的 dotnet 进程。</param>
    /// <returns>进程退出后完成。</returns>
    private static async Task StopProcessAsync(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            return;
        }

        await process.WaitForExitAsync().ConfigureAwait(false);
    }
}
