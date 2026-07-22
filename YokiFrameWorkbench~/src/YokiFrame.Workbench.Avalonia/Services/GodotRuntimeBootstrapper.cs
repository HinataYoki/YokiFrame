using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// 为 Installer 提供从选定源码包显式构建 Godot 项目 Runtime 并打开新 Installer 的平台进程边界。
/// </summary>
internal interface IGodotRuntimeBootstrapper
{
    /// <summary>
    /// 使用选定源码包构建目标项目当前平台缓存，并启动该缓存中的 Installer。
    /// </summary>
    /// <param name="sourcePackageRoot">只读 YokiFrame 源码包根。</param>
    /// <param name="targetProjectRoot">目标 Godot 项目根。</param>
    /// <param name="cancellationToken">用户取消当前构建时使用的令牌。</param>
    /// <returns>Runtime bootstrap 子进程完成任务。</returns>
    Task BootstrapAndOpenInstallerAsync(
        string sourcePackageRoot,
        string targetProjectRoot,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 通过源码包内 Packaging 项目执行受控 Runtime bootstrap，避免 Avalonia 重新实现缓存和启动规则。
/// </summary>
internal sealed class GodotRuntimeBootstrapper : IGodotRuntimeBootstrapper
{
    private const string WORKBENCH_DIRECTORY_NAME = "YokiFrameWorkbench~";
    private const string PACKAGING_PROJECT_RELATIVE_PATH =
        "src/YokiFrame.Packaging/YokiFrame.Packaging.csproj";

    /// <summary>
    /// 使用 `dotnet run` 调用源码包的 Packaging 权威入口，成功后由 Packaging 启动新 Installer。
    /// </summary>
    /// <param name="sourcePackageRoot">只读 YokiFrame 源码包根。</param>
    /// <param name="targetProjectRoot">目标 Godot 项目根。</param>
    /// <param name="cancellationToken">用户取消当前构建时使用的令牌。</param>
    /// <returns>Runtime bootstrap 子进程完成任务。</returns>
    public async Task BootstrapAndOpenInstallerAsync(
        string sourcePackageRoot,
        string targetProjectRoot,
        CancellationToken cancellationToken = default)
    {
        var fullSourcePackageRoot = RequireDirectory(sourcePackageRoot, "YokiFrame 源目录");
        var fullTargetProjectRoot = RequireDirectory(targetProjectRoot, "Godot 项目目录");
        var packagingProjectPath = Path.Combine(
            fullSourcePackageRoot,
            WORKBENCH_DIRECTORY_NAME,
            PACKAGING_PROJECT_RELATIVE_PATH.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(packagingProjectPath))
        {
            throw new FileNotFoundException("YokiFrame 源码包缺少 Runtime bootstrap 所需的 Packaging 项目。", packagingProjectPath);
        }

        var startInfo = CreateStartInfo(
            fullSourcePackageRoot,
            fullTargetProjectRoot,
            packagingProjectPath);
        await RunBootstrapProcessAsync(startInfo, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 创建不经 shell 解释的 Packaging 命令，确保带空格的包根和项目根不会改变参数边界。
    /// </summary>
    /// <param name="sourcePackageRoot">已规范化的 YokiFrame 源码包根。</param>
    /// <param name="targetProjectRoot">已规范化的 Godot 项目根。</param>
    /// <param name="packagingProjectPath">Packaging 项目完整路径。</param>
    /// <returns>可直接启动的 dotnet 进程配置。</returns>
    internal static ProcessStartInfo CreateStartInfo(
        string sourcePackageRoot,
        string targetProjectRoot,
        string packagingProjectPath)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = sourcePackageRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(packagingProjectPath);
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("runtime");
        startInfo.ArgumentList.Add("bootstrap");
        startInfo.ArgumentList.Add("--package-root");
        startInfo.ArgumentList.Add(sourcePackageRoot);
        startInfo.ArgumentList.Add("--project-root");
        startInfo.ArgumentList.Add(targetProjectRoot);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--open-installer");
        return startInfo;
    }

    /// <summary>
    /// 启动 bootstrap 子进程、等待完成并将失败输出收敛为可显示异常。
    /// </summary>
    /// <param name="startInfo">已构造的 dotnet 进程配置。</param>
    /// <param name="cancellationToken">取消时终止整个构建进程树。</param>
    /// <returns>进程成功完成任务。</returns>
    private static async Task RunBootstrapProcessAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        using Process process = new() { StartInfo = startInfo, EnableRaisingEvents = true };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("无法启动 .NET Runtime bootstrap 进程。");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            throw new InvalidOperationException("无法启动 .NET Runtime bootstrap 进程: " + exception.Message, exception);
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(CreateBootstrapFailureMessage(output, error));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await StopProcessAsync(process).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// 验证需要参与构建的目录存在，并统一返回绝对路径。
    /// </summary>
    /// <param name="path">用户选择的目录。</param>
    /// <param name="displayName">面向用户的目录名称。</param>
    /// <returns>已验证的完整目录路径。</returns>
    private static string RequireDirectory(string path, string displayName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(displayName + "不能为空。", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        return Directory.Exists(fullPath)
            ? fullPath
            : throw new DirectoryNotFoundException(displayName + "不存在: " + fullPath);
    }

    /// <summary>
    /// 将 dotnet 的标准输出和错误输出组合为 Installer 可展示的简体中文失败说明。
    /// </summary>
    /// <param name="output">dotnet 标准输出。</param>
    /// <param name="error">dotnet 标准错误。</param>
    /// <returns>保留关键构建证据的失败文本。</returns>
    private static string CreateBootstrapFailureMessage(string output, string error)
    {
        var details = new StringBuilder(error).Append(output).ToString().Trim();
        return string.IsNullOrWhiteSpace(details)
            ? "Godot Runtime 构建失败，dotnet 未返回额外诊断。"
            : "Godot Runtime 构建失败:" + Environment.NewLine + details;
    }

    /// <summary>
    /// 在用户取消时结束整个 dotnet 构建进程树，避免后台继续占用缓存 staging 目录。
    /// </summary>
    /// <param name="process">已启动的 bootstrap 进程。</param>
    /// <returns>进程确实退出后完成。</returns>
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
