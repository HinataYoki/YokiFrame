using YokiFrame.Workbench.Avalonia.Views.Pages;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 锁定 Workbench 文档 WebView 的项目级可变数据目录，避免浏览器缓存进入受管发布包。
/// </summary>
public sealed class DocumentationWebViewStorageTests
{
    /// <summary>
    /// 验证已知项目根时，WebView2 数据固定落在项目 `.yokiframe/workbench` 状态目录。
    /// </summary>
    [Fact]
    public void ResolveWebView2UserDataFolderUsesProjectScopedWorkbenchState()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-webview2-tests", "project");
        ToolStartupOptions startupOptions = new(
            ToolStartupMode.Workbench,
            projectRoot,
            projectRoot,
            projectRoot);

        var result = DocumentationPageView.ResolveWebView2UserDataFolder(startupOptions);

        Assert.Equal(Path.Combine(projectRoot, ".yokiframe", "workbench", "webview2"), result);
    }

    /// <summary>
    /// 验证设计期或未知启动上下文不推测项目路径，交由平台默认行为处理。
    /// </summary>
    [Fact]
    public void ResolveWebView2UserDataFolderReturnsNullWithoutStartupOptions()
    {
        var result = DocumentationPageView.ResolveWebView2UserDataFolder(null);

        Assert.Null(result);
    }
}
