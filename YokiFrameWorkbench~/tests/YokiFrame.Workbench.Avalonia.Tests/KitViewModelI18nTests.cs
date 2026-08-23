using Xunit;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 ActionKit、EventKit 与 FsmKit ViewModel 的动态语言投影。</summary>
public sealed class KitViewModelI18nTests
{
    /// <summary>切换语言时页面占位文本应立即改用英文资源。</summary>
    /// <remarks>整个测试体必须在 UI 线程执行：SetCulture 只有在 UI 线程上才会同步触发
    /// CultureChanged，否则重投影被 Post 到无人泵的 Dispatcher 队列。</remarks>
    [Fact]
    public async Task KitPages_ReprojectVisiblePlaceholdersOnCultureChange()
    {
        // 单独运行时也必须先初始化 Headless UI 线程，否则 Dispatcher 无人泵导致挂起。
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            WorkbenchI18nService service = WorkbenchI18nService.Instance;
            service.SetCulture("zh-CN");
            ActionKitPageViewModel action = new();
            EventKitPageViewModel events = new();
            FsmKitPageViewModel fsm = new();
            try
            {
                Assert.Equal("捕获堆栈", action.StackTraceButtonText);
                Assert.Equal("未选择", events.SelectedEventKey);
                Assert.Equal("等待 FsmKit 状态。", fsm.DiagnosticText);

                service.SetCulture("en-US");

                Assert.Equal("Capture Stack", action.StackTraceButtonText);
                Assert.Equal("Not selected", events.SelectedEventKey);
                Assert.Equal("Waiting for FsmKit state.", fsm.DiagnosticText);
            }
            finally
            {
                action.Dispose();
                events.Dispose();
                fsm.Dispose();
                service.SetCulture("zh-CN");
            }
        });
    }
}
