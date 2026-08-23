using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using YokiFrame.Tooling.Application.Models.EventKit;
using YokiFrame.Tooling.Application.Models.EventKit.Scan;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.ViewModels.EventKit;
using YokiFrame.Workbench.Avalonia.Views.Pages;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 EventKit 页面高频刷新时的选择、对象身份和筛选稳定性。
/// </summary>
public sealed class EventKitPageViewModelTests
{
    /// <summary>验证新帧只更新稳定列表项，不重建当前选择和既有时间线记录。</summary>
    [Fact]
    public void HighFrequencyFramesPreserveSelectionAndExistingActivityIdentity()
    {
        var viewModel = new EventKitPageViewModel();
        viewModel.ApplyPeriodicState(CreateState(2L, 2L, 1, false));
        viewModel.SelectedEvent = viewModel.Events.Single(item => item.EventKey == "GameSignal.Ready");
        var selectedReference = viewModel.SelectedEvent;
        var existingActivity = Assert.Single(viewModel.SelectedActivities);

        viewModel.ApplyPeriodicState(CreateState(3L, 3L, 2, true));

        Assert.Same(selectedReference, viewModel.SelectedEvent);
        Assert.Equal(2, viewModel.SelectedEvent!.HandlerCount);
        Assert.Equal(2, viewModel.SelectedActivities.Count);
        Assert.Contains(viewModel.SelectedActivities, item => ReferenceEquals(item, existingActivity));
        Assert.Equal("Shared Memory", viewModel.DataChannelText);
    }

    /// <summary>验证搜索和通道筛选在状态刷新后继续生效。</summary>
    [Fact]
    public void SearchAndChannelFilterRemainActiveAcrossFrames()
    {
        var viewModel = new EventKitPageViewModel
        {
            SearchText = "ready",
            SelectedChannel = "Enum"
        };

        viewModel.ApplyPeriodicState(CreateState(1L, 1L, 1, false));
        Assert.Equal("GameSignal.Ready", Assert.Single(viewModel.Events).EventKey);

        viewModel.ApplyPeriodicState(CreateState(2L, 2L, 2, true));
        Assert.Equal("GameSignal.Ready", Assert.Single(viewModel.Events).EventKey);
        Assert.Equal("ready", viewModel.SearchText);
        Assert.Equal("Enum", viewModel.SelectedChannel);
    }

    /// <summary>验证指定键和通道级 clear 都会进入带 payload 事件的所选时间线。</summary>
    [Fact]
    public void TypedEventTimelineIncludesSpecificAndChannelClear()
    {
        var viewModel = new EventKitPageViewModel();
        viewModel.ApplyPeriodicState(CreateState(5L, 5L, 0, false, true));
        viewModel.SelectedEvent = viewModel.Events.Single(item => item.EventKey == "GameSignal.Ready");

        Assert.Equal(3, viewModel.SelectedActivities.Count);
        Assert.Equal(2, viewModel.SelectedActivities.Count(item => item.Kind == "clear"));
    }

    /// <summary>验证没有 Runtime 时静态发送、注册和注销关系仍独立可见。</summary>
    [Fact]
    public void StaticOnlyRelationRemainsVisibleWithoutRuntime()
    {
        var viewModel = new EventKitPageViewModel();

        ApplyCodeScan(viewModel, CreateCodeScan());

        var relation = Assert.Single(viewModel.Events, static item => item.EventKey == "DamageEvent");
        Assert.False(relation.HasRuntime);
        Assert.True(relation.HasStaticRelation);
        Assert.Single(relation.Senders);
        Assert.Single(relation.Receivers);
        Assert.Single(relation.Unregisters);
    }

    /// <summary>验证高频 Runtime 帧保留关系行、选择和静态位置对象身份。</summary>
    [Fact]
    public void RuntimeRefreshPreservesStaticLocationAndSelectionIdentity()
    {
        var viewModel = new EventKitPageViewModel();
        ApplyCodeScan(viewModel, CreateCodeScan());
        viewModel.ApplyPeriodicState(CreateState(2L, 2L, 1, false));
        viewModel.SelectedEvent = viewModel.Events.Single(static item => item.EventKey == "DamageEvent");
        var relation = viewModel.SelectedEvent;
        var sender = Assert.Single(relation!.Senders);

        viewModel.ApplyPeriodicState(CreateState(3L, 3L, 2, true));

        Assert.Same(relation, viewModel.SelectedEvent);
        Assert.Same(sender, Assert.Single(viewModel.SelectedEvent!.Senders));
    }

    /// <summary>验证搜索同时匹配静态源码相对路径。</summary>
    [Fact]
    public void SearchMatchesStaticSourcePath()
    {
        var viewModel = new EventKitPageViewModel();
        ApplyCodeScan(viewModel, CreateCodeScan());

        viewModel.SearchText = "Combat/Emitter";

        Assert.Equal("DamageEvent", Assert.Single(viewModel.Events).EventKey);
    }

    /// <summary>验证 Type、Enum 和泛型负载只缩短展示文本，完整身份保持不变。</summary>
    [Fact]
    public void EventDisplayNamesRemoveNamespaceAndDeclaringTypes()
    {
        var typeEvent = CreateInternal<WorkbenchEventKitEvent>(
            "Type",
            "YokiFrameRuntimeSmoke.EventKitRuntimeSmokeController+DamageEvent",
            "Demo.Messages.Envelope<YokiFrameRuntimeSmoke.EventKitRuntimeSmokeController+DamageEvent>",
            1, 1L, "10:00:00.010", false);
        var enumEvent = CreateInternal<WorkbenchEventKitEvent>(
            "Enum",
            "YokiFrameRuntimeSmoke.EventKitRuntimeSmokeController+SmokeSignal.Pulse",
            string.Empty,
            1, 2L, "10:00:00.020", false);

        EventKitEventListItemViewModel typeItem = new(typeEvent);
        EventKitEventListItemViewModel enumItem = new(enumEvent);
        var activity = CreateInternal<WorkbenchEventKitActivity>(
            3L, "register", "Type", typeEvent.EventKey, typeEvent.PayloadType,
            "YokiFrameRuntimeSmoke.EventKitRuntimeSmokeController.OnDamagePrimary", "10:00:00.030");

        Assert.Equal("DamageEvent", typeItem.EventKeyDisplay);
        Assert.Equal("Envelope<DamageEvent>", typeItem.PayloadDisplay);
        Assert.Equal("SmokeSignal.Pulse", enumItem.EventKeyDisplay);
        Assert.Equal("OnDamagePrimary", activity.Detail);
        Assert.Contains("YokiFrameRuntimeSmoke", typeItem.Identity, StringComparison.Ordinal);
    }

    /// <summary>验证同一文件的多个调用点合并为一个文件组，并保留逐行打开入口。</summary>
    [Fact]
    public void RelationRowsGroupLocationsFromTheSameFile()
    {
        WorkbenchEventKitCodeLocation[] locations = Enumerable.Range(1, 5)
            .Select(static line => new WorkbenchEventKitCodeLocation("Assets/Combat/DamageFlow.cs", line))
            .ToArray();
        var relation = new WorkbenchEventKitCodeRelation(
            "Type", "Demo.DamageEvent", "Demo.DamageEvent", locations, locations, locations);

        EventKitEventListItemViewModel item = new(relation);

        Assert.Equal(5, item.Senders.Count);
        Assert.Equal(5, Assert.Single(item.VisibleSenderGroups).Locations.Count);
        Assert.Equal(5, Assert.Single(item.VisibleReceiverGroups).Locations.Count);
        Assert.Equal(5, Assert.Single(item.VisibleUnregisterGroups).Locations.Count);
        Assert.False(item.HasSenderOverflow);
        Assert.Equal("发送与注册均存在", item.FlowCoverageText);
        Assert.Equal("注册/注销数量平衡", item.LifetimeBalanceText);
    }

    /// <summary>验证事件列表和筛选项都按 Enum、Type、String 排列。</summary>
    [Fact]
    public void EventChannelsAreOrderedEnumTypeString()
    {
        var viewModel = new EventKitPageViewModel();
        WorkbenchEventKitCodeLocation[] empty = Array.Empty<WorkbenchEventKitCodeLocation>();
        var scan = new WorkbenchEventKitCodeScan(
            "C:/Project", true, 1, 1, TimeSpan.Zero,
            new[]
            {
                new WorkbenchEventKitCodeRelation("String", "legacy.message", "System.Int32", empty, empty, empty),
                new WorkbenchEventKitCodeRelation("Type", "Demo.DamageEvent", "Demo.DamageEvent", empty, empty, empty),
                new WorkbenchEventKitCodeRelation("Enum", "Demo.Signal.Ready", string.Empty, empty, empty, empty)
            });

        ApplyCodeScan(viewModel, scan);

        Assert.Equal(new[] { "Enum", "Type", "String" }, viewModel.Events.Select(static item => item.Channel));
        Assert.Equal(new[] { "全部", "Enum", "Type", "String" }, viewModel.ChannelOptions);
    }

    /// <summary>验证 XAML 使用行式关系卡、弹性主从列、完整详情和真实字号。</summary>
    [Fact]
    public void PageContractUsesCompactControlsAndRelations()
    {
        string xaml = File.ReadAllText(FindRepositoryFile(
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Views",
            "Pages",
            "EventKitPageView.axaml"));
        string styles = File.ReadAllText(FindRepositoryFile(
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Styles",
            "EventKit.axaml"));
        string colors = File.ReadAllText(FindRepositoryFile(
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Resources",
            "Colors.axaml"));
        string shell = File.ReadAllText(FindRepositoryFile(
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Views",
            "WorkbenchShellView.axaml"));

        Assert.True(xaml.Contains("发送方代码") || xaml.Contains("String.EventKit.SendersCode"), "EventKit 页面应包含发送方代码词条");
        Assert.True(xaml.Contains("注册 / 注销方") || xaml.Contains("String.EventKit.Subscribers"), "EventKit 页面应包含注册/注销方词条");
        Assert.True(xaml.Contains("运行时间线") || xaml.Contains("String.EventKit.RuntimeTimeline"), "EventKit 页面应包含运行时间线词条");
        Assert.True(xaml.Contains("代码位置") || xaml.Contains("String.EventKit.CodeLocations"), "EventKit 页面应包含代码位置词条");
        Assert.Contains("<Button.Flyout>", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("workbench.eventkit.search", xaml, StringComparison.Ordinal);
        Assert.Contains("SenderPreview", xaml, StringComparison.Ordinal);
        Assert.Contains("ReceiverPreview", xaml, StringComparison.Ordinal);
        Assert.Contains("UnregisterPreview", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedEvent.SenderGroups", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedEvent.ReceiverGroups", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedEvent.UnregisterGroups", xaml, StringComparison.Ordinal);
        Assert.Contains("EventKitCodeGroupTemplate", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("EventKitCodePreviewTemplate", xaml, StringComparison.Ordinal);
        Assert.Contains("EventKeyDisplay", xaml, StringComparison.Ordinal);
        Assert.Contains("PayloadSummaryText", xaml, StringComparison.Ordinal);
        Assert.Contains("FlowCoverageText", xaml, StringComparison.Ordinal);
        Assert.Contains("LifetimeBalanceText", xaml, StringComparison.Ordinal);
        Assert.Contains("RelationCountText", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("LastActivityText", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("eventkit-runtime-badge", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{CompiledBinding HandlerCountText}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("静态关系与运行时活动", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("eventkit-stat", xaml, StringComparison.Ordinal);
        Assert.Contains("workbench.eventkit.search", shell, StringComparison.Ordinal);
        Assert.Contains("EventKitPage.ChannelOptions", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("workbench.eventkit.scan", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("扫描代码", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("扫描代码", xaml, StringComparison.Ordinal);
        // “排除 Editor”词条已迁移为 shell 中的 DynamicResource 资源 key，兼容两种形态。
        Assert.True(shell.Contains("排除 Editor") || shell.Contains("String.EventKit.ExcludeEditor"), "Shell 应包含排除 Editor 词条");
        Assert.DoesNotContain("EventKitPage.DataChannelText", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{CompiledBinding IsType}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Classes.type=\"{CompiledBinding SelectedIsType}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedPayloadSummaryText", xaml, StringComparison.Ordinal);
        Assert.Contains("<ColumnDefinition Width=\"10*\" MinWidth=\"560\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("<ColumnDefinition Width=\"3*\" MinWidth=\"330\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("<GridSplitter Grid.Column=\"1\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"3*,14,4*,14,3*\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid MinHeight=\"88\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ScrollViewer.HorizontalScrollBarVisibility=\"Disabled\"", xaml, StringComparison.Ordinal);
        Assert.True(xaml.Contains("注册 / 注销方") || xaml.Contains("String.EventKit.Subscribers"), "应包含注册/注销方");
        Assert.True(xaml.Contains("Text=\"注册\"") || xaml.Contains("String.EventKit.Register"), "应包含注册");
        Assert.True(xaml.Contains("Text=\"注销\"") || xaml.Contains("String.EventKit.Unregister"), "应包含注销");
        Assert.True(xaml.Contains("未扫描到注销方") || xaml.Contains("String.EventKit.NoUnregistersScanned"), "应包含未扫描到注销方");
        Assert.Contains("eventkit-endpoint sender", xaml, StringComparison.Ordinal);
        Assert.Contains("eventkit-endpoint receiver", xaml, StringComparison.Ordinal);
        Assert.Contains("eventkit-flow-connector", xaml, StringComparison.Ordinal);
        Assert.Contains("Classes.type=\"{CompiledBinding IsType}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("HasMissingUnregister", xaml, StringComparison.Ordinal);
        Assert.Contains("StringFormat='+ L{0}'", xaml, StringComparison.Ordinal);
        Assert.Contains("StringFormat='- L{0}'", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Classes=\"eventkit-code-relations\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("MinWidth=\"720\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize=\"9\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize=\"10\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Viewbox", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ScaleTransform", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Border Classes=\"panel\" Padding=\"0\" MinWidth=\"720\">", xaml, StringComparison.Ordinal);
        Assert.Contains("ListBox.eventkit-relation-list ListBoxItem:selected", styles, StringComparison.Ordinal);
        Assert.Contains("ListBox.eventkit-relation-list ListBoxItem:selected /template/ ContentPresenter", styles, StringComparison.Ordinal);
        Assert.Contains("MinHeight\" Value=\"92", styles, StringComparison.Ordinal);
        Assert.Contains("Button.eventkit-code-summary", styles, StringComparison.Ordinal);
        Assert.Contains("Border.eventkit-event-hub.type", styles, StringComparison.Ordinal);
        Assert.Contains("Brush.EventKit.Type.Surface", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("Border.eventkit-code-relations", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("#", styles, StringComparison.Ordinal);
        Assert.Contains("Brush.EventKit.Type.Surface", colors, StringComparison.Ordinal);
        Assert.Contains("Brush.EventKit.Enum.Surface", colors, StringComparison.Ordinal);
        Assert.Contains("Brush.EventKit.String.Surface", colors, StringComparison.Ordinal);
    }

    /// <summary>验证真实 EventKit 页面非空渲染，等价帧像素稳定且监听变化可见。</summary>
    [Fact]
    public async Task PageRendersStablePixelsForEquivalentFrames()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var viewModel = new EventKitPageViewModel();
            viewModel.ApplyPeriodicState(CreateState(2L, 2L, 1, false));
            EventKitPageView view = new() { DataContext = viewModel };
            Window window = new() { Width = 1200, Height = 760, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                string firstHash = CaptureFrameHash(window);
                viewModel.ApplyPeriodicState(CreateState(2L, 2L, 1, false));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(firstHash, CaptureFrameHash(window));

                viewModel.ApplyPeriodicState(CreateState(3L, 3L, 2, true));
                Dispatcher.UIThread.RunJobs();
                Assert.NotEqual(firstHash, CaptureFrameHash(window));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>创建包含 Type/Enum 事件和确定活动 sequence 的强类型测试状态。</summary>
    private static WorkbenchEventKitState CreateState(
        long version,
        long sequence,
        int enumHandlerCount,
        bool includeThirdActivity,
        bool includeClearActivities = false)
    {
        object dataSource = CreateInternalDataSource(
            "unity-editor",
            "event-session",
            5L,
            "PlayMode",
            DateTimeOffset.Parse("2026-07-15T08:00:00Z"),
            "telemetry",
            new[] { "Global\\YokiFrame.EventKit" },
            string.Empty,
            "{}");
        WorkbenchEventKitEvent[] events =
        {
            CreateInternal<WorkbenchEventKitEvent>("Type", "DamageEvent", "DamageEvent", 1, 1L, "10:00:00.010", false),
            CreateInternal<WorkbenchEventKitEvent>("Enum", "GameSignal.Ready", string.Empty, enumHandlerCount, sequence, "10:00:00.020", false)
        };
        var activities = new List<WorkbenchEventKitActivity>
        {
            CreateInternal<WorkbenchEventKitActivity>(2L, "send", "Enum", "GameSignal.Ready", string.Empty, string.Empty, "10:00:00.020")
        };
        if (includeThirdActivity)
        {
            activities.Add(CreateInternal<WorkbenchEventKitActivity>(3L, "register", "Enum", "GameSignal.Ready", string.Empty, "Demo.OnReady", "10:00:00.030"));
        }

        if (includeClearActivities)
        {
            activities.Add(CreateInternal<WorkbenchEventKitActivity>(4L, "clear", "Enum", "GameSignal.Ready", string.Empty, string.Empty, "10:00:00.040"));
            activities.Add(CreateInternal<WorkbenchEventKitActivity>(5L, "clear", "Enum", "*", string.Empty, string.Empty, "10:00:00.050"));
        }

        return CreateInternal<WorkbenchEventKitState>(
            dataSource,
            version,
            sequence,
            1,
            1,
            0,
            2,
            1 + enumHandlerCount,
            activities.Count,
            events,
            activities.ToArray());
    }

    /// <summary>创建包含完整三类源码位置的静态扫描结果。</summary>
    private static WorkbenchEventKitCodeScan CreateCodeScan()
    {
        return CreateCodeScan("DamageEvent");
    }

    /// <summary>创建带指定 Type 事件身份的完整三类源码位置扫描结果。</summary>
    private static WorkbenchEventKitCodeScan CreateCodeScan(string eventKey)
    {
        var relation = new WorkbenchEventKitCodeRelation(
            "Type",
            eventKey,
            eventKey,
            new[] { new WorkbenchEventKitCodeLocation("Assets/Combat/Emitter.cs", 18) },
            new[] { new WorkbenchEventKitCodeLocation("Assets/Combat/Receiver.cs", 32) },
            new[] { new WorkbenchEventKitCodeLocation("Assets/Combat/Receiver.cs", 47) });
        return new WorkbenchEventKitCodeScan(
            "C:/Project",
            true,
            10,
            2,
            TimeSpan.FromMilliseconds(12),
            new[] { relation });
    }

    /// <summary>通过反射提交 ViewModel 私有静态扫描边界。</summary>
    private static void ApplyCodeScan(EventKitPageViewModel viewModel, WorkbenchEventKitCodeScan scan)
    {
        var method = typeof(EventKitPageViewModel).GetMethod(
            "ApplyCodeScan",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(viewModel, new object[] { scan });
    }

    /// <summary>通过反射创建 Application 内部 EventKit 数据源。</summary>
    private static object CreateInternalDataSource(params object[] arguments)
    {
        Type dataSourceType = typeof(WorkbenchEventKitState).Assembly.GetType(
            "YokiFrame.Tooling.Application.Models.EventKit.WorkbenchEventKitDataSource",
            true)!;
        object? instance = Activator.CreateInstance(
            dataSourceType,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            arguments,
            null);
        Assert.NotNull(instance);
        return instance;
    }

    /// <summary>调用 Application 模型的内部构造方法。</summary>
    private static T CreateInternal<T>(params object[] arguments)
    {
        object? instance = Activator.CreateInstance(
            typeof(T),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            arguments,
            null);
        return Assert.IsType<T>(instance);
    }

    /// <summary>从测试输出目录向上定位 Workbench 源文件。</summary>
    private static string FindRepositoryFile(params string[] relativeSegments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeSegments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate EventKit source file.");
    }

    /// <summary>捕获完整 Headless 帧并计算稳定像素哈希。</summary>
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
