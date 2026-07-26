using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using YokiFrame.Tooling.Application.Models.Luban;

namespace YokiFrame.Tooling.Application.Services.Luban;

/// <summary>集中执行 Luban 外部进程，并统一处理取消、退出码和日志收集。</summary>
public sealed class LubanCommandRunner
{
    /// <summary>执行一组已经结构化的 Luban 参数。</summary>
    /// <param name="options">工具、配置和工作目录参数。</param>
    /// <param name="arguments">不含 Luban 可执行文件本身的参数列表。</param>
    /// <param name="cancellationToken">请求取消时终止整个 Luban 进程树。</param>
    /// <returns>退出码和标准输出、错误输出的合并结果。</returns>
    public async Task<LubanCommandResult> RunAsync(
        LubanToolOptions options,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(arguments);
        if (string.IsNullOrWhiteSpace(options.LubanExecutablePath))
        {
            return new LubanCommandResult { Succeeded = false, Log = "未配置 Luban 可执行文件路径。" };
        }

        string executablePath = LubanPathResolver.ResolveProjectPath(options, options.LubanExecutablePath, "Luban 可执行文件");
        if (!File.Exists(executablePath))
        {
            return new LubanCommandResult { Succeeded = false, Log = "找不到 Luban 可执行文件: " + executablePath };
        }

        string configPath = ResolveConfigPath(options);
        if (!File.Exists(configPath))
        {
            return new LubanCommandResult { Succeeded = false, Log = "找不到 luban.conf: " + configPath };
        }

        string workDirectory = LubanPathResolver.ResolveWorkDirectory(options, configPath);
        if (!Directory.Exists(workDirectory))
        {
            return new LubanCommandResult { Succeeded = false, Log = "找不到 Luban 工作目录: " + workDirectory };
        }

        ProcessStartInfo startInfo = CreateStartInfo(executablePath, workDirectory, arguments);
        using Process process = new() { StartInfo = startInfo, EnableRaisingEvents = true };
        try
        {
            if (!process.Start())
            {
                return new LubanCommandResult { Succeeded = false, Log = "Luban 进程未能启动。" };
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            return new LubanCommandResult { Succeeded = false, Log = "无法启动 Luban: " + exception.Message };
        }

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            string output = await standardOutput.ConfigureAwait(false);
            string error = await standardError.ConfigureAwait(false);
            return new LubanCommandResult
            {
                Succeeded = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                Log = new StringBuilder(output).AppendLine(error).ToString()
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await StopProcessAsync(process).ConfigureAwait(false);
            try { await standardOutput.ConfigureAwait(false); } catch { }
            try { await standardError.ConfigureAwait(false); } catch { }
            throw;
        }
    }

    /// <summary>解析 luban.conf 路径，并在未显式提供时拒绝启动不确定的外部进程。</summary>
    /// <param name="options">待执行的工具参数。</param>
    /// <returns>规范化后的 luban.conf 绝对路径。</returns>
    private static string ResolveConfigPath(LubanToolOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.LubanConfigPath))
        {
            return string.Empty;
        }

        return LubanPathResolver.ResolveConfigPath(options);
    }

    /// <summary>构造不经过 shell 的进程启动信息，避免路径和参数被重新解释。</summary>
    /// <param name="executablePath">已验证存在的 Luban 文件。</param>
    /// <param name="workDirectory">已验证存在的进程工作目录。</param>
    /// <param name="arguments">业务参数。</param>
    /// <returns>可直接启动的进程描述。</returns>
    private static ProcessStartInfo CreateStartInfo(
        string executablePath,
        string workDirectory,
        IReadOnlyList<string> arguments)
    {
        bool isDotnetAssembly = executablePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        ProcessStartInfo startInfo = new()
        {
            FileName = isDotnetAssembly ? "dotnet" : executablePath,
            WorkingDirectory = workDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        if (isDotnetAssembly)
        {
            startInfo.ArgumentList.Add(executablePath);
        }

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    /// <summary>终止被取消的 Luban 进程树，避免后台任务继续占用临时输出目录。</summary>
    /// <param name="process">已经成功启动的 Luban 进程。</param>
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
