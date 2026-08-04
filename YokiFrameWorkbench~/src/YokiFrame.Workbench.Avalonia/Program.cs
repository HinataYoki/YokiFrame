using Avalonia;
using Avalonia.Skia;
using Avalonia.Win32;
using YokiFrame;
using YokiFrame.RuntimeCache;
using YokiFrame.Workbench.Avalonia.Diagnostics;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia;

/// <summary>
/// YokiFrame Avalonia Workbench 桌面入口。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 获取主入口在 Avalonia 初始化前解析的启动选项；设计期或 Headless 入口可能为空。
    /// </summary>
    internal static ToolStartupOptions? StartupOptions { get; private set; }

    /// <summary>
    /// 获取当前 Workbench owner 的激活协调器；Installer 或降级启动时为空。
    /// </summary>
    internal static WorkbenchActivationCoordinator? ActivationCoordinator { get; private set; }

    /// <summary>
    /// 启动 Avalonia 桌面生命周期；初始化前不访问 UI 相关服务。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    [STAThread]
    public static void Main(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var appBaseDirectory = AppContext.BaseDirectory;
        var options = ToolStartupOptions.FromArgs(args, currentDirectory, appBaseDirectory);
        WorkbenchStartupTrace.Configure(options);
        using var runtimeLease = TryAcquireRuntimeLease(options);
        TryPruneProjectStorage(options);
        WorkbenchStartupTrace.Mark("main.enter");
        using var activationCoordinator = CreateActivationCoordinator(options);
        if (activationCoordinator?.ActivationRedirected == true)
        {
            WorkbenchStartupTrace.Mark("main.redirected");
            return;
        }

        if (activationCoordinator?.CoordinationDegraded == true)
        {
            WorkbenchStartupTrace.Mark("main.activation-degraded");
        }

        StartupOptions = options;
        ActivationCoordinator = activationCoordinator?.IsPrimaryInstance == true
            ? activationCoordinator
            : null;
        try
        {
            WorkbenchStartupTrace.Mark("main.before-lifetime");
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            ActivationCoordinator = null;
            StartupOptions = null;
            WorkbenchStartupTrace.Mark("main.after-lifetime");
        }
    }

    /// <summary>
    /// 仅为 Workbench 模式创建项目级单实例协调器，Installer 保持普通独立窗口行为。
    /// </summary>
    /// <param name="options">已解析启动选项。</param>
    /// <returns>Workbench 协调器；Installer 模式为空。</returns>
    private static WorkbenchActivationCoordinator? CreateActivationCoordinator(ToolStartupOptions options)
    {
        return options.Mode == ToolStartupMode.Workbench
            ? WorkbenchActivationCoordinator.Start(options.ProjectRoot)
            : null;
    }

    /// <summary>
    /// 在 Workbench 启动阶段回收项目级过期证据；清理失败不能阻断 UI 启动。
    /// </summary>
    /// <param name="options">已解析的工具启动选项。</param>
    private static void TryPruneProjectStorage(ToolStartupOptions options)
    {
        if (options.Mode != ToolStartupMode.Workbench)
        {
            return;
        }

        try
        {
            _ = YokiFrameFileBridgePruner.Prune(options.ProjectRoot);
        }
        catch (Exception exception)
        {
            WorkbenchStartupTrace.Mark("storage-cleanup.failed." + exception.GetType().Name);
        }
    }

    /// <summary>
    /// 在 Workbench 生命周期内保护启动时指向的 Runtime fingerprint，避免后台清理误删正在运行的文件。
    /// </summary>
    /// <param name="options">已解析的启动选项。</param>
    /// <returns>当前 Runtime lease；无有效指针或目录时返回空。</returns>
    private static RuntimeCacheLease? TryAcquireRuntimeLease(ToolStartupOptions options)
    {
        if (options.Mode != ToolStartupMode.Workbench)
        {
            return null;
        }

        var fingerprint = WorkbenchRuntimeUpdateService.ReadCurrentFingerprint(options.ProjectRoot);
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return null;
        }

        var lease = RuntimeCacheLease.TryAcquire(options.ProjectRoot, fingerprint);
        if (lease == null)
        {
            WorkbenchStartupTrace.Mark("runtime-cache.lease-unavailable");
        }

        return lease;
    }

    /// <summary>
    /// 创建 Avalonia AppBuilder，供运行时和设计工具复用。
    /// </summary>
    /// <returns>Avalonia 应用构建器。</returns>
    public static AppBuilder BuildAvaloniaApp()
    {
        WorkbenchStartupTrace.Mark("main.before-build");
        var builder = AppBuilder.Configure<WorkbenchApp>();
        builder = OperatingSystem.IsWindows()
            ? builder.UseWin32().UseSkia().UseHarfBuzz()
            : builder.UsePlatformDetect();
        builder = builder.WithInterFont();
#if DEBUG
        builder = builder.LogToTrace();
#endif
        WorkbenchStartupTrace.Mark("main.after-build");
        return builder;
    }
}
