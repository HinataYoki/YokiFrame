using System.Diagnostics;
using System.Text.Json;
using YokiFrame.Tooling.Application.Models.LocalizationKit;
using YokiFrame.Tooling.Application.Services.LocalizationKit;

namespace YokiFrame.Tooling.Application.Tests.LocalizationKit;

/// <summary>验证 LocalizationKit standalone JSON 的并发提交和文件系统路径边界。</summary>
public sealed class LocalizationKitApplicationServicePersistenceTests
{
    /// <summary>并发补充不同语言时必须在锁内重读最新文件，不能由后提交者覆盖先提交者。</summary>
    [Fact]
    public async Task ConcurrentAddsPreserveBothValues()
    {
        using TestProject project = TestProject.Create();
        LocalizationKitApplicationService service = new();

        LocalizationOperationResult[] results = await Task.WhenAll(
            Task.Run(() => service.Add(project.CreateAddRequest("English", "Start"))),
            Task.Run(() => service.Add(project.CreateAddRequest("Japanese", "スタート"))));

        Assert.All(results, result => Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics)));
        LocalizationEntryRecord entry = Assert.Single(service.Load(project.Options).Entries);
        Assert.Equal("Start", entry.Values["English"]);
        Assert.Equal("スタート", entry.Values["Japanese"]);
    }

    /// <summary>外部持有同源文件命名 Mutex 时 Add 必须等待，证明 CLI 与 Workbench 可跨实例串行提交。</summary>
    [Fact]
    public async Task AddWaitsForSourceWriteMutex()
    {
        using TestProject project = TestProject.Create();
        using ManualResetEventSlim mutexAcquired = new(false);
        using ManualResetEventSlim releaseMutex = new(false);
        Thread holder = StartMutexHolder(project.SourcePath, mutexAcquired, releaseMutex);
        Assert.True(mutexAcquired.Wait(TimeSpan.FromSeconds(2)));

        Task<LocalizationOperationResult>? addTask = null;
        try
        {
            addTask = Task.Run(() => new LocalizationKitApplicationService()
                .Add(project.CreateAddRequest("English", "Start")));
            await Task.Delay(150);
            Assert.False(addTask.IsCompleted);
        }
        finally
        {
            releaseMutex.Set();
            Assert.True(holder.Join(TimeSpan.FromSeconds(2)));
        }

        LocalizationOperationResult result = await addTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
    }

    /// <summary>项目内目录链接不得把 standalone JSON 读取重定向到项目根之外。</summary>
    [Fact]
    public void LoadRejectsNestedDirectoryReparsePoint()
    {
        using TestProject project = TestProject.Create();
        string outsideRoot = CreateTemporaryDirectory("yokiframe-localization-outside-");
        string linkedDirectory = Path.Combine(project.Root, "linked");
        try
        {
            File.Copy(project.SourcePath, Path.Combine(outsideRoot, "localization.json"));
            CreateDirectoryLink(linkedDirectory, outsideRoot);
            LocalizationKitOptions linkedOptions = new()
            {
                ProjectRoot = project.Root,
                SourcePath = Path.Combine("linked", "localization.json")
            };

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => new LocalizationKitApplicationService().Load(linkedOptions));

            Assert.Contains("重解析点", exception.Message);
        }
        finally
        {
            DeleteDirectoryLink(linkedDirectory);
            if (Directory.Exists(outsideRoot)) Directory.Delete(outsideRoot, true);
        }
    }

    /// <summary>项目根自身为目录链接时也必须拒绝，避免只检查子路径而遗漏根边界。</summary>
    [Fact]
    public void LoadRejectsReparsePointProjectRoot()
    {
        using TestProject project = TestProject.Create();
        string linkedRoot = Path.Combine(Path.GetTempPath(), "yokiframe-localization-link-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateDirectoryLink(linkedRoot, project.Root);
            LocalizationKitOptions linkedOptions = new() { ProjectRoot = linkedRoot, SourcePath = "localization.json" };

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => new LocalizationKitApplicationService().Load(linkedOptions));

            Assert.Contains("重解析点", exception.Message);
        }
        finally
        {
            DeleteDirectoryLink(linkedRoot);
        }
    }

    /// <summary>在专用线程持有源文件 Mutex，避免跨 await 在线程池线程释放线程亲和的 Mutex。</summary>
    /// <param name="sourcePath">待锁定的 LocalizationKit JSON 绝对路径。</param>
    /// <param name="acquired">Mutex 已取得信号。</param>
    /// <param name="release">允许释放 Mutex 的信号。</param>
    /// <returns>已经启动的后台持锁线程。</returns>
    private static Thread StartMutexHolder(
        string sourcePath,
        ManualResetEventSlim acquired,
        ManualResetEventSlim release)
    {
        Thread holder = new(() =>
        {
            using Mutex mutex = new(false, LocalizationKitApplicationService.CreateSourceWriteMutexName(sourcePath));
            mutex.WaitOne();
            acquired.Set();
            release.Wait();
            mutex.ReleaseMutex();
        })
        {
            IsBackground = true
        };
        holder.Start();
        return holder;
    }

    /// <summary>创建独立临时目录，供路径越界测试承载项目外文件。</summary>
    /// <param name="prefix">临时目录名称前缀。</param>
    /// <returns>已经创建的临时目录绝对路径。</returns>
    private static string CreateTemporaryDirectory(string prefix)
    {
        string path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>创建目录重解析点；Windows 符号链接无权限时回落到普通用户可创建的 Junction。</summary>
    /// <param name="linkPath">待创建链接路径。</param>
    /// <param name="targetPath">链接目标目录。</param>
    private static void CreateDirectoryLink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception exception) when (exception is PlatformNotSupportedException or UnauthorizedAccessException or IOException)
        {
            if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("当前平台无法创建目录符号链接。", exception);
            CreateWindowsJunction(linkPath, targetPath, exception);
        }
    }

    /// <summary>通过 Windows 内置 mklink 创建 Junction，保证测试无需符号链接提权。</summary>
    /// <param name="linkPath">待创建 Junction 路径。</param>
    /// <param name="targetPath">Junction 目标目录。</param>
    /// <param name="symlinkException">符号链接创建失败的原始异常。</param>
    private static void CreateWindowsJunction(string linkPath, string targetPath, Exception symlinkException)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = System.Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("mklink");
        startInfo.ArgumentList.Add("/J");
        startInfo.ArgumentList.Add(linkPath);
        startInfo.ArgumentList.Add(targetPath);
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 mklink 创建测试 Junction。", symlinkException);
        process.WaitForExit();
        if (process.ExitCode != 0 || !Directory.Exists(linkPath))
            throw new InvalidOperationException("无法创建测试 Junction。", symlinkException);
    }

    /// <summary>删除测试目录链接本身，不递归触碰其目标目录。</summary>
    /// <param name="linkPath">待删除的目录链接。</param>
    private static void DeleteDirectoryLink(string linkPath)
    {
        if (Directory.Exists(linkPath)) Directory.Delete(linkPath);
    }

    /// <summary>提供包含三种语言的最小 standalone JSON 项目。</summary>
    private sealed class TestProject : IDisposable
    {
        /// <summary>记录临时项目路径并建立稳定的源文件选项。</summary>
        /// <param name="root">已创建的项目根目录。</param>
        private TestProject(string root)
        {
            Root = root;
            SourcePath = Path.Combine(root, "localization.json");
            Options = new LocalizationKitOptions { ProjectRoot = root, SourcePath = "localization.json" };
        }

        /// <summary>获取临时项目根目录。</summary>
        public string Root { get; }

        /// <summary>获取 standalone JSON 绝对路径。</summary>
        public string SourcePath { get; }

        /// <summary>获取服务调用使用的项目选项。</summary>
        public LocalizationKitOptions Options { get; }

        /// <summary>创建最小项目并写入一条仅含简体中文的文本。</summary>
        /// <returns>可用于并发写入和路径测试的临时项目。</returns>
        public static TestProject Create()
        {
            TestProject project = new(CreateTemporaryDirectory("yokiframe-localization-persistence-"));
            File.WriteAllText(project.SourcePath, JsonSerializer.Serialize(new
            {
                formatVersion = 1,
                languages = new[] { new { id = "ChineseSimplified" }, new { id = "English" }, new { id = "Japanese" } },
                texts = new[]
                {
                    new { id = 1, key = "start", values = new Dictionary<string, string> { ["ChineseSimplified"] = "开始" } }
                }
            }, new JsonSerializerOptions { WriteIndented = true }));
            return project;
        }

        /// <summary>构造针对同一文本编号的单语言补充请求。</summary>
        /// <param name="language">目标语言。</param>
        /// <param name="value">待写入文本。</param>
        /// <returns>绑定当前临时项目的补充请求。</returns>
        public LocalizationAddRequest CreateAddRequest(string language, string value) => new()
        {
            Options = Options,
            TextId = 1,
            Language = language,
            Value = value
        };

        /// <summary>删除临时项目及其 standalone JSON。</summary>
        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
