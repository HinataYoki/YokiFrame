using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YokiFrame.Tooling.Application.Models.ActionKit;
using YokiFrame.Tooling.Application.Packages;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views;
using YokiFrame.Workbench.Avalonia.Views.Pages;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>覆盖 ActionKit 树优先响应式布局在最小窗口和默认窗口中的真实渲染结果。</summary>
public sealed class ActionKitHeadlessRenderingTests
{
    /// <summary>验证目标窗口内的 Roots 密度、递归树宽度、Inspector 断点和 12px 字号红线。</summary>
    /// <param name="width">待验证窗口宽度。</param>
    /// <param name="height">待验证窗口高度。</param>
    /// <param name="expectsWideInspector">是否应显示常驻节点详情。</param>
    [Theory]
    [InlineData(1280, 820, false)]
    [InlineData(1700, 1060, true)]
    public async Task ActionKitTreeFirstLayoutFitsTargetViewport(
        int width,
        int height,
        bool expectsWideInspector)
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            WorkbenchShellViewModel shellViewModel = CreateShellViewModel();
            shellViewModel.ActionKitPage.ApplyPeriodicState(CreateVisualState());
            shellViewModel.SelectedPage = "ActionKit";
            Window window = new()
            {
                Width = width,
                Height = height,
                Content = new WorkbenchShellView(shellViewModel)
            };

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                AssertActionKitLayout(window, expectsWideInspector);
                SaveFrame(window, $"actionkit-tree-first-{width}x{height}.png");
            }
            finally
            {
                window.Close();
                shellViewModel.ActionKitPage.Dispose();
            }
        });
    }

    /// <summary>验证紧凑模式诊断抽屉可展开到受控高度并显示节点详情页签。</summary>
    [Fact]
    public async Task CompactDrawerExpandsWithinConfiguredBounds()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            WorkbenchShellViewModel shellViewModel = CreateShellViewModel();
            shellViewModel.ActionKitPage.ApplyPeriodicState(CreateVisualState());
            shellViewModel.SelectedPage = "ActionKit";
            Window window = new()
            {
                Width = 1280,
                Height = 820,
                Content = new WorkbenchShellView(shellViewModel)
            };

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                ActionKitPageView page = window.GetVisualDescendants().OfType<ActionKitPageView>().Single();
                ToggleButton toggle = page.FindControl<ToggleButton>("DrawerToggleButton")!;
                Border drawer = page.FindControl<Border>("DrawerPanel")!;
                toggle.IsChecked = true;
                Dispatcher.UIThread.RunJobs();

                Assert.InRange(drawer.Bounds.Height, 180D, 300D);
                Assert.True(page.FindControl<TabItem>("CompactInspectorTab")!.IsVisible);
                SaveFrame(window, "actionkit-drawer-expanded-1280x820.png");
            }
            finally
            {
                window.Close();
                shellViewModel.ActionKitPage.Dispose();
            }
        });
    }

    /// <summary>创建不启动外部刷新循环的 Workbench 壳层视图模型。</summary>
    /// <returns>可切换到 ActionKit 页面的壳层视图模型。</returns>
    private static WorkbenchShellViewModel CreateShellViewModel()
    {
        string packageRoot = Directory.GetParent(FindWorkbenchRoot())?.FullName
            ?? throw new DirectoryNotFoundException("无法定位 YokiFrame 包根。");
        YokiFramePackageMetadata packageMetadata = YokiFramePackageMetadataReader.Read(packageRoot);
        return new WorkbenchShellViewModel(
            () => { },
            _ => { },
            (_, _) => Task.CompletedTask,
            packageMetadata,
            _ => Task.CompletedTask);
    }

    /// <summary>检查响应式列宽、递归树密度、字号和水平滚动约束。</summary>
    /// <param name="window">已经完成布局的 Workbench 窗口。</param>
    /// <param name="expectsWideInspector">是否应显示常驻节点详情。</param>
    private static void AssertActionKitLayout(Window window, bool expectsWideInspector)
    {
        ActionKitPageView page = window.GetVisualDescendants().OfType<ActionKitPageView>().Single();
        ListBox roots = page.GetVisualDescendants().OfType<ListBox>()
            .Single(static control => control.Classes.Contains("actionkit-root-list"));
        TreeView tree = page.GetVisualDescendants().OfType<TreeView>()
            .Single(static control => control.Classes.Contains("actionkit-tree"));
        Border inspector = page.FindControl<Border>("InspectorPanel")!;
        Border drawer = page.FindControl<Border>("DrawerPanel")!;
        TabItem compactInspector = page.FindControl<TabItem>("CompactInspectorTab")!;

        Assert.True(tree.Bounds.Width >= 600D, "ActionKit 执行树未获得足够的主区宽度。");
        Assert.Equal(expectsWideInspector, inspector.IsVisible);
        Assert.Equal(!expectsWideInspector, compactInspector.IsVisible);
        Assert.InRange(drawer.Bounds.Height, 31D, 33D);
        AssertControlFitsViewport(page, window, "ActionKit 页面");
        AssertControlFitsViewport(tree, window, "ActionKit 执行树");
        if (expectsWideInspector) AssertControlFitsViewport(inspector, window, "ActionKit Inspector");
        Assert.Empty(page.GetVisualAncestors().OfType<Viewbox>());

        ListBoxItem[] visibleRoots = roots.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Where(item => IsFullyVisible(item, roots))
            .ToArray();
        double rootItemHeight = roots.GetVisualDescendants().OfType<ListBoxItem>().First().Bounds.Height;
        Assert.True(
            visibleRoots.Length >= 8,
            $"ActionKit 首屏仅完整显示 {visibleRoots.Length} 个活动根，列表高 {roots.Bounds.Height:F0}px，行高 {rootItemHeight:F0}px。");

        TreeViewItem[] visibleNodes = tree.GetVisualDescendants()
            .OfType<TreeViewItem>()
            .Where(item => IsFullyVisible(item, tree))
            .ToArray();
        Assert.True(visibleNodes.Length >= 8, $"ActionKit 首屏仅完整显示 {visibleNodes.Length} 个树节点。");

        TextBlock[] visibleText = page.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(static text => text.IsVisible && !string.IsNullOrWhiteSpace(text.Text))
            .ToArray();
        Assert.NotEmpty(visibleText);
        Assert.All(visibleText, static text => Assert.True(
            text.FontSize >= 12D,
            $"ActionKit 文本“{text.Text}”小于 12px。"));
        Assert.Contains(visibleText, static text => string.Equals(text.Text, "L7", StringComparison.Ordinal));
        Assert.Contains(visibleText, static text => string.Equals(text.Text, "PAR", StringComparison.Ordinal));
        Assert.Contains(visibleText, static text => string.Equals(text.Text, "REP", StringComparison.Ordinal));

        ScrollBar[] horizontalScrollBars = page.GetVisualDescendants()
            .OfType<ScrollBar>()
            .Where(static bar => bar.IsVisible
                && bar.Orientation == global::Avalonia.Layout.Orientation.Horizontal)
            .ToArray();
        Assert.Empty(horizontalScrollBars);
    }

    /// <summary>判断列表项的上下边界是否完整落在所属滚动视口内。</summary>
    /// <param name="item">待检查的列表项。</param>
    /// <param name="viewport">承载列表项的滚动控件。</param>
    /// <returns>列表项完整可见时返回 true。</returns>
    private static bool IsFullyVisible(Control item, Control viewport)
    {
        Point? topLeft = item.TranslatePoint(default, viewport);
        if (topLeft == null) return false;
        return topLeft.Value.Y >= -1D
            && topLeft.Value.Y + item.Bounds.Height <= viewport.Bounds.Height + 1D;
    }

    /// <summary>按控件的窗口坐标检查四条边界，拒绝目标窗口下的裁切。</summary>
    /// <param name="control">待检查控件。</param>
    /// <param name="window">承载页面的窗口。</param>
    /// <param name="label">断言失败时使用的控件语义。</param>
    private static void AssertControlFitsViewport(Control control, Window window, string label)
    {
        Point? topLeft = control.TranslatePoint(default, window);
        Point? bottomRight = control.TranslatePoint(
            new Point(control.Bounds.Width, control.Bounds.Height),
            window);
        Assert.NotNull(topLeft);
        Assert.NotNull(bottomRight);
        Assert.True(topLeft.Value.X >= -1D, $"{label}左侧越出窗口。");
        Assert.True(topLeft.Value.Y >= -1D, $"{label}顶部越出窗口。");
        Assert.True(bottomRight.Value.X <= window.ClientSize.Width + 1D, $"{label}右侧被窗口裁切。");
        Assert.True(bottomRight.Value.Y <= window.ClientSize.Height + 1D, $"{label}底部被窗口裁切。");
    }

    /// <summary>创建十个活动根、十层组合树、调用帧和终态事件的视觉状态。</summary>
    /// <returns>可覆盖紧凑与宽屏布局的 ActionKit 强类型状态。</returns>
    private static WorkbenchActionKitState CreateVisualState()
    {
        WorkbenchActionKitRoot[] roots = Enumerable.Range(0, 10)
            .Select(CreateVisualRoot)
            .ToArray();
        WorkbenchActionKitEvent[] events = Enumerable.Range(0, 8)
            .Select(index => new WorkbenchActionKitEvent(
                (9000 + index).ToString(),
                index % 2 == 0 ? "Sequence" : "Parallel",
                index == 3 ? "Faulted" : "Completed",
                5200 + index,
                index == 3 ? "Simulated action failure" : string.Empty))
            .ToArray();
        object source = CreateInternalDataSource(
            "unity-editor", "action-visual", 7L, "PlayMode",
            DateTimeOffset.Parse("2026-07-19T08:00:00Z"), "telemetry", string.Empty,
            new[] { "Global\\YokiFrame.ActionKit" }, string.Empty, "{}");
        return CreateInternal<WorkbenchActionKitState>(
            source,
            12L,
            new WorkbenchActionKitStats(5288L, roots.Length, 1736L, 18L, 3L, 1815L, true, 2),
            roots,
            events,
            roots.Length,
            1815L,
            false,
            false,
            false,
            false,
            false);
    }

    /// <summary>创建一个具备递归组合树和可辨识元数据的活动根。</summary>
    /// <param name="index">根动作顺序。</param>
    /// <returns>第一个根包含十层树，其余根包含一个叶动作。</returns>
    private static WorkbenchActionKitRoot CreateVisualRoot(int index)
    {
        IReadOnlyList<WorkbenchActionKitNode> children = index == 0
            ? new[] { CreateDeepNode(1, 10) }
            : new[]
            {
                new WorkbenchActionKitNode(
                    $"{index}-leaf", "Delay", "Started", false, false,
                    $"Delay({index + 1}.0s)", Array.Empty<WorkbenchActionKitNode>())
            };
        IReadOnlyList<WorkbenchActionKitStackFrame> stack = index == 0
            ? new[]
            {
                new WorkbenchActionKitStackFrame("ActionDemo.Start", "ActionDemo.cs", 42),
                new WorkbenchActionKitStackFrame("GameLoop.Initialize", "GameLoop.cs", 18)
            }
            : Array.Empty<WorkbenchActionKitStackFrame>();
        string type = index % 3 == 0 ? "Sequence" : (index % 3 == 1 ? "Parallel" : "Repeat");
        return new WorkbenchActionKitRoot(
            $"root-{index:00}", type, "Started", false, false,
            $"{type}({children.Count}, index=0)",
            index % 2 == 0 ? "ScaledDeltaTime" : "UnscaledDeltaTime",
            false,
            stack,
            children,
            children.Count,
            0);
    }

    /// <summary>递归创建交替使用 Parallel、Repeat 与 Sequence 的单分支视觉节点。</summary>
    /// <param name="depth">当前绝对深度。</param>
    /// <param name="maximumDepth">递归终止深度。</param>
    /// <returns>具备稳定 ID 和活动索引的组合节点。</returns>
    private static WorkbenchActionKitNode CreateDeepNode(int depth, int maximumDepth)
    {
        string type = (depth % 3) switch
        {
            1 => "Parallel",
            2 => "Repeat",
            _ => "Sequence"
        };
        IReadOnlyList<WorkbenchActionKitNode> children = depth < maximumDepth
            ? new[] { CreateDeepNode(depth + 1, maximumDepth) }
            : Array.Empty<WorkbenchActionKitNode>();
        return new WorkbenchActionKitNode(
            $"deep-{depth:00}",
            type,
            "Started",
            depth == 5,
            false,
            $"{type}({Math.Max(children.Count, 1)}, index=0)",
            children,
            children.Count,
            children.Count > 0 ? 0 : -1);
    }

    /// <summary>通过反射创建 Application 内部 ActionKit 数据源。</summary>
    /// <param name="arguments">内部构造方法参数。</param>
    /// <returns>可传入 ActionKit 状态构造方法的数据源。</returns>
    private static object CreateInternalDataSource(params object[] arguments)
    {
        Type type = typeof(WorkbenchActionKitState).Assembly.GetType(
            "YokiFrame.Tooling.Application.Models.ActionKit.WorkbenchActionKitDataSource", true)!;
        object? instance = Activator.CreateInstance(
            type,
            System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic,
            null,
            arguments,
            null);
        return Assert.IsAssignableFrom<object>(instance);
    }

    /// <summary>调用 Application 模型的内部构造方法。</summary>
    /// <typeparam name="T">目标强类型模型。</typeparam>
    /// <param name="arguments">内部构造方法参数。</param>
    /// <returns>构造完成的强类型模型。</returns>
    private static T CreateInternal<T>(params object[] arguments)
    {
        object? instance = Activator.CreateInstance(
            typeof(T),
            System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic,
            null,
            arguments,
            null);
        return Assert.IsType<T>(instance);
    }

    /// <summary>保存 Headless 渲染帧，供 ActionKit 两种目标窗口尺寸人工复核。</summary>
    /// <param name="window">已经完成布局的 Workbench 窗口。</param>
    /// <param name="fileName">目标截图文件名。</param>
    private static void SaveFrame(Window window, string fileName)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        string outputDirectory = Path.Combine(
            FindWorkbenchRoot(), ".artifacts", "screenshots", "workbench");
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, fileName);
        using FileStream stream = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        frame.Save(stream);
        Assert.True(stream.Length > 1024, "ActionKit Headless 截图内容为空或异常小。");
    }

    /// <summary>从测试输出目录向上定位 Workbench 源码根。</summary>
    /// <returns>YokiFrameWorkbench~ 绝对路径。</returns>
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

        throw new DirectoryNotFoundException("无法定位 YokiFrameWorkbench~ 源码根。");
    }
}
