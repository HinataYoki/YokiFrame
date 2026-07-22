using System.Diagnostics;

namespace YokiFrame.Packaging.Services;

/// <summary>
/// 读取独立 YokiFrame Git 仓库的 index，供发布验证以即将进入 Git URL 的内容为准判断。
/// </summary>
internal sealed class GitIndexReleaseReader
{
    /// <summary>
    /// 确认输入目录本身就是 Git 工作树根，避免从父仓库或嵌套子目录读取错误 index。
    /// </summary>
    /// <param name="packageRoot">预期的 YokiFrame 独立包根。</param>
    internal void EnsureRepositoryRoot(string packageRoot)
    {
        var fullPackageRoot = NormalizeFullPath(packageRoot);
        var result = RunGit(fullPackageRoot, "rev-parse", "--show-toplevel");
        if (result.ExitCode != 0)
        {
            throw CreateGitFailure("Unable to resolve YokiFrame Git repository root.", result);
        }

        var repositoryRoot = NormalizeFullPath(result.StandardOutput.Trim());
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(fullPackageRoot, repositoryRoot, comparison))
        {
            throw new InvalidDataException(
                "Release verification requires the YokiFrame package root to be the Git repository root.");
        }
    }

    /// <summary>
    /// 返回当前 index 中全部已跟踪的相对路径，统一为 Git 使用的正斜杠形式。
    /// </summary>
    /// <param name="packageRoot">YokiFrame Git 根。</param>
    /// <returns>不包含空项的 index 路径列表。</returns>
    internal IReadOnlyList<string> ReadTrackedPaths(string packageRoot)
    {
        var result = RunGit(packageRoot, "ls-files", "--stage", "-z");
        if (result.ExitCode != 0)
        {
            throw CreateGitFailure("Unable to read YokiFrame Git index.", result);
        }

        return ParseTrackedPaths(result.StandardOutput);
    }

    /// <summary>
    /// 返回未被 ignore 规则排除的未跟踪文件，防止源码只存在于工作树却没有进入发布 index。
    /// </summary>
    /// <param name="packageRoot">YokiFrame Git 根。</param>
    /// <returns>使用正斜杠的未跟踪文件路径。</returns>
    internal IReadOnlyList<string> ReadUntrackedPaths(string packageRoot)
    {
        var result = RunGit(packageRoot, "ls-files", "--others", "--exclude-standard", "-z");
        if (result.ExitCode != 0)
        {
            throw CreateGitFailure("Unable to read untracked YokiFrame release files.", result);
        }

        return ParseGitPaths(result.StandardOutput);
    }

    /// <summary>
    /// 确认整个发布工作树没有未暂存修改，使物理预检与 Git index 校验针对同一份内容。
    /// </summary>
    /// <param name="packageRoot">YokiFrame Git 根。</param>
    internal void EnsureWorkingTreeMatchesIndex(string packageRoot)
    {
        var result = RunGit(packageRoot, "diff", "--quiet");
        if (result.ExitCode == 0)
        {
            return;
        }

        if (result.ExitCode == 1)
        {
            throw new InvalidDataException(
                "Git index and working tree differ; stage or discard the change before release verification.");
        }

        throw CreateGitFailure("Unable to compare the YokiFrame Git index and working tree.", result);
    }

    /// <summary>
    /// 解析以 NUL 分隔的 Git 路径输出，并统一为正斜杠形式。
    /// </summary>
    /// <param name="output">Git `-z` 输出。</param>
    /// <returns>不包含空项的路径列表。</returns>
    private static IReadOnlyList<string> ParseGitPaths(string output)
    {
        return output
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => path.Replace('\\', '/'))
            .ToArray();
    }

    /// <summary>
    /// 解析 `git ls-files --stage -z`，并拒绝符号链接与 gitlink 等非普通文件进入源码发布索引。
    /// </summary>
    /// <param name="output">包含 mode、对象 ID、stage 和路径的 NUL 分隔输出。</param>
    /// <returns>统一为正斜杠的普通已跟踪路径。</returns>
    private static IReadOnlyList<string> ParseTrackedPaths(string output)
    {
        List<string> paths = new();
        foreach (var entry in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tabIndex = entry.IndexOf('\t');
            var modeSeparatorIndex = entry.IndexOf(' ');
            if (tabIndex <= 0 || modeSeparatorIndex <= 0 || modeSeparatorIndex >= tabIndex)
            {
                throw new InvalidDataException("Git index contains an invalid staged entry.");
            }

            var mode = entry[..modeSeparatorIndex];
            var path = entry[(tabIndex + 1)..].Replace('\\', '/');
            if (string.Equals(mode, "120000", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Tracked symbolic links are not allowed in the source release: " + path);
            }

            if (string.Equals(mode, "160000", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Tracked gitlinks or submodules are not allowed in the source release: " + path);
            }

            paths.Add(path);
        }

        return paths;
    }

    /// <summary>
    /// 启动 git 并完整读取两个输出流，避免较长 index 错误信息造成子进程管道阻塞。
    /// </summary>
    /// <param name="workingDirectory">Git 工作树根。</param>
    /// <param name="arguments">不含 git 可执行名的参数。</param>
    /// <returns>进程退出码和完整输出。</returns>
    private static GitCommandResult RunGit(string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start git for release verification.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(standardOutputTask, standardErrorTask);
        return new GitCommandResult(process.ExitCode, standardOutputTask.Result, standardErrorTask.Result);
    }

    /// <summary>
    /// 规范化一个目录绝对路径并移除非根目录末尾分隔符，便于跨平台严格比较。
    /// </summary>
    /// <param name="path">待规范化路径。</param>
    /// <returns>规范化完整路径。</returns>
    private static string NormalizeFullPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// 将 git 子进程失败转换为包含 stderr 的稳定发布诊断。
    /// </summary>
    /// <param name="message">调用方语义错误说明。</param>
    /// <param name="result">失败 Git 命令结果。</param>
    /// <returns>可直接抛出的异常。</returns>
    private static InvalidDataException CreateGitFailure(string message, GitCommandResult result)
    {
        var diagnostic = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();
        return new InvalidDataException(
            string.IsNullOrWhiteSpace(diagnostic) ? message : message + " " + diagnostic);
    }

    /// <summary>
    /// 封装单次 Git 进程的退出码和两个文本输出流。
    /// </summary>
    /// <param name="ExitCode">进程退出码。</param>
    /// <param name="StandardOutput">完整标准输出。</param>
    /// <param name="StandardError">完整标准错误。</param>
    private sealed record GitCommandResult(int ExitCode, string StandardOutput, string StandardError);
}
