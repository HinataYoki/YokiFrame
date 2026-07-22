using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YokiFrame.Tooling.Application.Packages;
using YokiFrame.Workbench.Avalonia.Components;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖框架总览在真实 Avalonia Headless 视觉树中的一级导航和宽屏渲染结果。
/// </summary>
public sealed class WorkbenchOverviewHeadlessRenderingTests
{
    /// <summary>验证文档基准图标在真实主题资源树中同时解析几何路径和描边色。</summary>
    /// <param name="iconKey">待解析的稳定图标键。</param>
    [Theory]
    [InlineData("codegenkit")]
    [InlineData("inspectorkit")]
    [InlineData("logkit")]
    [InlineData("toolclass")]
    [InlineData("localization")]
    public async Task DocumentationAlignedNavigationIconResolvesGeometryAndThemeBrush(string iconKey)
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            NavigationIcon icon = new() { IconKey = iconKey };
            Window window = new() { Width = 80, Height = 80, Content = icon };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                global::Avalonia.Controls.Shapes.Path path = icon.GetVisualDescendants()
                    .OfType<global::Avalonia.Controls.Shapes.Path>()
                    .Single();
                Assert.NotNull(path.Data);
                Assert.NotNull(path.Stroke);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// 验证文档页紧凑标题栏中的关键词搜索框在真实视觉树可见且保持可用宽度。
    /// </summary>
    [Fact]
    public async Task DocumentationHeaderRendersKeywordSearch()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var packageRoot = Directory.GetParent(FindWorkbenchRoot())?.FullName
                ?? throw new DirectoryNotFoundException("无法定位 YokiFrame 包根。");
            var packageMetadata = YokiFramePackageMetadataReader.Read(packageRoot);
            var viewModel = new WorkbenchShellViewModel(
                () => { },
                _ => { },
                (_, _) => Task.CompletedTask,
                packageMetadata,
                _ => Task.CompletedTask);
            Window window = new()
            {
                Width = 1700,
                Height = 1060,
                Content = new WorkbenchShellView(viewModel)
            };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                var search = Assert.Single(window.GetVisualDescendants().OfType<TextBox>(), textBox =>
                    string.Equals(
                        AutomationProperties.GetAutomationId(textBox),
                        "workbench.docs.search",
                        StringComparison.Ordinal));

                var searchContainer = Assert.IsType<StackPanel>(search.Parent);
                // Headless 不支持附加 NativeWebView；只提升同一标题栏控件以验证真实布局。
                searchContainer.IsVisible = true;
                Dispatcher.UIThread.RunJobs();
                Assert.True(search.IsVisible);
                Assert.InRange(search.Bounds.Width, 220, 280);
                SaveFrame(window, "documentation-search-1700x1060.png");
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// 验证工作台分组只显示框架与文档，并保存宽屏视觉证据供人工复核。
    /// </summary>
    [Fact]
    public async Task FrameworkOverviewRendersTauriNavigationStructure()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var packageRoot = Directory.GetParent(FindWorkbenchRoot())?.FullName
                ?? throw new DirectoryNotFoundException("无法定位 YokiFrame 包根。");
            var packageMetadata = YokiFramePackageMetadataReader.Read(packageRoot);
            var viewModel = new WorkbenchShellViewModel(
                () => { },
                _ => { },
                (_, _) => Task.CompletedTask,
                packageMetadata,
                _ => Task.CompletedTask);
            Window window = new()
            {
                Width = 1700,
                Height = 1060,
                Content = new WorkbenchShellView(viewModel)
            };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                var navigation = window.GetVisualDescendants().OfType<SideNavigation>().Single();
                var navigationTexts = navigation.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Where(static textBlock => textBlock.IsVisible)
                    .Select(static textBlock => textBlock.Text)
                    .Where(static text => !string.IsNullOrWhiteSpace(text))
                    .ToArray();
                var visibleTexts = window.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Where(static textBlock => textBlock.IsVisible)
                    .Select(static textBlock => textBlock.Text)
                    .Where(static text => !string.IsNullOrWhiteSpace(text))
                    .ToArray();

                Assert.Contains("工作台", navigationTexts);
                Assert.Contains("框架", navigationTexts);
                Assert.Contains("文档", navigationTexts);
                Assert.DoesNotContain("Architecture", navigationTexts);
                Assert.Contains("FsmKit", navigationTexts);
                Assert.DoesNotContain("诊断", navigationTexts);
                Assert.DoesNotContain("AI Control", navigationTexts);
                Assert.DoesNotContain("Automation", navigationTexts);
                Assert.Contains("框架总览", visibleTexts);
                Assert.Contains("查看框架连接、引擎通信、AI Skills 与运行日志。", visibleTexts);
                Assert.DoesNotContain("Native Debug Console", visibleTexts);
                Assert.DoesNotContain("YokiFrame Kit 调试工作台", visibleTexts);
                Assert.Contains("v" + packageMetadata.Version, visibleTexts);
                var pageHeader = Assert.Single(window.GetVisualDescendants()
                    .OfType<Border>()
, static border => border.Classes.Contains("page-header"));
                Assert.InRange(pageHeader.Bounds.Height, 44, 80);
                StackPanel pageIntroduction = window.GetVisualDescendants().OfType<StackPanel>()
                    .Single(panel => string.Equals(panel.Name, "PageIntroduction", StringComparison.Ordinal));
                Assert.InRange(pageIntroduction.Bounds.X, 0, 1);
                SaveFrame(window);
                AssertCompactOverviewFitsViewport(window);
                viewModel.SelectedPage = "ResKit";
                window.Width = 1700;
                window.Height = 1060;
                Dispatcher.UIThread.RunJobs();
                Assert.InRange(pageIntroduction.Bounds.X, 0, 1);
                SaveFrame(window, "reskit-header-left-1700x1060.png");
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// 将框架总览切换到紧凑桌面视口，验证真实字号的完整双栏布局落在窗口客户区内。
    /// </summary>
    /// <param name="window">已经显示并完成首次布局的 Workbench 窗口。</param>
    private static void AssertCompactOverviewFitsViewport(Window window)
    {
        window.Width = 1280;
        window.Height = 820;
        Dispatcher.UIThread.RunJobs();

        var designSurface = window.GetVisualDescendants()
            .OfType<Grid>()
            .Single(static grid => string.Equals(grid.Name, "OverviewDesignSurface", StringComparison.Ordinal));
        AssertControlFitsViewport(designSurface, window, "总览设计画布");
        Assert.Empty(designSurface.GetVisualAncestors().OfType<Viewbox>());

        var terminalLine = designSurface.GetVisualDescendants()
            .OfType<TextBlock>()
            .First(static textBlock => textBlock.Classes.Contains("terminal-line"));
        Assert.True(terminalLine.FontSize >= 14, "紧凑视口下运行日志正文小于 14px。");

        var customSkillPanel = window.GetVisualDescendants()
            .OfType<Grid>()
            .Single(static grid => string.Equals(grid.Name, "OverviewCustomSkillPanel", StringComparison.Ordinal));
        AssertControlFitsViewport(customSkillPanel, window, "自定义 Skill 目录");

        var skillTargetRows = window.GetVisualDescendants()
            .OfType<Border>()
            .Where(static border => border.Classes.Contains("skill-target-row"))
            .ToArray();
        Assert.NotEmpty(skillTargetRows);
        foreach (var skillTargetRow in skillTargetRows)
        {
            AssertControlFitsViewport(skillTargetRow, window, "Skill 安装目标");
        }

        SaveFrame(window, "framework-overview-1280x820.png");
    }

    /// <summary>
    /// 按控件在窗口中的实际坐标检查四条边界，避免紧凑布局隐藏底部或右侧功能。
    /// </summary>
    /// <param name="control">待检查的总览控件。</param>
    /// <param name="window">承载总览的窗口。</param>
    /// <param name="label">断言失败时显示的控件语义。</param>
    private static void AssertControlFitsViewport(Control control, Window window, string label)
    {
        var topLeft = control.TranslatePoint(default, window);
        var bottomRight = control.TranslatePoint(new Point(control.Bounds.Width, control.Bounds.Height), window);
        Assert.NotNull(topLeft);
        Assert.NotNull(bottomRight);
        Assert.True(topLeft.Value.X >= -1, $"紧凑视口下{label}左侧越出窗口。");
        Assert.True(topLeft.Value.Y >= -1, $"紧凑视口下{label}顶部越出窗口。");
        Assert.True(bottomRight.Value.X <= window.ClientSize.Width + 1, $"紧凑视口下{label}右侧被窗口裁切。");
        Assert.True(bottomRight.Value.Y <= window.ClientSize.Height + 1, $"紧凑视口下{label}底部被窗口裁切。");
    }

    /// <summary>
    /// 保存框架总览 Headless 渲染帧，并拒绝空白或异常小的视觉证据。
    /// </summary>
    /// <param name="window">已经完成布局的 Workbench 窗口。</param>
    /// <param name="fileName">截图文件名；默认保存框架总览证据。</param>
    private static void SaveFrame(
        Window window,
        string fileName = "framework-overview-1700x1060.png")
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var outputDirectory = Path.Combine(FindWorkbenchRoot(), ".artifacts", "screenshots", "workbench");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, fileName);
        using FileStream stream = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        frame.Save(stream);
        Assert.True(stream.Length > 1024, "Workbench 框架总览 Headless 截图内容为空或异常小。");
    }

    /// <summary>
    /// 从测试输出目录向上定位 Workbench 源码根，确保视觉证据不会写入运行时发布目录。
    /// </summary>
    /// <returns>YokiFrameWorkbench~ 绝对路径。</returns>
    private static string FindWorkbenchRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "YokiFrame.Workbench.Avalonia")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("无法定位 YokiFrameWorkbench~ 源码根。");
    }
}
