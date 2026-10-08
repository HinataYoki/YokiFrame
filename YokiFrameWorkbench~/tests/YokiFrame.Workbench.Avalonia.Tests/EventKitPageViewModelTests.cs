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
    public void ApplyPeriodicState_高频刷新_保留选择和既有时间线身份()
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
    public void ApplyPeriodicState_已有搜索和通道筛选_刷新后继续生效()
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
    public void ApplyPeriodicState_指定键和通道clear_进入所选时间线()
    {
        var viewModel = new EventKitPageViewModel();
        viewModel.ApplyPeriodicState(CreateState(5L, 5L, 0, false, true));
        viewModel.SelectedEvent = viewModel.Events.Single(item => item.EventKey == "GameSignal.Ready");

        Assert.Equal(3, viewModel.SelectedActivities.Count);
        Assert.Equal(2, viewModel.SelectedActivities.Count(item => item.Kind == "clear"));
    }

    /// <summary>验证没有 Runtime 时静态发送、注册和注销关系仍独立可见。</summary>
    [Fact]
    public void SetPageActive_仅有静态关系_无Runtime仍可见()
    {
        var viewModel = CreateViewModel(CreateCodeScan());

        var relation = Assert.Single(viewModel.Events, static item => item.EventKey == "DamageEvent");
        Assert.False(relation.HasRuntime);
        Assert.True(relation.HasStaticRelation);
        Assert.Single(relation.Senders);
        Assert.Single(relation.Receivers);
        Assert.Single(relation.Unregisters);
    }

    /// <summary>验证高频 Runtime 帧保留关系行、选择和静态位置对象身份。</summary>
    [Fact]
    public void ApplyPeriodicState_高频Runtime帧_保留静态位置和选择身份()
    {
        var viewModel = CreateViewModel(CreateCodeScan());
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
    public void SearchText_静态路径命中_只保留对应事件()
    {
        var viewModel = CreateViewModel(CreateCodeScan());

        viewModel.SearchText = "Combat/Emitter";

        Assert.Equal("DamageEvent", Assert.Single(viewModel.Events).EventKey);
    }

    /// <summary>验证 Type、Enum 和泛型负载只缩短展示文本，完整身份保持不变。</summary>
    [Fact]
    public void EventKitEventListItemViewModel_带命名空间的键_只缩短展示文本()
    {
        var typeEvent = new WorkbenchEventKitEvent(
            "Type",
            "YokiFrameRuntimeSmoke.EventKitRuntimeSmokeController+DamageEvent",
            "Demo.Messages.Envelope<YokiFrameRuntimeSmoke.EventKitRuntimeSmokeController+DamageEvent>",
            1, 1L, "10:00:00.010", false);
        var enumEvent = new WorkbenchEventKitEvent(
            "Enum",
            "YokiFrameRuntimeSmoke.EventKitRuntimeSmokeController+SmokeSignal.Pulse",
            string.Empty,
            1, 2L, "10:00:00.020", false);

        EventKitEventListItemViewModel typeItem = new(typeEvent);
        EventKitEventListItemViewModel enumItem = new(enumEvent);
        var activity = new WorkbenchEventKitActivity(
            3L, "register", "Type", typeEvent.EventKey, typeEvent.PayloadType,
            "YokiFrameRuntimeSmoke.EventKitRuntimeSmokeController.OnDamagePrimary", "10:00:00.030");

        Assert.Equal("DamageEvent", typeItem.EventKeyDisplay);
        Assert.Equal("Envelope<DamageEvent>", typeItem.PayloadDisplay);
        Assert.Equal("SmokeSignal.Pulse", enumItem.EventKeyDisplay);
        Assert.Equal("OnDamagePrimary", activity.Detail);
        Assert.Equal(
            "Type::" + typeEvent.EventKey + "::" + typeEvent.PayloadType,
            typeItem.Identity);
    }

    /// <summary>验证同一文件的多个调用点合并为一个文件组，并保留逐行打开入口。</summary>
    [Fact]
    public void EventKitEventListItemViewModel_同一文件多行_合并为一组并保留行号()
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
    public void CreateViewModel_混合通道扫描_按EnumTypeString排列()
    {
        WorkbenchEventKitCodeLocation[] empty = Array.Empty<WorkbenchEventKitCodeLocation>();
        var scan = new WorkbenchEventKitCodeScan(
            "C:/Project", true, 1, 1, TimeSpan.Zero,
            new[]
            {
                new WorkbenchEventKitCodeRelation("String", "legacy.message", "System.Int32", empty, empty, empty),
                new WorkbenchEventKitCodeRelation("Type", "Demo.DamageEvent", "Demo.DamageEvent", empty, empty, empty),
                new WorkbenchEventKitCodeRelation("Enum", "Demo.Signal.Ready", string.Empty, empty, empty, empty)
            });

        var viewModel = CreateViewModel(scan);

        Assert.Equal(new[] { "Enum", "Type", "String" }, viewModel.Events.Select(static item => item.Channel));
        Assert.Equal(new[] { "全部", "Enum", "Type", "String" }, viewModel.ChannelOptions);
    }

    /// <summary>验证真实 EventKit 页面非空渲染，等价帧像素稳定且监听变化可见。</summary>
    [Fact]
    public async Task EventKitPageView_等价帧_像素稳定且监听变化可见()
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
        WorkbenchEventKitDataSource dataSource = new WorkbenchEventKitDataSource(
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
            new WorkbenchEventKitEvent("Type", "DamageEvent", "DamageEvent", 1, 1L, "10:00:00.010", false),
            new WorkbenchEventKitEvent("Enum", "GameSignal.Ready", string.Empty, enumHandlerCount, sequence, "10:00:00.020", false)
        };
        var activities = new List<WorkbenchEventKitActivity>
        {
            new WorkbenchEventKitActivity(2L, "send", "Enum", "GameSignal.Ready", string.Empty, string.Empty, "10:00:00.020")
        };
        if (includeThirdActivity)
        {
            activities.Add(new WorkbenchEventKitActivity(3L, "register", "Enum", "GameSignal.Ready", string.Empty, "Demo.OnReady", "10:00:00.030"));
        }

        if (includeClearActivities)
        {
            activities.Add(new WorkbenchEventKitActivity(4L, "clear", "Enum", "GameSignal.Ready", string.Empty, string.Empty, "10:00:00.040"));
            activities.Add(new WorkbenchEventKitActivity(5L, "clear", "Enum", "*", string.Empty, string.Empty, "10:00:00.050"));
        }

        return new WorkbenchEventKitState(
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

    /// <summary>通过已有 internal 扫描委托和页面激活入口提交静态扫描。</summary>
    /// <param name="scan">待投影的静态扫描结果。</param>
    /// <returns>已合并扫描关系的页面视图模型。</returns>
    private static EventKitPageViewModel CreateViewModel(WorkbenchEventKitCodeScan scan)
    {
        EventKitPageViewModel viewModel = new((_, _) => Task.FromResult(scan), null);
        viewModel.SetProjectRoot(scan.ProjectRoot);
        viewModel.SetPageActive(true);
        return viewModel;
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
