using System.Diagnostics;
using System.Runtime.InteropServices;
using YokiFrame.Packaging.Models;

namespace YokiFrame.Packaging.Services;

/// <summary>
/// 在当前宿主执行 Workbench 与 CLI 发布，并通过 staging 原子提交项目缓存平台目录和 manifest。
/// </summary>
public sealed class RuntimePublishService
{
    private const string PRODUCT_NAME = "YokiFrameTool";

    /// <summary>
    /// 执行当前平台发布计划。
    /// </summary>
    /// <param name="plan">已校验的当前平台发布计划。</param>
    /// <returns>正式 GUI、CLI 和 manifest 入口。</returns>
    public RuntimePublishResult Publish(RuntimePublishPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        using var publishLock = RuntimePublishLock.AcquireForRuntimeRoot(plan.RuntimeRoot);
        return PublishWithLockHeld(plan);
    }

    /// <summary>
    /// 在调用方已持有项目 Runtime 包根锁时执行发布，供缓存服务把 current.json 纳入同一事务边界。
    /// </summary>
    /// <param name="plan">已校验的发布计划。</param>
    /// <returns>正式 GUI、CLI 和 manifest 入口。</returns>
    internal RuntimePublishResult PublishWithLockHeld(RuntimePublishPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        RuntimePublishTransaction.Recover(plan);
        EnsureDotnet10Sdk(plan.WorkbenchRoot);
        PrepareStagingRoot(plan);
        try
        {
            PublishGuiProject(plan, plan.StagingRoot);
            if (plan.Profile.PublishCli)
            {
                PublishCliProject(plan, plan.StagingRoot);
                MoveCliAppHost(plan);
            }

            CreateMacAppBundle(plan);
            SetExecutableBits(plan);
            RemoveDebugSymbols(plan.StagingRoot);
            ValidateStagedEntries(plan);
            CommitStagedProfile(plan);
            return CreateResult(plan);
        }
        finally
        {
            if (Directory.Exists(plan.StagingRoot))
            {
                Directory.Delete(plan.StagingRoot, true);
            }
        }
    }

    /// <summary>
    /// 确认当前 dotnet 环境包含 .NET 10 SDK；Runtime 自举不自动下载安装 SDK。
    /// </summary>
    /// <param name="workingDirectory">dotnet 命令工作目录。</param>
    private static void EnsureDotnet10Sdk(string workingDirectory)
    {
        var output = RunDotnet(workingDirectory, new[] { "--list-sdks" });
        if (!output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(static line => line.TrimStart().StartsWith("10.", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(".NET 10 SDK is required to build YokiFrame WorkbenchRuntime.");
        }
    }

    /// <summary>
    /// 清理上次中断留下的当前平台 staging，并创建空目录。
    /// </summary>
    /// <param name="plan">发布计划。</param>
    private static void PrepareStagingRoot(RuntimePublishPlan plan)
    {
        if (Directory.Exists(plan.StagingRoot))
        {
            Directory.Delete(plan.StagingRoot, true);
        }

        Directory.CreateDirectory(plan.StagingRoot);
    }

    /// <summary>
    /// 将 Workbench GUI 按 profile 选项发布到共享 staging 目录。
    /// </summary>
    /// <param name="plan">发布计划。</param>
    /// <param name="outputRoot">共享 staging 目录。</param>
    private static void PublishGuiProject(RuntimePublishPlan plan, string outputRoot)
    {
        var arguments = CreatePublishArguments(
            plan.GuiProjectPath,
            plan,
            outputRoot,
            plan.Profile.SelfContained);
        if (plan.Profile.PublishReadyToRun)
        {
            arguments.Add("-p:PublishReadyToRun=true");
        }

        if (plan.Profile.PublishAot)
        {
            AppendNativeAotArguments(arguments);
        }

        RunDotnet(plan.WorkbenchRoot, arguments);
    }

    /// <summary>
    /// 将 CLI 按当前 profile 的发布方式写入 GUI 同一 staging 目录；AOT profile 会生成独立 Native AOT CLI。
    /// </summary>
    /// <param name="plan">发布计划。</param>
    /// <param name="outputRoot">共享 staging 目录。</param>
    private static void PublishCliProject(RuntimePublishPlan plan, string outputRoot)
    {
        var arguments = CreatePublishArguments(
            plan.CliProjectPath,
            plan,
            outputRoot,
            plan.Profile.SelfContained);
        if (plan.Profile.PublishAot)
        {
            AppendNativeAotArguments(arguments);
        }

        RunDotnet(plan.WorkbenchRoot, arguments);
    }

    /// <summary>
    /// 追加 GUI 与 CLI 共用的 Native AOT 发布参数；使用入口开关避免 PublishAot 污染可移植项目引用。
    /// </summary>
    /// <param name="arguments">即将传给 dotnet publish 的参数集合。</param>
    private static void AppendNativeAotArguments(ICollection<string> arguments)
    {
        arguments.Add("-p:YokiFramePublishAot=true");
        arguments.Add("-p:StripSymbols=true");
    }

    /// <summary>
    /// 创建 GUI 与 CLI 共用的 dotnet publish 参数，调用方只追加自身发布模式属性。
    /// </summary>
    /// <param name="projectPath">待发布项目。</param>
    /// <param name="plan">发布计划。</param>
    /// <param name="outputRoot">共享 staging 目录。</param>
    /// <param name="selfContained">是否生成自包含运行时。</param>
    /// <returns>可直接交给 dotnet 子进程的参数。</returns>
    private static List<string> CreatePublishArguments(
        string projectPath,
        RuntimePublishPlan plan,
        string outputRoot,
        bool selfContained)
    {
        return new List<string>
        {
            "publish", projectPath,
            "--configuration", plan.Configuration,
            "--runtime", plan.Profile.DotnetRuntimeIdentifier,
            "--self-contained", selfContained ? "true" : "false",
            "--output", outputRoot,
            "--nologo",
            "-p:DebugType=None",
            "-p:DebugSymbols=false"
        };
    }

    /// <summary>
    /// 将 CLI apphost 改名为稳定的 `yoki` 平台入口，程序集文件保持原名供运行时加载。
    /// </summary>
    /// <param name="plan">发布计划。</param>
    private static void MoveCliAppHost(RuntimePublishPlan plan)
    {
        var sourcePath = Path.Combine(plan.StagingRoot, plan.Profile.CliAppHostName);
        var targetPath = ResolveEntryPath(plan.StagingRoot, plan.Profile.CliEntry);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("CLI apphost was not published.", sourcePath);
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(sourcePath, targetPath, comparison))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Move(sourcePath, targetPath, true);
    }

    /// <summary>
    /// 为 macOS profile 创建 `.app` 薄启动器；真实 Avalonia apphost 仍与共享依赖位于平台根。
    /// </summary>
    /// <param name="plan">发布计划。</param>
    private static void CreateMacAppBundle(RuntimePublishPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.Profile.MacAppBundleName))
        {
            return;
        }

        var bundleRoot = Path.Combine(plan.StagingRoot, plan.Profile.MacAppBundleName);
        var contentsRoot = Path.Combine(bundleRoot, "Contents");
        var macOsRoot = Path.Combine(contentsRoot, "MacOS");
        Directory.CreateDirectory(macOsRoot);
        var launcherPath = ResolveEntryPath(plan.StagingRoot, plan.Profile.GuiEntry);
        File.WriteAllText(launcherPath, CreateMacLauncherText());
        File.WriteAllText(Path.Combine(contentsRoot, "Info.plist"), CreateMacInfoPlist());
    }

    /// <summary>
    /// 创建 macOS bundle 内转发到平台根 GUI apphost 的 POSIX 启动脚本。
    /// </summary>
    /// <returns>使用 LF 换行的启动脚本。</returns>
    private static string CreateMacLauncherText()
    {
        return "#!/bin/sh\n"
            + "APP_ROOT=\"$(CDPATH= cd -- \"$(dirname -- \"$0\")/../../..\" && pwd)\"\n"
            + "exec \"$APP_ROOT/YokiFrame.Workbench.Avalonia\" \"$@\"\n";
    }

    /// <summary>
    /// 创建最小 macOS app bundle 元数据。
    /// </summary>
    /// <returns>Info.plist 文本。</returns>
    private static string CreateMacInfoPlist()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"https://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n"
            + "<plist version=\"1.0\"><dict>\n"
            + "<key>CFBundleExecutable</key><string>YokiFrame.Workbench.Avalonia</string>\n"
            + "<key>CFBundleIdentifier</key><string>com.hinatayoki.yokiframe.workbench</string>\n"
            + "<key>CFBundleName</key><string>YokiFrame Workbench</string>\n"
            + "<key>CFBundlePackageType</key><string>APPL</string>\n"
            + "</dict></plist>\n";
    }

    /// <summary>
    /// 在 Unix 宿主为 GUI、CLI 和 macOS bundle 启动器补齐执行位。
    /// </summary>
    /// <param name="plan">发布计划。</param>
    private static void SetExecutableBits(RuntimePublishPlan plan)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        AddUnixExecuteBits(Path.Combine(plan.StagingRoot, plan.Profile.GuiAppHostName));
        if (plan.Profile.PublishCli)
        {
            AddUnixExecuteBits(ResolveEntryPath(plan.StagingRoot, plan.Profile.CliEntry));
        }

        AddUnixExecuteBits(ResolveEntryPath(plan.StagingRoot, plan.Profile.GuiEntry));
    }

    /// <summary>
    /// 为存在的文件增加用户、组和其它用户执行位。
    /// </summary>
    /// <param name="path">目标文件。</param>
    private static void AddUnixExecuteBits(string path)
    {
        if ((!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsFreeBSD())
            || !File.Exists(path))
        {
            return;
        }

        var mode = File.GetUnixFileMode(path);
        mode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        File.SetUnixFileMode(path, mode);
    }

    /// <summary>
    /// 删除 staging 内所有调试符号，避免生成 profile 带入大型 PDB。
    /// </summary>
    /// <param name="stagingRoot">staging 根。</param>
    private static void RemoveDebugSymbols(string stagingRoot)
    {
        foreach (var path in Directory.EnumerateFiles(stagingRoot, "*.pdb", SearchOption.AllDirectories))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 在切换正式目录前确认 GUI 与 CLI 两个用户入口均已生成。
    /// </summary>
    /// <param name="plan">发布计划。</param>
    private static void ValidateStagedEntries(RuntimePublishPlan plan)
    {
        RequireEntry(plan.StagingRoot, plan.Profile.GuiEntry, "Runtime GUI entry was not generated.");
        if (plan.Profile.PublishCli)
        {
            RequireEntry(plan.StagingRoot, plan.Profile.CliEntry, "Runtime CLI entry was not generated.");
        }
    }

    /// <summary>
    /// 校验发布入口存在。
    /// </summary>
    /// <param name="root">平台根目录。</param>
    /// <param name="entry">相对入口。</param>
    /// <param name="message">缺失错误说明。</param>
    private static void RequireEntry(string root, string entry, string message)
    {
        var path = ResolveEntryPath(root, entry);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(message, path);
        }
    }

    /// <summary>
    /// 将 staging 切换为正式 profile，再原子更新 manifest；manifest 失败时恢复旧 profile。
    /// </summary>
    /// <param name="plan">发布计划。</param>
    private static void CommitStagedProfile(RuntimePublishPlan plan)
    {
        Directory.CreateDirectory(plan.RuntimeRoot);
        var existingManifest = new RuntimeManifestReader().ReadIfExists(plan.ManifestPath);
        RuntimePublishTransaction.Commit(plan, () => WriteMergedManifest(plan, existingManifest));
    }

    /// <summary>
    /// 基于已提交 profile 生成并原子写入合并 manifest。
    /// </summary>
    /// <param name="plan">发布计划。</param>
    /// <param name="existingManifest">提交前读取的旧 manifest。</param>
    private static void WriteMergedManifest(RuntimePublishPlan plan, RuntimeManifest? existingManifest)
    {
        var manifest = new RuntimeManifestBuilder().Build(
            plan.RuntimeRoot,
            PRODUCT_NAME,
            existingManifest,
            plan.Profile.RuntimeIdentifier,
            plan.Profile.GuiEntry,
            plan.Profile.CliEntry,
            plan.Profile.SharedRuntime);
        new RuntimeManifestWriter().Write(manifest, plan.ManifestPath);
    }

    /// <summary>
    /// 创建发布成功结果。
    /// </summary>
    /// <param name="plan">已提交发布计划。</param>
    /// <returns>正式入口结果。</returns>
    private static RuntimePublishResult CreateResult(RuntimePublishPlan plan)
    {
        var cliPath = plan.Profile.PublishCli
            ? ResolveEntryPath(plan.PublishRoot, plan.Profile.CliEntry)
            : string.Empty;
        return new RuntimePublishResult(
            plan.Profile.RuntimeIdentifier,
            plan.PublishRoot,
            ResolveEntryPath(plan.PublishRoot, plan.Profile.GuiEntry),
            cliPath,
            plan.ManifestPath);
    }

    /// <summary>
    /// 执行 dotnet 子进程并返回标准输出；失败时抛出包含标准错误的异常。
    /// </summary>
    /// <param name="workingDirectory">进程工作目录。</param>
    /// <param name="arguments">dotnet 参数。</param>
    /// <returns>标准输出文本。</returns>
    private static string RunDotnet(string workingDirectory, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
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

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start dotnet process.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(outputTask, errorTask);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "dotnet " + string.Join(' ', arguments) + " failed." + Environment.NewLine + errorTask.Result + outputTask.Result);
        }

        return outputTask.Result;
    }

    /// <summary>
    /// 将 manifest 正斜杠入口转换为指定平台根下的完整路径。
    /// </summary>
    /// <param name="root">平台根目录。</param>
    /// <param name="entry">manifest 相对入口。</param>
    /// <returns>入口完整路径。</returns>
    private static string ResolveEntryPath(string root, string entry)
    {
        return RuntimePathGuard.RequireEntryPath(root, entry, nameof(entry));
    }
}
