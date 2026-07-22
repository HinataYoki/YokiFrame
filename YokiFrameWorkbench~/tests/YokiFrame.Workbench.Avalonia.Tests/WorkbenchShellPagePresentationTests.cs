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
    public void ShellSwitchesBetweenOverviewDetailAndSpecializedPresentations()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, (_, _) => Task.CompletedTask);

        Assert.True(ReadBooleanProperty(viewModel, "IsOverviewPage"));
        Assert.Equal("框架总览", viewModel.CurrentPageTitle);
        Assert.Equal("查看框架连接、引擎通信、AI Skills 与运行日志。", viewModel.CurrentPageDescription);
        Assert.False(ReadBooleanProperty(viewModel, "IsDetailPage"));
        Assert.False(ReadBooleanProperty(viewModel, "IsFsmKitPage"));
        Assert.False(ReadBooleanProperty(viewModel, "IsDocumentationPage"));

        viewModel.SelectedPage = "FsmKit";

        Assert.False(ReadBooleanProperty(viewModel, "IsOverviewPage"));
        Assert.False(ReadBooleanProperty(viewModel, "IsDetailPage"));
        Assert.True(ReadBooleanProperty(viewModel, "IsFsmKitPage"));
        Assert.Equal("FsmKit", viewModel.CurrentPageTitle);
        Assert.Equal("观察状态机实例、当前状态、转换历史与运行证据。", viewModel.CurrentPageDescription);

        viewModel.SelectedPage = "ResKit";

        Assert.True(ReadBooleanProperty(viewModel, "IsResKitPage"));
        Assert.False(ReadBooleanProperty(viewModel, "IsFsmKitPage"));
        Assert.Equal("ResKit 资源工作台", viewModel.CurrentPageTitle);

        viewModel.SelectedPage = "Docs";

        Assert.True(ReadBooleanProperty(viewModel, "IsDocumentationPage"));
        Assert.False(ReadBooleanProperty(viewModel, "IsFsmKitPage"));
        Assert.Equal("文档", viewModel.CurrentPageTitle);
        Assert.Equal("浏览随 YokiFrame 包提供的离线文档与 API 参考。", viewModel.CurrentPageDescription);

    }

    /// <summary>验证已移除的 Architecture 历史页面名称回落到稳定默认页。</summary>
    [Fact]
    public void RemovedArchitecturePageFallsBackToDefaultPage()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, (_, _) => Task.CompletedTask)
        {
            SelectedPage = "Architecture"
        };

        Assert.Equal(WorkbenchShellViewModel.DefaultPageName, viewModel.SelectedPage);
        Assert.True(ReadBooleanProperty(viewModel, "IsOverviewPage"));
    }

    /// <summary>
    /// 验证 Shell XAML 为总览、通用详情和专用页面提供互斥绑定。
    /// </summary>
    [Fact]
    public void ShellXamlSeparatesGenericAndSpecializedContent()
    {
        var xaml = ReadWorkbenchShellViewXaml();

        Assert.Contains("IsVisible=\"{CompiledBinding IsOverviewPage}\"", xaml);
        Assert.Contains("IsVisible=\"{CompiledBinding IsDetailPage}\"", xaml);
        Assert.DoesNotContain("IsArchitecturePage", xaml);
        Assert.Contains("IsVisible=\"{CompiledBinding IsFsmKitPage}\"", xaml);
        Assert.Contains("IsVisible=\"{CompiledBinding IsResKitPage}\"", xaml);
        Assert.Contains("IsVisible=\"{CompiledBinding IsDocumentationPage}\"", xaml);
        Assert.Contains("pages:FsmKitPageView", xaml);
        Assert.Contains("pages:ResKitPageView", xaml);
        Assert.DoesNotContain("pages:ArchitecturePageView", xaml);
        Assert.Contains("pages:DocumentationPageView", xaml);
        Assert.Contains("Text=\"{CompiledBinding CurrentPageTitle}\"", xaml);
        Assert.Contains("Text=\"{CompiledBinding CurrentPageDescription}\"", xaml);
        Assert.Equal(1, CountOccurrences(xaml, "Classes=\"page-header\""));
    }

    /// <summary>
    /// 验证详情页使用带类型上下文的 DataTemplate 渲染 CurrentSections。
    /// </summary>
    [Fact]
    public void ShellDetailTemplateBindsCurrentSectionsWithCompiledDataType()
    {
        var xaml = ReadWorkbenchShellViewXaml();

        Assert.Contains("ItemsSource=\"{CompiledBinding CurrentSections}\"", xaml);
        Assert.Contains("<DataTemplate x:DataType=\"vm:WorkbenchDisplaySection\">", xaml);
        Assert.Contains("Text=\"{CompiledBinding Label}\"", xaml);
        Assert.Contains("Text=\"{CompiledBinding Value}\"", xaml);
    }

    /// <summary>
    /// 读取 ViewModel 的公开布尔属性，使缺失 presentation 契约产生明确 RED 失败。
    /// </summary>
    /// <param name="viewModel">Shell ViewModel。</param>
    /// <param name="propertyName">公开属性名。</param>
    /// <returns>布尔属性值。</returns>
    private static bool ReadBooleanProperty(WorkbenchShellViewModel viewModel, string propertyName)
    {
        var property = typeof(WorkbenchShellViewModel).GetProperty(propertyName);

        Assert.NotNull(property);
        return Assert.IsType<bool>(property.GetValue(viewModel));
    }

    /// <summary>
    /// 统计指定片段在 XAML 中出现的次数，确保 Shell 只维护一个公共页头入口。
    /// </summary>
    /// <param name="source">待检查的 XAML 文本。</param>
    /// <param name="value">待统计的稳定片段。</param>
    /// <returns>片段出现次数。</returns>
    private static int CountOccurrences(string source, string value)
    {
        return source.Split(value, StringSplitOptions.None).Length - 1;
    }

    /// <summary>
    /// 从测试输出目录向上查找 Workbench Shell XAML。
    /// </summary>
    /// <returns>Workbench Shell XAML 文本。</returns>
    private static string ReadWorkbenchShellViewXaml()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var directCandidate = Path.Combine(
                directory.FullName,
                "src",
                "YokiFrame.Workbench.Avalonia",
                "Views",
                "WorkbenchShellView.axaml");
            if (File.Exists(directCandidate))
            {
                return File.ReadAllText(directCandidate);
            }

            var workspaceCandidate = Path.Combine(
                directory.FullName,
                "Assets",
                "YokiFrame",
                "YokiFrameWorkbench~",
                "src",
                "YokiFrame.Workbench.Avalonia",
                "Views",
                "WorkbenchShellView.axaml");
            if (File.Exists(workspaceCandidate))
            {
                return File.ReadAllText(workspaceCandidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 WorkbenchShellView.axaml。");
    }
}
