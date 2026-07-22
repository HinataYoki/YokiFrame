using System.Diagnostics;
using YokiFrame.Packaging;

namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 覆盖 Git URL 源码发布包的预检与 Git index 门禁。
/// </summary>
public sealed class RuntimeReleaseCommandsTests
{
    private static readonly object sConsoleSync = new();

    /// <summary>
    /// 验证源码包只保留权威 bootstrap 模板时可以通过 release prepare，且不会生成包内 Runtime 文件。
    /// </summary>
    [Fact]
    public void ReleasePrepareAcceptsSourcePackageWithoutRuntimePayload()
    {
        using ReleasePackageFixture fixture = ReleasePackageFixture.Create();

        var result = ExecutePackaging("runtime", "release-prepare", "--package-root", fixture.PackageRoot);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("free of package-local Runtime payloads", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(fixture.RuntimeRoot));
        Assert.Equal(string.Empty, result.StandardError.Trim());
    }

    /// <summary>
    /// 验证 release prepare 会拒绝工作树中残留的 manifest、profile 或 staging，避免忽略规则掩盖发布错误。
    /// </summary>
    [Fact]
    public void ReleasePrepareRejectsPackageLocalRuntimePayload()
    {
        using ReleasePackageFixture fixture = ReleasePackageFixture.Create();
        fixture.WriteRuntimePayload("win-x64-aot/YokiFrame.Workbench.Avalonia.exe", "aot-gui");

        var result = ExecutePackaging("runtime", "release-prepare", "--package-root", fixture.PackageRoot);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("must not contain WorkbenchRuntime", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证 release verify 接受仅含源码、文档和权威 bootstrap 模板的真实 Git index。
    /// </summary>
    [Fact]
    public void ReleaseVerifyAcceptsTrackedSourceOnly()
    {
        using ReleasePackageFixture fixture = ReleasePackageFixture.Create();
        fixture.InitializeGitRepository();
        fixture.StageSourceOnly();

        var result = ExecutePackaging("runtime", "release-verify", "--package-root", fixture.PackageRoot);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("verified for Git URL", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, result.StandardError.Trim());
    }

    /// <summary>
    /// 验证空 Git index 不能被当成无非法产物的有效源码发布投影。
    /// </summary>
    [Fact]
    public void ReleaseVerifyRejectsEmptyGitIndex()
    {
        using ReleasePackageFixture fixture = ReleasePackageFixture.Create();
        fixture.InitializeGitRepository();

        var result = ExecutePackaging("runtime", "release-verify", "--package-root", fixture.PackageRoot);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Git index is empty", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证新增源码必须进入 index，避免工作树可编译但 Git URL 发布包静默遗漏文件。
    /// </summary>
    [Fact]
    public void ReleaseVerifyRejectsUntrackedSourceFile()
    {
        using ReleasePackageFixture fixture = ReleasePackageFixture.Create();
        fixture.InitializeGitRepository();
        fixture.StageSourceOnly();
        fixture.WriteWorkingTreeFile("Core/Runtime/UntrackedFeature.cs", "public sealed class UntrackedFeature { }");

        var result = ExecutePackaging("runtime", "release-verify", "--package-root", fixture.PackageRoot);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Untracked source-release path", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证已跟踪文件的未暂存修改会使物理预检与 index 内容不一致，因此必须拒绝发布。
    /// </summary>
    [Fact]
    public void ReleaseVerifyRejectsUnstagedWorkingTreeChange()
    {
        using ReleasePackageFixture fixture = ReleasePackageFixture.Create();
        fixture.InitializeGitRepository();
        fixture.StageSourceOnly();
        fixture.WriteWorkingTreeFile("package.json", "{\"name\":\"changed.in.working.tree\"}");

        var result = ExecutePackaging("runtime", "release-verify", "--package-root", fixture.PackageRoot);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("index and working tree differ", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证 bootstrap 模板仅存在于工作树但未进入 index 时不能通过发布门禁。
    /// </summary>
    [Fact]
    public void ReleaseVerifyRejectsRequiredTemplateMissingFromIndex()
    {
        using ReleasePackageFixture fixture = ReleasePackageFixture.Create();
        fixture.InitializeGitRepository();
        fixture.StageSourceOnly();
        fixture.RemoveFromIndex("YokiFrameWorkbench~/scripts/runtime-bootstrap/install-godot.command");

        var result = ExecutePackaging("runtime", "release-verify", "--package-root", fixture.PackageRoot);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Required source-release file", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证 release verify 会拒绝被误加入索引的普通编译产物，即使它不位于旧 Runtime 目录。
    /// </summary>
    [Fact]
    public void ReleaseVerifyRejectsTrackedCompiledArtifact()
    {
        using ReleasePackageFixture fixture = ReleasePackageFixture.Create();
        fixture.InitializeGitRepository();
        fixture.StageSourceOnly();
        fixture.WriteAndStage("Core/Runtime/YokiFrame.dll", "compiled");

        var result = ExecutePackaging("runtime", "release-verify", "--package-root", fixture.PackageRoot);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("not allowed", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证 release verify 会拒绝被误加入索引的项目级缓存内容。
    /// </summary>
    [Fact]
    public void ReleaseVerifyRejectsTrackedProjectRuntimeCache()
    {
        using ReleasePackageFixture fixture = ReleasePackageFixture.Create();
        fixture.InitializeGitRepository();
        fixture.StageSourceOnly();
        fixture.WriteAndStage(".yokiframe/runtime/current.json", "{}");

        var result = ExecutePackaging("runtime", "release-verify", "--package-root", fixture.PackageRoot);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("not allowed", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证源码发布门禁读取 Git index mode，并拒绝 mode 120000 的链接条目绕过物理目录扫描。
    /// </summary>
    [Fact]
    public void ReleaseVerifyRejectsTrackedSymbolicLink()
    {
        using ReleasePackageFixture fixture = ReleasePackageFixture.Create();
        fixture.InitializeGitRepository();
        fixture.StageSourceOnly();
        fixture.StageSymbolicLinkEntry("Core/Runtime/LinkedMarker.cs", "Core/Runtime/CoreMarker.cs");

        var result = ExecutePackaging("runtime", "release-verify", "--package-root", fixture.PackageRoot);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("symbolic links", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证 release verify 会拒绝 mode 160000 的 gitlink，避免子模块指针代替实际源码进入发布索引。
    /// </summary>
    [Fact]
    public void ReleaseVerifyRejectsTrackedGitLink()
    {
        using ReleasePackageFixture fixture = ReleasePackageFixture.Create();
        fixture.InitializeGitRepository();
        fixture.StageSourceOnly();
        fixture.StageGitLinkEntry("Core/ExternalDependency", "gitlink-fixture");

        var result = ExecutePackaging("runtime", "release-verify", "--package-root", fixture.PackageRoot);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("gitlinks or submodules", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 在独占 Console 重定向下执行 Packaging CLI，避免测试并行时互相污染标准输出。
    /// </summary>
    /// <param name="args">传给 Packaging CLI 的参数。</param>
    /// <returns>退出码、标准输出和标准错误。</returns>
    private static PackagingCommandResult ExecutePackaging(params string[] args)
    {
        lock (sConsoleSync)
        {
            var originalOutput = Console.Out;
            var originalError = Console.Error;
            using StringWriter output = new();
            using StringWriter error = new();
            try
            {
                Console.SetOut(output);
                Console.SetError(error);
                var exitCode = Program.Main(args);
                return new PackagingCommandResult(exitCode, output.ToString(), error.ToString());
            }
            finally
            {
                Console.SetOut(originalOutput);
                Console.SetError(originalError);
            }
        }
    }

    /// <summary>
    /// 封装一次 CLI 调用的稳定可断言结果。
    /// </summary>
    /// <param name="ExitCode">CLI 退出码。</param>
    /// <param name="StandardOutput">完整标准输出。</param>
    /// <param name="StandardError">完整标准错误。</param>
    private sealed record PackagingCommandResult(int ExitCode, string StandardOutput, string StandardError);

    /// <summary>
    /// 构造包含最小源码包和真实临时 Git index 的隔离 fixture。
    /// </summary>
    private sealed class ReleasePackageFixture : IDisposable
    {
        private readonly string mRoot;

        /// <summary>
        /// 创建包含包元数据、公开文档、三种通用 bootstrap 和三种 Godot 安装入口的源码包。
        /// </summary>
        private ReleasePackageFixture()
        {
            mRoot = Path.Combine(Path.GetTempPath(), "yokiframe-release-tests", Guid.NewGuid().ToString("N"));
            PackageRoot = mRoot;
            RuntimeRoot = Path.Combine(PackageRoot, "WorkbenchRuntime~");
            WriteFile(Path.Combine(PackageRoot, "package.json"), "{\"name\":\"com.hinatayoki.yokiframe\"}");
            WriteFile(Path.Combine(PackageRoot, "Documentation~", "Guides", "Tooling.md"), "# Tooling");
            WriteFile(Path.Combine(PackageRoot, "Core", "Runtime", "CoreMarker.cs"), "public sealed class CoreMarker { }");
            WriteBootstrapTemplate("build-current-platform.cmd", "cmd-template");
            WriteBootstrapTemplate("build-current-platform.sh", "sh-template");
            WriteBootstrapTemplate("build-current-platform.command", "command-template");
            WriteBootstrapTemplate("install-godot.cmd", "godot-cmd-template");
            WriteBootstrapTemplate("install-godot.sh", "godot-sh-template");
            WriteBootstrapTemplate("install-godot.command", "godot-command-template");
        }

        /// <summary>
        /// 获取测试包根。
        /// </summary>
        public string PackageRoot { get; }

        /// <summary>
        /// 获取不应出现在源码包内的旧 Runtime 根。
        /// </summary>
        public string RuntimeRoot { get; }

        /// <summary>
        /// 创建新的隔离 release fixture。
        /// </summary>
        /// <returns>包含未初始化 Git 索引的 fixture。</returns>
        public static ReleasePackageFixture Create()
        {
            return new ReleasePackageFixture();
        }

        /// <summary>
        /// 初始化临时 Git 仓库，使 release verify 可以读取真实 index 而不是模拟文件列表。
        /// </summary>
        public void InitializeGitRepository()
        {
            RunGit("init");
        }

        /// <summary>
        /// 暂存允许进入 Git URL 包的源码和模板，不包含任何运行产物。
        /// </summary>
        public void StageSourceOnly()
        {
            RunGit("add", "--", "package.json", "Documentation~", "Core", "YokiFrameWorkbench~/scripts/runtime-bootstrap");
        }

        /// <summary>
        /// 向源码包故意写入旧 Runtime 文件，供 release prepare 负向验证。
        /// </summary>
        /// <param name="relativePath">相对于 WorkbenchRuntime~ 的路径。</param>
        /// <param name="content">测试内容。</param>
        public void WriteRuntimePayload(string relativePath, string content)
        {
            WriteFile(Path.Combine(RuntimeRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)), content);
        }

        /// <summary>
        /// 写入并暂存一个相对于包根的错误发布文件。
        /// </summary>
        /// <param name="relativePath">包相对路径。</param>
        /// <param name="content">测试内容。</param>
        public void WriteAndStage(string relativePath, string content)
        {
            WriteFile(Path.Combine(PackageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)), content);
            RunGit("add", "--", relativePath);
        }

        /// <summary>
        /// 写入工作树文件但不修改 index，用于验证未跟踪文件和未暂存修改门禁。
        /// </summary>
        /// <param name="relativePath">包相对路径。</param>
        /// <param name="content">文件内容。</param>
        public void WriteWorkingTreeFile(string relativePath, string content)
        {
            WriteFile(Path.Combine(PackageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)), content);
        }

        /// <summary>
        /// 仅从 index 移除指定文件并保留工作树副本，模拟必需文件遗漏于发布投影。
        /// </summary>
        /// <param name="relativePath">包相对路径。</param>
        public void RemoveFromIndex(string relativePath)
        {
            RunGit("rm", "--cached", "--", relativePath);
        }

        /// <summary>
        /// 通过 Git plumbing 写入 mode 120000 条目，不依赖当前操作系统创建符号链接的权限。
        /// </summary>
        /// <param name="relativePath">待写入 index 的链接路径。</param>
        /// <param name="target">符号链接 blob 中保存的相对目标。</param>
        public void StageSymbolicLinkEntry(string relativePath, string target)
        {
            var blobSourcePath = Path.Combine(PackageRoot, ".git-link-target");
            File.WriteAllText(blobSourcePath, target);
            try
            {
                var blobId = RunGit("hash-object", "-w", "--", ".git-link-target").Trim();
                RunGit("update-index", "--add", "--cacheinfo", "120000", blobId, relativePath);
            }
            finally
            {
                File.Delete(blobSourcePath);
            }
        }

        /// <summary>
        /// 通过 Git plumbing 写入 mode 160000 条目，模拟源码发布索引中的子模块指针。
        /// </summary>
        /// <param name="relativePath">待写入 index 的 gitlink 路径。</param>
        /// <param name="content">用于创建测试对象的稳定内容。</param>
        public void StageGitLinkEntry(string relativePath, string content)
        {
            var objectSourcePath = Path.Combine(PackageRoot, ".gitlink-object");
            File.WriteAllText(objectSourcePath, content);
            try
            {
                var objectId = RunGit("hash-object", "-w", "--", ".gitlink-object").Trim();
                RunGit("update-index", "--add", "--cacheinfo", "160000", objectId, relativePath);
            }
            finally
            {
                File.Delete(objectSourcePath);
            }
        }

        /// <summary>
        /// 删除 fixture 创建的整个独立包根。
        /// </summary>
        public void Dispose()
        {
            TryDeleteRoot();
        }

        /// <summary>
        /// 写入单份 bootstrap 权威模板。
        /// </summary>
        /// <param name="fileName">模板文件名。</param>
        /// <param name="content">模板内容。</param>
        private void WriteBootstrapTemplate(string fileName, string content)
        {
            WriteFile(Path.Combine(PackageRoot, "YokiFrameWorkbench~", "scripts", "runtime-bootstrap", fileName), content);
        }

        /// <summary>
        /// 在 fixture Git 根运行一条不依赖用户全局配置的 Git 命令。
        /// </summary>
        /// <param name="arguments">不含 git 可执行名的参数。</param>
        private string RunGit(params string[] arguments)
        {
            ProcessStartInfo startInfo = new("git")
            {
                WorkingDirectory = PackageRoot,
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
                ?? throw new InvalidOperationException("Unable to start git for release test.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("git " + string.Join(' ', arguments) + " failed: " + standardError + standardOutput);
            }

            return standardOutput;
        }

        /// <summary>
        /// 尝试删除临时 Git 根，避免 Windows 在 git 子进程刚退出时的短暂对象文件锁覆盖业务断言。
        /// </summary>
        private void TryDeleteRoot()
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (Directory.Exists(mRoot))
                    {
                        Directory.Delete(mRoot, recursive: true);
                    }

                    return;
                }
                catch (IOException) when (attempt < 2)
                {
                    Thread.Sleep(100);
                }
                catch (UnauthorizedAccessException) when (attempt < 2)
                {
                    Thread.Sleep(100);
                }
                catch (IOException)
                {
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// 写入文件并创建父目录。
        /// </summary>
        /// <param name="path">文件完整路径。</param>
        /// <param name="content">UTF-8 文本内容。</param>
        private static void WriteFile(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }
}
