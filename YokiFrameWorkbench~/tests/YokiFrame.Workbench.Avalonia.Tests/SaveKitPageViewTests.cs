using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views;
using YokiFrame.Workbench.Avalonia.Views.Pages;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 SaveKit 页面在宽屏和紧凑桌面尺寸下可以加载核心区域。</summary>
public sealed class SaveKitPageViewTests
{
    /// <summary>检查配置、统计和文件浏览区域均进入 Avalonia 视觉树。</summary>
    [Theory]
    [InlineData(1700, 1060)]
    [InlineData(1280, 820)]
    public async Task SaveKitPageRendersConfigurationAndFileBrowser(double width, double height)
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            SaveKitPageView view = new() { DataContext = new SaveKitPageViewModel() };
            Window window = new() { Width = width, Height = height, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.True(view.IsVisible);
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), item => item.Text == "存档目录");
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), item => item.Text == "文件扩展名");
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), item => item.Text == "运行时状态");
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), item => item.Text == "存档文件");
                Assert.Single(view.GetVisualDescendants().OfType<GridSplitter>());
                Assert.DoesNotContain(view.GetVisualDescendants().OfType<ScrollBar>(), item => item.IsVisible && item.Orientation == Orientation.Horizontal);
                SaveFrame(window, width, height, "page");
            }
            finally { window.Close(); }
        });
    }

    /// <summary>检查 SaveKit 与导航和页头一起渲染时仍保持完整双栏布局。</summary>
    [Theory]
    [InlineData(1700, 1060)]
    [InlineData(1280, 820)]
    public async Task SaveKitPageRendersInsideWorkbenchShell(double width, double height)
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            WorkbenchShellViewModel viewModel = new(() => { }, _ => { }, (_, _) => Task.CompletedTask)
            {
                SelectedPage = "SaveKit"
            };
            Window window = new() { Width = width, Height = height, Content = new WorkbenchShellView(viewModel) };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                SaveKitPageView page = window.GetVisualDescendants().OfType<SaveKitPageView>().Single();
                Assert.True(page.IsVisible);
                Assert.Single(page.GetVisualDescendants().OfType<GridSplitter>());
                Assert.DoesNotContain(page.GetVisualDescendants().OfType<ScrollBar>(), item => item.IsVisible && item.Orientation == Orientation.Horizontal);
                SaveFrame(window, width, height, "shell");
            }
            finally { window.Close(); }
        });
    }

    /// <summary>保存 SaveKit 两档目标尺寸的 Headless 帧，供人工复核布局没有退化。</summary>
    private static void SaveFrame(Window window, double width, double height, string prefix)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        string outputDirectory = Path.Combine(
            FindWorkbenchRoot(), ".artifacts", "screenshots", "workbench");
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, "savekit-" + prefix + "-" + (int)width + "x" + (int)height + ".png");
        using FileStream stream = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        frame.Save(stream);
        Assert.True(stream.Length > 1024, "SaveKit Headless 截图内容为空或异常小。");
    }

    /// <summary>从测试输出目录向上定位 Workbench 源码根。</summary>
    private static string FindWorkbenchRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "YokiFrame.Workbench.Avalonia")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("无法定位 YokiFrameWorkbench~ 根目录。");
    }
}
