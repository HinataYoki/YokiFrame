using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YokiFrame.Tooling.Application.Models.ActionKit;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.ViewModels.ActionKit;
using YokiFrame.Workbench.Avalonia.Views.Pages;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>覆盖 ActionKit 稳定选择、XAML 契约和 Headless 渲染。</summary>
public sealed partial class ActionKitPageViewModelTests
{
    /// <summary>验证等价宿主刷新按字符串 ID 保持根选择。</summary>
    [Fact]
    public void PeriodicRefreshPreservesSelectedRootId()
    {
        ActionKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(1L, 2));
        viewModel.SelectedRoot = viewModel.Roots[1];

        viewModel.ApplyPeriodicState(CreateState(2L, 3));

        Assert.Equal("9007199254740993", viewModel.SelectedRoot?.ActionId);
        Assert.Equal(3, viewModel.Events.Count);
        Assert.Equal("Sequence", viewModel.SelectedActionType);
    }

    /// <summary>验证相同 identity/version 的周期状态不会重建树和事件对象。</summary>
    [Fact]
    public void EqualVersionRefreshReusesEntireProjection()
    {
        ActionKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(1L, 2));
        ActionKitRootViewModel root = viewModel.Roots[0];
        ActionKitNodeViewModel child = root.Children[0];
        ActionKitEventListItemViewModel terminalEvent = viewModel.Events[0];

        viewModel.ApplyPeriodicState(CreateState(1L, 2));

        Assert.Same(root, viewModel.Roots[0]);
        Assert.Same(child, viewModel.Roots[0].Children[0]);
        Assert.Same(terminalEvent, viewModel.Events[0]);
    }

    /// <summary>验证版本变化时按稳定键复用对象、更新字段并保留节点选择。</summary>
    [Fact]
    public void ChangedVersionUpdatesReusedProjectionInPlace()
    {
        ActionKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(1L, 2));
        ActionKitRootViewModel root = viewModel.Roots[0];
        ActionKitNodeViewModel child = root.Children[0];
        ActionKitEventListItemViewModel terminalEvent = viewModel.Events[0];
        viewModel.SelectedRoot = root;
        viewModel.SelectedNode = child;

        viewModel.ApplyPeriodicState(CreateState(
            2L,
            2,
            rootStatus: "Paused",
            childDebugInfo: "Delay(2s)"));

        Assert.Same(root, viewModel.Roots[0]);
        Assert.Same(child, viewModel.Roots[0].Children[0]);
        Assert.Same(child, viewModel.SelectedNode);
        Assert.Same(terminalEvent, viewModel.Events[0]);
        Assert.Equal("Paused", root.Status);
        Assert.Equal("Delay(2s)", child.DebugInfo);
    }

    /// <summary>验证切换宿主后，旧命令响应不会覆盖新页面身份与状态。</summary>
    [Fact]
    public async Task CommandResultFromPreviousHostIdentityIsIgnored()
    {
        TaskCompletionSource<WorkbenchActionKitState> response = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ActionKitPageViewModel viewModel = new((_, _, _) => response.Task, null);
        viewModel.ApplyPeriodicState(CreateState(1L, 1));
        Task command = viewModel.ToggleStackTraceCommand.ExecuteAsync();
        viewModel.ApplyPeriodicState(CreateState(
            10L, 1, engineId: "godot-runtime", sessionId: "session-b", generation: 2L));

        response.SetResult(CreateState(2L, 1, stackTraceEnabled: false));
        await command;

        Assert.Equal(250L, viewModel.FrameCount);
        Assert.True(viewModel.StackTraceEnabled);
        Assert.Equal(string.Empty, viewModel.OperationStatusText);
    }

    /// <summary>验证同宿主较新的周期状态不会被较旧命令响应回退。</summary>
    [Fact]
    public async Task OlderCommandResultCannotOverwriteNewerSameHostState()
    {
        TaskCompletionSource<WorkbenchActionKitState> response = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ActionKitPageViewModel viewModel = new((_, _, _) => response.Task, null);
        viewModel.ApplyPeriodicState(CreateState(1L, 1));
        Task command = viewModel.ToggleStackTraceCommand.ExecuteAsync();
        viewModel.ApplyPeriodicState(CreateState(10L, 1));

        response.SetResult(CreateState(2L, 1, stackTraceEnabled: false));
        await command;

        Assert.Equal(250L, viewModel.FrameCount);
        Assert.True(viewModel.StackTraceEnabled);
        Assert.Equal(string.Empty, viewModel.OperationStatusText);
    }

    /// <summary>验证页面使用树优先响应式布局、诊断抽屉和显式 Application 命令。</summary>
    [Fact]
    public void PageContractUsesTreeAndExplicitStackCommands()
    {
        string xaml = WorkbenchContractTestFiles.ReadSource("Views", "Pages", "ActionKitPageView.axaml");
        string inspector = WorkbenchContractTestFiles.ReadSource(
            "Views", "Pages", "ActionKit", "ActionKitNodeInspectorView.axaml");
        string shell = WorkbenchContractTestFiles.ReadSource("Views", "WorkbenchShellView.axaml");
        string styles = WorkbenchContractTestFiles.ReadSource("Styles", "ActionKit.axaml");

        Assert.Contains("ActionWorkspace", xaml, StringComparison.Ordinal);
        Assert.Contains("InspectorPanel", xaml, StringComparison.Ordinal);
        Assert.Contains("DrawerTabs", xaml, StringComparison.Ordinal);
        Assert.Contains("CompactInspectorTab", xaml, StringComparison.Ordinal);
        Assert.Contains("TreeDataTemplate", xaml, StringComparison.Ordinal);
        Assert.Contains("ActionKitTreeViewItemTheme", xaml, StringComparison.Ordinal);
        Assert.Contains("FilteredRoots", xaml, StringComparison.Ordinal);
        Assert.Contains("actionkit-type-badge sequence", xaml, StringComparison.Ordinal);
        Assert.Contains("ActionKitNodeInspectorView", xaml, StringComparison.Ordinal);
        Assert.Contains("节点详情", inspector, StringComparison.Ordinal);
        Assert.DoesNotContain("actionkit-summary-strip", xaml, StringComparison.Ordinal);
        Assert.Contains("ActionKitPage.ToggleStackTraceCommand", shell, StringComparison.Ordinal);
        Assert.Contains("ActionKitPage.ClearStackTraceCommand", shell, StringComparison.Ordinal);
        Assert.Contains("清空历史", shell, StringComparison.Ordinal);
        Assert.Contains("不影响最近终态记录", shell, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", shell, StringComparison.Ordinal);
        Assert.Contains("从左侧选择一个活动根以检查执行流程", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedFlowNodes", xaml, StringComparison.Ordinal);
        Assert.Contains("ActionKitTreeIndentConverter", styles, StringComparison.Ordinal);
        Assert.Contains("IsExpanded\" Value=\"True", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize=\"10\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize=\"11\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Viewbox", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LayoutTransform", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ActionKitPageView", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Value=\"#", styles, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>验证深层递归树保留真实深度、组合边界和活动执行路径。</summary>
    [Fact]
    public void DeepTreeProjectsCappedIndentMetadataWithoutFlatteningHierarchy()
    {
        ActionKitRootViewModel root = new(CreateDeepRoot(10));
        ActionKitNodeViewModel node = root;
        for (var depth = 1; depth <= 10; depth++)
        {
            node = Assert.Single(node.Children);
            Assert.Equal(depth, node.Depth);
        }

        Assert.True(node.IsDeepNode);
        Assert.Equal("L10", node.DepthBadgeText);
        Assert.True(node.IsInsideParallel);
        Assert.True(node.IsInsideRepeat);
        Assert.True(node.IsCurrentPath);
        Assert.Equal(11, CountRecursiveNodes(root));
    }

    /// <summary>验证叶根与无选择状态都显示动作树、调用帧的纯状态空态。</summary>
    [Fact]
    public void LeafRootExposesTreeAndStackEmptyStates()
    {
        ActionKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(1L, 0, includeChildren: false));

        Assert.True(viewModel.IsTreeEmpty);
        Assert.True(viewModel.IsStackTraceEmpty);

        viewModel.SelectedRoot = viewModel.Roots[1];

        Assert.True(viewModel.IsTreeEmpty);
        Assert.False(viewModel.IsStackTraceEmpty);
    }

    /// <summary>验证真实 ActionKit 页面非空渲染且指标变化改变像素。</summary>
    [Fact]
    public async Task PageRendersNonBlankAndUpdatesPixels()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ActionKitPageViewModel viewModel = new();
            viewModel.ApplyPeriodicState(CreateState(1L, 2));
            ActionKitPageView view = new() { DataContext = viewModel };
            Window window = new() { Width = 1420, Height = 820, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                string firstHash = CaptureFrameHash(window);
                viewModel.ApplyPeriodicState(CreateState(2L, 3));
                Dispatcher.UIThread.RunJobs();
                Assert.NotEqual(firstHash, CaptureFrameHash(window));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>验证页面在 Workbench 最小工作区内三栏不重叠，且明暗主题都能产生有效像素。</summary>
    [Fact]
    public async Task PageFitsMinimumWorkspaceInLightAndDarkThemes()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            string lightHash = RenderAndAssertLayout(ThemeVariant.Light);
            string darkHash = RenderAndAssertLayout(ThemeVariant.Dark);

            Assert.NotEqual(lightHash, darkHash);
        });
    }

    /// <summary>验证紧凑工作区把节点详情移入抽屉，宽屏才恢复常驻 Inspector。</summary>
    [Fact]
    public async Task PageSwitchesInspectorAtWorkspaceBreakpoint()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ActionKitPageViewModel viewModel = new();
            viewModel.ApplyPeriodicState(CreateState(1L, 2));
            ActionKitPageView view = new() { DataContext = viewModel };
            Window window = new() { Width = 1080, Height = 680, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Border inspector = view.FindControl<Border>("InspectorPanel")!;
                Assert.False(inspector.IsVisible);
                Assert.Contains("actionkit-compact", view.Classes);

                window.Width = 1420;
                Dispatcher.UIThread.RunJobs();
                Assert.True(inspector.IsVisible);
                Assert.Contains("actionkit-wide", view.Classes);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>创建包含两个根、子动作、调用帧和终态的强类型测试状态。</summary>
    private static WorkbenchActionKitState CreateState(
        long version,
        int eventCount,
        bool includeChildren = true,
        string rootStatus = "Started",
        string childDebugInfo = "Delay(1s)",
        string engineId = "unity-editor",
        string sessionId = "action-session",
        long generation = 9L,
        bool stackTraceEnabled = true,
        IReadOnlyList<WorkbenchActionKitRoot>? rootsOverride = null,
        IReadOnlyList<WorkbenchActionKitEvent>? eventsOverride = null)
    {
        IReadOnlyList<WorkbenchActionKitRoot> roots = rootsOverride
            ?? CreateDefaultRoots(includeChildren, rootStatus, childDebugInfo);
        IReadOnlyList<WorkbenchActionKitEvent> events = eventsOverride
            ?? CreateDefaultEvents(eventCount);
        object source = CreateInternalDataSource(
            engineId, sessionId, generation, "PlayMode",
            DateTimeOffset.Parse("2026-07-16T08:00:00Z"), "telemetry", string.Empty,
            new[] { "Global\\YokiFrame.ActionKit" }, string.Empty, "{}");
        return CreateInternal<WorkbenchActionKitState>(
            source,
            version,
            new WorkbenchActionKitStats(
                240 + version, roots.Count, 8, 2, 1, events.Count, stackTraceEnabled, 1),
            roots,
            events,
            roots.Count,
            events.Count,
            false,
            false,
            false,
            false,
            false);
    }

    /// <summary>创建包含 Sequence、Parallel 与 Repeat 的十层递归动作根。</summary>
    /// <param name="maximumDepth">最深子节点的绝对深度。</param>
    /// <returns>所有容器均指向唯一活动子节点的根动作。</returns>
    private static WorkbenchActionKitRoot CreateDeepRoot(int maximumDepth)
    {
        WorkbenchActionKitNode child = CreateDeepNode(1, maximumDepth);
        return new WorkbenchActionKitRoot(
            "root", "Sequence", "Started", false, false, "Sequence(1, index=0)",
            "ScaledDeltaTime", false, Array.Empty<WorkbenchActionKitStackFrame>(),
            new[] { child }, 1, 0);
    }

    /// <summary>递归创建交替使用 Parallel、Repeat 与 Sequence 的单分支测试节点。</summary>
    /// <param name="depth">当前节点绝对深度。</param>
    /// <param name="maximumDepth">递归终止深度。</param>
    /// <returns>保留真实父子结构的 Application 节点。</returns>
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
        int currentChildIndex = children.Count > 0 ? 0 : -1;
        return new WorkbenchActionKitNode(
            depth.ToString(), type, "Started", false, false, type + "(1, index=0)",
            children, children.Count, currentChildIndex);
    }

    /// <summary>递归统计节点总量，证明视图模型没有把动作树压平成列表。</summary>
    /// <param name="node">待统计子树根。</param>
    /// <returns>包含根节点自身的节点数量。</returns>
    private static int CountRecursiveNodes(ActionKitNodeViewModel node)
    {
        int count = 1;
        for (var index = 0; index < node.Children.Count; index++)
        {
            count += CountRecursiveNodes(node.Children[index]);
        }

        return count;
    }

    /// <summary>渲染指定主题并验证紧凑双栏和折叠抽屉保持稳定边界。</summary>
    private static string RenderAndAssertLayout(ThemeVariant themeVariant)
    {
        ActionKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(1L, 2));
        ActionKitPageView view = new() { DataContext = viewModel };
        Window window = new()
        {
            Width = 1080,
            Height = 680,
            RequestedThemeVariant = themeVariant,
            Content = view
        };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var roots = FindAutomationControl(view, "actionkit.roots-panel");
            var tree = FindAutomationControl(view, "actionkit.tree-panel");
            var drawer = FindAutomationControl(view, "actionkit.drawer");
            var rootsBounds = GetViewBounds(view, roots);
            var treeBounds = GetViewBounds(view, tree);
            var drawerBounds = GetViewBounds(view, drawer);
            Assert.True(rootsBounds.Right <= treeBounds.Left);
            Assert.True(treeBounds.Right <= view.Bounds.Width);
            Assert.True(drawerBounds.Bottom <= view.Bounds.Height);
            Assert.InRange(drawerBounds.Height, 31D, 33D);
            AssertNamedAutomationControl(view, "actionkit.roots");
            AssertNamedAutomationControl(view, "actionkit.tree");
            AssertNamedAutomationControl(view, "actionkit.drawer");
            return CaptureFrameHash(window);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>把控件局部边界转换到 ActionKit 页面坐标系。</summary>
    private static Rect GetViewBounds(ActionKitPageView view, Control control)
    {
        var point = control.TranslatePoint(default, view);
        Assert.NotNull(point);
        return new Rect(point.Value, control.Bounds.Size);
    }

    /// <summary>按稳定 AutomationId 查找页面内唯一控件。</summary>
    private static Control FindAutomationControl(ActionKitPageView view, string automationId)
    {
        return Assert.Single(view.GetVisualDescendants().OfType<Control>(), control =>
            string.Equals(
                AutomationProperties.GetAutomationId(control),
                automationId,
                StringComparison.Ordinal));
    }

    /// <summary>验证指定自动化控件具有可供屏幕阅读器朗读的名称。</summary>
    private static void AssertNamedAutomationControl(ActionKitPageView view, string automationId)
    {
        var control = FindAutomationControl(view, automationId);
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)));
    }

    /// <summary>通过反射创建 Application 内部 ActionKit 数据源。</summary>
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

    /// <summary>捕获 Headless 帧并计算稳定像素哈希。</summary>
    private static string CaptureFrameHash(Window window)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        using var framebuffer = frame.Lock();
        int byteCount = checked(framebuffer.RowBytes * framebuffer.Size.Height);
        byte[] pixels = new byte[byteCount];
        Marshal.Copy(framebuffer.Address, pixels, 0, byteCount);
        Assert.Contains(pixels, static value => value != 0);
        return Convert.ToHexString(SHA256.HashData(pixels));
    }
}
