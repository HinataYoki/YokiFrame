using System.Diagnostics;

namespace YokiFrame.Cli.Tests;

/// <summary>CLI 集成测试公用辅助方法。</summary>
internal static class CliTestHelpers
{
    /// <summary>根据测试输出目录定位同一 solution 构建出的 CLI 程序。</summary>
    /// <returns>CLI 程序 DLL 路径。</returns>
    internal static string GetCliAssemblyPath()
    {
        var outputDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var configurationDirectory = outputDirectory.Parent
            ?? throw new DirectoryNotFoundException("CLI test output configuration directory is missing.");
        var projectDirectory = configurationDirectory.Parent
            ?? throw new DirectoryNotFoundException("CLI test output project directory is missing.");
        var binDirectory = projectDirectory.Parent
            ?? throw new DirectoryNotFoundException("CLI test output bin directory is missing.");
        var cliAssemblyPath = Path.Combine(
            binDirectory.FullName,
            "YokiFrame.Cli",
            configurationDirectory.Name,
            outputDirectory.Name,
            "YokiFrame.Cli.dll");

        Xunit.Assert.True(File.Exists(cliAssemblyPath), "CLI assembly was not built: " + cliAssemblyPath);
        return cliAssemblyPath;
    }

    /// <summary>启动真实 CLI 程序并捕获标准输出和标准错误。</summary>
    /// <param name="arguments">传递给 CLI 的参数列表。</param>
    /// <returns>进程退出结果。</returns>
    internal static async Task<CliProcessResult> RunCliAsync(params string[] arguments)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(GetCliAssemblyPath());
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start YokiFrame.Cli process.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new CliProcessResult(process.ExitCode, await standardOutputTask, await standardErrorTask);
    }
}

/// <summary>CLI 子进程执行结果。</summary>
/// <param name="ExitCode">进程退出码。</param>
/// <param name="StandardOutput">标准输出文本。</param>
/// <param name="StandardError">标准错误文本。</param>
internal sealed record CliProcessResult(int ExitCode, string StandardOutput, string StandardError);
