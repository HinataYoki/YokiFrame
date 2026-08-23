using System.Reflection;
using YokiFrame.Tooling.Application.Models.PoolKit;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.ViewModels.PoolKit;
using Xunit;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 PoolKit 页面与列表行的动态文案会随语言切换刷新。</summary>
public sealed class PoolKitI18nTests
{
    /// <summary>语言切换应刷新占位来源、按钮文本、告警模板和既有列表行。</summary>
    /// <remarks>整个测试体必须在 UI 线程执行：SetCulture 只有在 UI 线程上才会同步触发
    /// CultureChanged，否则重投影被 Post 到无人泵的 Dispatcher 队列。</remarks>
    [Fact]
    public async Task PoolPage_ReprojectsDynamicTextsOnCultureChange()
    {
        // 单独运行时也必须先初始化 Headless UI 线程，否则 Dispatcher 无人泵导致挂起。
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            WorkbenchI18nService service = WorkbenchI18nService.Instance;
            service.SetCulture("zh-CN");
            PoolKitPageViewModel viewModel = new();
            try
            {
                // 未连接 Runtime 时显示通用等待占位。
                Assert.Equal("等待数据", viewModel.Source);
                Assert.Equal("启用跟踪", viewModel.TrackingButtonText);
                Assert.Equal("跟踪 关", viewModel.TrackingStatusText);

                viewModel.ApplyPeriodicState(CreateState());
                Assert.True(viewModel.HasLeakWarning);
                Assert.Contains("本次检查发现 1 个仍有借出对象的候选池", viewModel.LeakWarningText);
                Assert.NotNull(viewModel.SelectedPool);
                Assert.Equal("刚借出", viewModel.SelectedPool!.RecentEventText);

                service.SetCulture("en-US");

                // 已连接后的来源文本来自 payload，本地化不覆盖 Runtime 数据。
                Assert.Equal("telemetry", viewModel.Source);
                Assert.Equal("Stop tracking", viewModel.TrackingButtonText);
                Assert.Equal("Tracking on", viewModel.TrackingStatusText);
                Assert.Contains("Found 1 pools with objects still checked out", viewModel.LeakWarningText);
                Assert.Contains("No pools matching", viewModel.SearchEmptyText);
                Assert.Equal("Just borrowed", viewModel.SelectedPool!.RecentEventText);
                PoolKitEventListItemViewModel eventRow = Assert.Single(viewModel.Events);
                Assert.Equal("Borrowed", eventRow.EventTypeText);
            }
            finally
            {
                viewModel.Dispose();
                service.SetCulture("zh-CN");
            }
        });
    }

    /// <summary>创建带单个对象池、单个借出事件和一个疑似未归还候选的最小状态。</summary>
    private static WorkbenchPoolKitState CreateState()
    {
        WorkbenchPoolKitPool[] pools = { CreatePool("PanelHandler", "YokiFrame.PanelHandler", 1, 0) };
        WorkbenchPoolKitEvent[] events =
        {
            new("Spawn", 16.85d, "PanelHandler", "Panel-0", "Assets/UI/Panel.cs", 18)
        };
        object dataSource = CreateInternalDataSource(
            "unity-editor", "pool-session", 8L, "PlayMode",
            DateTimeOffset.Parse("2026-07-16T08:00:00Z"), "telemetry", string.Empty,
            new[] { "Global\\YokiFrame.PoolKit" }, string.Empty, "{}");
        return CreateInternal<WorkbenchPoolKitState>(
            dataSource,
            1L,
            new WorkbenchPoolKitStats(1, 1, 0, 3, 5, true, false, true, 1),
            pools,
            events,
            new WorkbenchPoolKitLeakReport(new[] { new WorkbenchPoolKitSuspectedLeak("PanelHandler", 1, 4) }, 1, false),
            1,
            1,
            false,
            false);
    }

    /// <summary>创建单个对象池测试 read model。</summary>
    private static WorkbenchPoolKitPool CreatePool(string name, string typeName, int activeCount, int inactiveCount)
    {
        WorkbenchPoolKitObject[] active = Enumerable.Range(0, activeCount)
            .Select(index => new WorkbenchPoolKitObject("Object-" + index, 10 + index, "Assets/Test.cs", 10 + index))
            .ToArray();
        WorkbenchPoolKitObject[] inactive = Enumerable.Range(0, inactiveCount)
            .Select(index => new WorkbenchPoolKitObject("Idle-" + index, 0, string.Empty, 0))
            .ToArray();
        int total = activeCount + inactiveCount;
        return new WorkbenchPoolKitPool(
            name + "\u001f" + typeName + "\u001f0",
            name, typeName, total, activeCount, inactiveCount, Math.Max(total, 4), 20,
            total > 0 ? (double)activeCount / total : 0d,
            "Normal", activeCount, false, inactiveCount, false, active, inactive);
    }

    /// <summary>通过反射创建 Application 内部 PoolKit 数据源。</summary>
    private static object CreateInternalDataSource(params object[] arguments)
    {
        Type type = typeof(WorkbenchPoolKitState).Assembly.GetType(
            "YokiFrame.Tooling.Application.Models.PoolKit.WorkbenchPoolKitDataSource", true)!;
        return Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, arguments, null)!;
    }

    /// <summary>调用 Application 模型的内部构造方法。</summary>
    private static T CreateInternal<T>(params object[] arguments)
    {
        return Assert.IsType<T>(Activator.CreateInstance(
            typeof(T),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, arguments, null));
    }
}
