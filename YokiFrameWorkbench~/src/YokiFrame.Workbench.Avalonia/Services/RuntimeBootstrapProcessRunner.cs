using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// 通过源码包 Packaging 项目执行可取消 Runtime bootstrap，供 Workbench 更新和 Godot Installer 恢复共用。
/// </summary>
internal static class RuntimeBootstrapProcessRunner
{
    /// <summary>
    /// 创建不经 shell 解释的 Packaging bootstrap 命令。
    /// </summary>
    /// <param name="sourcePackageRoot">只读 YokiFrame 源码包根。</param>
    /// <param name="targetProjectRoot">目标 Unity 或 Godot 项目根。</param>
    /// <param name="packagingProjectPath">Packaging 项目完整路径。</param>
    /// <param name="openInstaller">成功后是否由 Packaging 打开新 Installer。</param>
    /// <returns>可直接启动的 dotnet 进程配置。</returns>
    internal static ProcessStartInfo CreateStartInfo(
        string sourcePackageRoot,
        string targetProjectRoot,
        string packagingProjectPath,
        bool openInstaller)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = sourcePackageRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in CreateArguments(
                     sourcePackageRoot,
                     targetProjectRoot,
                     packagingProjectPath,
                     openInstaller))
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    /// <summary>
    /// 启动 bootstrap 子进程并等待完成；取消时终止整个构建进程树。
    /// </summary>
    /// <param name="startInfo">已构造的 dotnet 进程配置。</param>
    /// <param name="failureTitle">构建失败时的业务语义标题。</param>
    /// <param name="cancellationToken">窗口或当前操作生命周期令牌。</param>
    /// <returns>进程以成功退出码结束时完成。</returns>
    internal static async Task RunAsync(
        ProcessStartInfo startInfo,
        string failureTitle,
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
    /// 生成 Packaging bootstrap 的稳定参数序列。
    /// </summary>
    private static IEnumerable<string> CreateArguments(
        string sourcePackageRoot,
        string targetProjectRoot,
        string packagingProjectPath,
        bool openInstaller)
    {
        yield return "run";
        yield return "--project";
        yield return packagingProjectPath;
        yield return "--";
        yield return "runtime";
        yield return "bootstrap";
        yield return "--package-root";
        yield return sourcePackageRoot;
        yield return "--project-root";
        yield return targetProjectRoot;
        yield return "--configuration";
        yield return "Release";
        if (openInstaller)
        {
            yield return "--open-installer";
        }
    }

    /// <summary>
    /// 将 dotnet 标准输出与错误输出组合为可显示失败说明。
    /// </summary>
    private static string CreateFailureMessage(string failureTitle, string output, string error)
    {
        var details = new StringBuilder(error).Append(output).ToString().Trim();
        return string.IsNullOrWhiteSpace(details)
            ? failureTitle + "，dotnet 未返回额外诊断。"
            : failureTitle + ":" + Environment.NewLine + details;
    }

    /// <summary>
    /// 终止尚未退出的 dotnet 构建进程树，并等待句柄进入终态。
    /// </summary>
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
