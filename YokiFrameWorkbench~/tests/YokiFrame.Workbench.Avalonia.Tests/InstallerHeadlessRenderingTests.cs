using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 使用真实 XAML 和 ViewModel 渲染 Installer 四种状态与两个最低视口。
/// </summary>
public sealed class InstallerHeadlessRenderingTests
{
    private static readonly IReadOnlyList<(int Width, int Height)> sViewports = new[]
    {
        (1020, 820),
        (900, 680)
    };

    /// <summary>
    /// 渲染全部状态和视口，输出可搬运 PNG 并验证关键布局边界。
    /// 像素与最低视口高度回归以 Windows 为基线；其它平台只校验控件树可渲染且不抛异常。
    /// </summary>
    [Fact]
    public async Task InstallerStatesRenderNonEmptyWithoutOverlap()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            foreach (var scenario in Enum.GetValues<InstallerRenderScenario>())
            {
                foreach (var viewport in sViewports)
                {
                    if (WorkbenchTestPlatform.SupportsInstallerPixelLayoutBaseline)
                    {
                        await RenderAndAssertAsync(scenario, viewport.Width, viewport.Height);
                    }
                    else
                    {
                        await RenderWithoutPixelBaselineAsync(scenario, viewport.Width, viewport.Height);
                    }
                }
            }
        });
    }

    /// <summary>
    /// 展开日志抽屉并验证内容位于固定命令栏上方，最低视口仍保留完整主操作。
    /// </summary>
    [Fact]
    public async Task LogDrawerExpandsAboveFixedCommandBar()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            using var fixture = InstallerHeadlessFixture.Create();
            var viewModel = await fixture.CreateViewModelAsync(InstallerRenderScenario.UnityLocal);
            Window window = new()
            {
                Width = 900,
                Height = 680,
                MinWidth = 900,
                MinHeight = 680,
                RequestedThemeVariant = ThemeVariant.Light,
                Content = new InstallerShellView(viewModel)
            };
            try
            {
                window.Show();
                var toggle = Assert.Single(
                    window.GetVisualDescendants().OfType<ToggleButton>(),
                    static control => control.Name == "LogDrawerToggle");
                toggle.IsChecked = true;
                Dispatcher.UIThread.RunJobs();

                var logList = Assert.Single(
                    window.GetVisualDescendants().OfType<ListBox>(),
                    static control => control.Name == "InstallerLogList");
                var footer = FindClass<Border>(window, "installer-footer");
                Assert.True(logList.IsVisible && logList.Bounds.Height > 80, "日志抽屉没有展开。");
                Assert.All(FindOperationButtons(window, viewModel), control => AssertContainedBy(window, footer, control));
                SaveAndAssertFrame(window, InstallerRenderScenario.UnityLocal, 900, 680, "-logs");
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// 在非 Windows headless 环境只验证窗口可创建、XAML 绑定不崩，不比对像素布局。
    /// </summary>
    private static async Task RenderWithoutPixelBaselineAsync(
        InstallerRenderScenario scenario,
        int width,
        int height)
    {
        using var fixture = InstallerHeadlessFixture.Create();
        var viewModel = await fixture.CreateViewModelAsync(scenario);
        Window window = new()
        {
            Width = width,
            Height = height,
            MinWidth = width,
            MinHeight = height,
            RequestedThemeVariant = ThemeVariant.Light,
            Content = new InstallerShellView(viewModel)
        };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(window.Content);
            Assert.NotEmpty(window.GetVisualDescendants().OfType<Control>());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// 创建场景 ViewModel、显示真实 XAML、保存截图并执行布局断言。
    /// </summary>
    /// <param name="scenario">Installer 页面状态。</param>
    /// <param name="width">窗口宽度。</param>
    /// <param name="height">窗口高度。</param>
    private static async Task RenderAndAssertAsync(
        InstallerRenderScenario scenario,
        int width,
        int height)
    {
        using var fixture = InstallerHeadlessFixture.Create();
        var viewModel = await fixture.CreateViewModelAsync(scenario);
        Window window = new()
        {
            Width = width,
            Height = height,
            MinWidth = width,
            MinHeight = height,
            RequestedThemeVariant = ThemeVariant.Light,
            Content = new InstallerShellView(viewModel)
        };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            AssertLayout(window, viewModel, width, height);
            SaveAndAssertFrame(window, scenario, width, height);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// 验证关键控件位于窗口内，左侧步骤轨道、配置工作面、审阅区和底部命令栏互不重叠。
    /// </summary>
    /// <param name="window">已完成布局的窗口。</param>
    /// <param name="viewModel">当前 Installer ViewModel。</param>
    /// <param name="expectedWidth">期望窗口宽度。</param>
    /// <param name="expectedHeight">期望窗口高度。</param>
    private static void AssertLayout(
        Window window,
        InstallerShellViewModel viewModel,
        int expectedWidth,
        int expectedHeight)
    {
        var rail = FindClass<Border>(window, "installer-rail");
        var workspace = FindClass<ScrollViewer>(window, "installer-workspace");
        var reviewPane = FindClass<Border>(window, "installer-review-pane");
        var titleBar = FindClass<Border>(window, "installer-titlebar");
        var configControls = FindVisibleConfigurationControls(window);
        var operationControls = FindOperationButtons(window, viewModel);
        var footer = FindClass<Border>(window, "installer-footer");
        var logDrawer = FindClass<Grid>(window, "installer-log-header");
        var keyControls = configControls
            .Concat(operationControls)
            .Append(rail)
            .Append(workspace)
            .Append(reviewPane)
            .Append(titleBar)
            .Append(footer)
            .Append(logDrawer)
            .ToArray();
        Assert.All(keyControls, control => AssertInsideWindow(window, control, expectedWidth, expectedHeight));

        var railBounds = GetWindowBounds(window, rail);
        var workspaceBounds = GetWindowBounds(window, workspace);
        var reviewBounds = GetWindowBounds(window, reviewPane);
        var titleBounds = GetWindowBounds(window, titleBar);
        var footerBounds = GetWindowBounds(window, footer);
        var drawerBounds = GetWindowBounds(window, logDrawer);
        Assert.True(railBounds.Right <= workspaceBounds.Left + 1, "步骤轨道与配置工作面发生重叠。");
        Assert.True(workspaceBounds.Right <= reviewBounds.Left + 1, "配置工作面与审阅区发生重叠。");
        Assert.True(Math.Abs(workspaceBounds.Top - reviewBounds.Top) <= 1, "配置工作面与审阅区顶部没有对齐。");
        Assert.True(titleBounds.Bottom <= workspaceBounds.Top + 1, "标题栏与主工作区发生重叠。");
        Assert.True(workspaceBounds.Bottom <= footerBounds.Top + 1, "配置工作面与命令栏发生重叠。");
        Assert.True(reviewBounds.Bottom <= footerBounds.Top + 1, "审阅区与命令栏发生重叠。");
        Assert.All(operationControls, control => AssertContainedBy(window, footer, control));
        Assert.True(drawerBounds.Height > 24, "日志抽屉入口在最低视口中不可见。");
        AssertPathInputDoesNotOverlapPicker(window, "installer.target.path", "installer.target.pick");
        AssertPathInputDoesNotOverlapPicker(window, "installer.source.path", "installer.source.pick");
        AssertInstallModeOptionsFillGrid(window);
        AssertButtonIconAndTextCentersMatch(window);
    }

    /// <summary>
    /// 保存当前帧并验证像素尺寸、颜色变化和 PNG 内容均非空。
    /// </summary>
    /// <param name="window">已显示窗口。</param>
    /// <param name="scenario">页面状态。</param>
    /// <param name="width">截图宽度。</param>
    /// <param name="height">截图高度。</param>
    private static void SaveAndAssertFrame(
        Window window,
        InstallerRenderScenario scenario,
        int width,
        int height,
        string suffix = "")
    {
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.True(frame.PixelSize.Width >= width - 2);
        Assert.True(frame.PixelSize.Height >= height - 2);
        AssertFrameHasVisualContent(frame);

        var outputRoot = FindScreenshotRoot();
        Directory.CreateDirectory(outputRoot);
        var outputPath = Path.Combine(
            outputRoot,
            scenario.ToString().ToLowerInvariant() + suffix + "-" + width + "x" + height + ".png");
        using FileStream stream = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        frame.Save(stream);
        Assert.True(stream.Length > 1024, "PNG 截图内容为空或异常小。");
    }

    /// <summary>
    /// 从帧缓冲抽样颜色，避免仅生成单色或全透明图片也被判为成功。
    /// </summary>
    /// <param name="frame">Headless 渲染帧。</param>
    private static void AssertFrameHasVisualContent(WriteableBitmap frame)
    {
        using var framebuffer = frame.Lock();
        var byteCount = framebuffer.RowBytes * framebuffer.Size.Height;
        byte[] pixels = new byte[byteCount];
        Marshal.Copy(framebuffer.Address, pixels, 0, byteCount);
        HashSet<int> sampledColors = new();
        var step = Math.Max(4, framebuffer.RowBytes / 64);
        for (var index = 0; index + 3 < pixels.Length; index += step)
        {
            sampledColors.Add(BitConverter.ToInt32(pixels, index));
            if (sampledColors.Count >= 8)
            {
                return;
            }
        }

        Assert.Fail("Headless 帧没有足够的像素变化。");
    }

    /// <summary>
    /// 查找当前场景中实际参与布局的路径、模式和 Godot 配置控件。
    /// </summary>
    /// <param name="window">Installer 窗口。</param>
    /// <returns>可见配置控件。</returns>
    private static IReadOnlyList<Control> FindVisibleConfigurationControls(Window window)
    {
        HashSet<string> ids = new(StringComparer.Ordinal)
        {
            "installer.source.path",
            "installer.target.path",
            "installer.mode.local",
            "installer.mode.git",
            "installer.git.url",
            "installer.godot.repair",
            "installer.godot.enable"
        };
        return window.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => ids.Contains(AutomationProperties.GetAutomationId(control) ?? string.Empty))
            .Where(static control => control.Bounds.Width > 0 && control.Bounds.Height > 0)
            .ToArray();
    }

    /// <summary>
    /// 通过真实命令实例定位预览和安装按钮，避免依赖易变显示文本。
    /// </summary>
    /// <param name="window">Installer 窗口。</param>
    /// <param name="viewModel">当前 ViewModel。</param>
    /// <returns>操作区按钮。</returns>
    private static IReadOnlyList<Control> FindOperationButtons(
        Window window,
        InstallerShellViewModel viewModel)
    {
        return window.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => ReferenceEquals(button.Command, viewModel.PreviewCommand)
                || ReferenceEquals(button.Command, viewModel.InstallCommand))
            .Cast<Control>()
            .ToArray();
    }

    /// <summary>
    /// 验证长路径文本框与同一行目录选择按钮保持明确间距。
    /// </summary>
    /// <param name="window">Installer 窗口。</param>
    /// <param name="inputId">路径输入 AutomationId。</param>
    /// <param name="pickerId">选择按钮 AutomationId。</param>
    private static void AssertPathInputDoesNotOverlapPicker(
        Window window,
        string inputId,
        string pickerId)
    {
        var input = FindAutomationControl(window, inputId);
        var picker = FindAutomationControl(window, pickerId);
        if (input.Bounds.Width <= 0 || picker.Bounds.Width <= 0)
        {
            return;
        }

        var inputBounds = GetWindowBounds(window, input);
        var pickerBounds = GetWindowBounds(window, picker);
        Assert.True(inputBounds.Right + 4 <= pickerBounds.Left, inputId + " 与选择按钮发生重叠。");
    }

    /// <summary>
    /// 验证可见的 Unity 本地包和 Git 包分段均分宽度，且只允许边框为连接视觉产生一像素接合。
    /// </summary>
    /// <param name="window">Installer 窗口。</param>
    private static void AssertInstallModeOptionsFillGrid(Window window)
    {
        var localOption = FindAutomationControl(window, "installer.mode.local");
        var gitOption = FindAutomationControl(window, "installer.mode.git");
        if (localOption.Bounds.Width <= 0 || gitOption.Bounds.Width <= 0)
        {
            return;
        }

        var localBounds = GetWindowBounds(window, localOption);
        var gitBounds = GetWindowBounds(window, gitOption);
        Assert.True(
            Math.Abs(localBounds.Width - gitBounds.Width) <= 1,
            "本地包和 Git 包选项没有均分安装模式网格宽度。");
        Assert.True(
            localBounds.Right <= gitBounds.Left + 1,
            "本地包和 Git 包分段发生几何越界。");
    }

    /// <summary>
    /// 验证控件全局 Bounds 没有逃出目标窗口。
    /// </summary>
    /// <param name="window">Installer 窗口。</param>
    /// <param name="control">待检查控件。</param>
    /// <param name="width">窗口宽度。</param>
    /// <param name="height">窗口高度。</param>
    private static void AssertInsideWindow(Window window, Control control, int width, int height)
    {
        var bounds = GetWindowBounds(window, control);
        Assert.True(bounds.Left >= -1 && bounds.Top >= -1, "控件左侧或顶部逃出窗口。");
        Assert.True(bounds.Right <= width + 1 && bounds.Bottom <= height + 1, "控件右侧或底部逃出窗口。");
    }

    /// <summary>
    /// 验证操作控件完整位于底部命令栏内，避免窄视口越界。
    /// </summary>
    /// <param name="window">Installer 窗口。</param>
    /// <param name="container">底部命令栏。</param>
    /// <param name="control">待验证操作控件。</param>
    private static void AssertContainedBy(Window window, Control container, Control control)
    {
        var containerBounds = GetWindowBounds(window, container);
        var controlBounds = GetWindowBounds(window, control);
        Assert.True(controlBounds.Left >= containerBounds.Left - 1, "操作控件左侧逃出命令栏。");
        Assert.True(controlBounds.Top >= containerBounds.Top - 1, "操作控件顶部逃出命令栏。");
        Assert.True(controlBounds.Right <= containerBounds.Right + 1, "操作控件右侧逃出命令栏。");
        Assert.True(controlBounds.Bottom <= containerBounds.Bottom + 1, "操作控件底部逃出命令栏。");
    }

    /// <summary>
    /// 验证所有可见图标文字按钮共享同一垂直中心线，避免图标因自身高度向下偏移。
    /// </summary>
    /// <param name="window">Installer 窗口。</param>
    private static void AssertButtonIconAndTextCentersMatch(Window window)
    {
        var buttonContents = window.GetVisualDescendants()
            .OfType<StackPanel>()
            .Where(static panel => panel.Classes.Contains("installer-button-content"))
            .Where(static panel => panel.Bounds.Width > 0 && panel.Bounds.Height > 0)
            .ToArray();
        Assert.NotEmpty(buttonContents);
        foreach (var content in buttonContents)
        {
            var icon = Assert.Single(content.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Path>());
            var text = Assert.Single(content.GetVisualDescendants().OfType<TextBlock>());
            var iconBounds = GetWindowBounds(window, icon);
            var textBounds = GetWindowBounds(window, text);
            var centerOffset = Math.Abs(iconBounds.Center.Y - textBounds.Center.Y);
            Assert.True(centerOffset <= 1, $"按钮图标与文字中心线偏移 {centerOffset:0.##} 像素。");
        }
    }

    /// <summary>
    /// 把控件局部 Bounds 转换到窗口坐标系。
    /// </summary>
    /// <param name="window">Installer 窗口。</param>
    /// <param name="control">待转换控件。</param>
    /// <returns>窗口坐标 Bounds。</returns>
    private static Rect GetWindowBounds(Window window, Control control)
    {
        var point = control.TranslatePoint(default, window);
        Assert.NotNull(point);
        return new Rect(point.Value, control.Bounds.Size);
    }

    /// <summary>
    /// 按 AutomationId 查找唯一控件。
    /// </summary>
    /// <param name="window">Installer 窗口。</param>
    /// <param name="automationId">AutomationId。</param>
    /// <returns>匹配控件。</returns>
    private static Control FindAutomationControl(Window window, string automationId)
    {
        return Assert.Single(window.GetVisualDescendants()
            .OfType<Control>(),
            control => string.Equals(
                AutomationProperties.GetAutomationId(control),
                automationId,
                StringComparison.Ordinal));
    }

    /// <summary>
    /// 按样式类查找唯一控件。
    /// </summary>
    /// <typeparam name="TControl">目标控件类型。</typeparam>
    /// <param name="window">Installer 窗口。</param>
    /// <param name="className">样式类名。</param>
    /// <returns>匹配控件。</returns>
    private static TControl FindClass<TControl>(Window window, string className)
        where TControl : Control
    {
        return Assert.Single(window.GetVisualDescendants()
            .OfType<TControl>(),
            control => control.Classes.Contains(className));
    }

    /// <summary>
    /// 从测试输出目录向上定位 Workbench 根，并返回可搬运截图目录。
    /// </summary>
    /// <returns>`.artifacts/screenshots/installer` 绝对路径。</returns>
    private static string FindScreenshotRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "YokiFrame.Workbench.Avalonia")))
            {
                return Path.Combine(directory.FullName, ".artifacts", "screenshots", "installer");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("无法定位 YokiFrameWorkbench~ 根目录。");
    }
}
