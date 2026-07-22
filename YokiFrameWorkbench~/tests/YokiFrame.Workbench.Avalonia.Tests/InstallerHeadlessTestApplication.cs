using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 为普通 xUnit 2 测试一次性初始化加载真实 Workbench 资源的 Headless 应用。
/// </summary>
internal static class InstallerHeadlessTestApplication
{
    private static readonly object sInitializationLock = new();
    private static readonly TaskCompletionSource<bool> sInitializationCompletion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private static bool sInitializationStarted;

    /// <summary>
    /// 确保 Headless Avalonia 在专用 UI 线程中仅初始化一次并启动消息循环。
    /// </summary>
    internal static void EnsureInitialized()
    {
        lock (sInitializationLock)
        {
            if (!sInitializationStarted)
            {
                sInitializationStarted = true;
                Thread uiThread = new(RunDispatcherLoop)
                {
                    IsBackground = true,
                    Name = "YokiFrame Installer Headless UI"
                };
                uiThread.Start();
            }
        }

        sInitializationCompletion.Task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// 在固定线程初始化 Workbench 应用，并持续处理测试提交的 Dispatcher 工作。
    /// </summary>
    private static void RunDispatcherLoop()
    {
        try
        {
            AppBuilder.Configure<WorkbenchApp>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = false,
                    ShouldRenderOnUIThread = true
                })
                .UseSkia()
                .SetupWithoutStarting();
            sInitializationCompletion.TrySetResult(true);
            Dispatcher.UIThread.MainLoop(CancellationToken.None);
        }
        catch (Exception exception)
        {
            sInitializationCompletion.TrySetException(exception);
        }
    }
}
