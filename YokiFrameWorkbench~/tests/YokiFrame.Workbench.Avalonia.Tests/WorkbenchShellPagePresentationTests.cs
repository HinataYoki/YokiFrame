using Avalonia.Threading;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Workbench Shell 的总览、通用详情和专用页面呈现契约。
/// </summary>
public sealed class WorkbenchShellPagePresentationTests
{
    /// <summary>
    /// 验证 Shell 根据 Catalog presentation 在总览、详情和专用页面状态之间切换。
    /// </summary>
    [Fact]
    public void SelectedPage_切换总览专用页和文档_呈现状态跟随当前页()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        Dispatcher.UIThread.Invoke(static () => WorkbenchI18nService.Instance.SetCulture("zh-CN"));
        try
        {
            using var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, (_, _) => Task.CompletedTask);
            AssertSelectedPagePresentation(viewModel);
        }
        finally
        {
            Dispatcher.UIThread.Invoke(static () => WorkbenchI18nService.Instance.SetCulture("zh-CN"));
        }
    }

    /// <summary>
    /// 断言总览、FsmKit、ResKit 和文档页的呈现状态跟随当前选择。
    /// </summary>
    /// <param name="viewModel">待检查的 Shell。</param>
    private static void AssertSelectedPagePresentation(WorkbenchShellViewModel viewModel)
    {
        Assert.True(viewModel.IsOverviewPage);
        Assert.Equal("框架总览", viewModel.CurrentPageTitle);
        Assert.Equal("查看框架连接、引擎通信、AI Skills 与运行日志。", viewModel.CurrentPageDescription);
        Assert.False(viewModel.IsDetailPage);
        Assert.False(viewModel.IsFsmKitPage);
        Assert.False(viewModel.IsDocumentationPage);
        Assert.False(viewModel.IsWorkspacePage);
        Assert.Null(viewModel.ActiveWorkspacePage);

        viewModel.SelectedPage = "FsmKit";

        Assert.False(viewModel.IsOverviewPage);
        Assert.False(viewModel.IsDetailPage);
        Assert.True(viewModel.IsFsmKitPage);
        Assert.True(viewModel.IsWorkspacePage);
        Assert.Same(viewModel.FsmKitPage, viewModel.ActiveWorkspacePage);
        Assert.Equal("FsmKit", viewModel.CurrentPageTitle);
        Assert.Equal("观察状态机实例、当前状态、转换历史与运行证据。", viewModel.CurrentPageDescription);

        viewModel.SelectedPage = "ResKit";

        Assert.True(viewModel.IsResKitPage);
        Assert.False(viewModel.IsFsmKitPage);
        Assert.Same(viewModel.ResKitPage, viewModel.ActiveWorkspacePage);
        Assert.Equal("ResKit 资源工作台", viewModel.CurrentPageTitle);

        viewModel.SelectedPage = "Docs";

        Assert.True(viewModel.IsDocumentationPage);
        Assert.False(viewModel.IsFsmKitPage);
        Assert.Same(viewModel.DocumentationPage, viewModel.ActiveWorkspacePage);
        Assert.Equal("文档", viewModel.CurrentPageTitle);
        Assert.Equal("浏览随 YokiFrame 包提供的离线文档与 API 参考。", viewModel.CurrentPageDescription);
    }
}
