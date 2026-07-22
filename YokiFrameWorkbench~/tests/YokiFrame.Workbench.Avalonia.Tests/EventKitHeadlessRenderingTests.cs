using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YokiFrame.Tooling.Application.Models.EventKit.Scan;
using YokiFrame.Tooling.Application.Packages;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views;
using YokiFrame.Workbench.Avalonia.Views.Pages;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>覆盖 EventKit 行式主从布局在最小窗口和默认窗口中的真实渲染结果。</summary>
public sealed class EventKitHeadlessRenderingTests
{
    /// <summary>验证事件卡密度、真实字号和左右详情列在目标窗口尺寸内完整可见。</summary>
    /// <param name="width">待验证窗口宽度。</param>
    /// <param name="height">待验证窗口高度。</param>
    /// <param name="minimumVisibleCards">列表视口至少完整可见的事件卡数量。</param>
    [Theory]
    [InlineData(1280, 820, 6)]
    [InlineData(1700, 1060, 7)]
    public async Task EventKitMasterDetailFitsTargetViewport(
        int width,
        int height,
        int minimumVisibleCards)
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var shellViewModel = CreateShellViewModel();
            ApplyCodeScan(shellViewModel.EventKitPage, CreateVisualCodeScan());
            shellViewModel.SelectedPage = "EventKit";
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
                AssertEventKitLayout(window, minimumVisibleCards);
                SaveFrame(window, $"eventkit-master-detail-{width}x{height}.png");
            }
            finally
            {
                window.Close();
                shellViewModel.EventKitPage.Dispose();
            }
        });
    }

    /// <summary>创建不启动外部刷新循环的 Workbench 壳层视图模型。</summary>
    /// <returns>可切换到 EventKit 页面的壳层视图模型。</returns>
    private static WorkbenchShellViewModel CreateShellViewModel()
    {
        var packageRoot = Directory.GetParent(FindWorkbenchRoot())?.FullName
            ?? throw new DirectoryNotFoundException("无法定位 YokiFrame 包根。");
        var packageMetadata = YokiFramePackageMetadataReader.Read(packageRoot);
        return new WorkbenchShellViewModel(
            () => { },
            _ => { },
            (_, _) => Task.CompletedTask,
            packageMetadata,
            _ => Task.CompletedTask);
    }

    /// <summary>检查主从列宽、卡片密度、字号红线和关键详情区的窗口边界。</summary>
    /// <param name="window">已经完成布局的 Workbench 窗口。</param>
    /// <param name="minimumVisibleCards">列表视口至少完整可见的卡片数量。</param>
    private static void AssertEventKitLayout(Window window, int minimumVisibleCards)
    {
        var page = window.GetVisualDescendants().OfType<EventKitPageView>().Single();
        var list = page.GetVisualDescendants().OfType<ListBox>()
            .Single(static control => control.Classes.Contains("eventkit-relation-list"));
        var detail = page.GetVisualDescendants().OfType<Border>()
            .Single(static control => control.Classes.Contains("eventkit-detail-panel"));

        Assert.True(list.Bounds.Width >= 560, "EventKit 事件列表窄于 560px。 ");
        Assert.True(detail.Bounds.Width >= 330, "EventKit 详情列窄于 330px。 ");
        Assert.True(
            list.Bounds.Width >= detail.Bounds.Width * 1.6,
            "EventKit 事件信息流未获得足够的主区宽度。 ");
        AssertControlFitsViewport(page, window, "EventKit 页面");
        AssertControlFitsViewport(detail, window, "EventKit 详情列");
        Assert.Empty(page.GetVisualAncestors().OfType<Viewbox>());

        ListBoxItem[] visibleCards = list.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Where(item => IsFullyVisible(item, list))
            .ToArray();
        Assert.True(
            visibleCards.Length >= minimumVisibleCards,
            $"EventKit 首屏仅完整显示 {visibleCards.Length} 张事件卡。 ");
        Assert.All(visibleCards, static item => Assert.InRange(item.Bounds.Height, 88, 100));

        var visibleText = page.GetVisualDescendants().OfType<TextBlock>()
            .Where(static text => text.IsVisible && !string.IsNullOrWhiteSpace(text.Text))
            .ToArray();
        Assert.NotEmpty(visibleText);
        Assert.All(visibleText, static text => Assert.True(
            text.FontSize >= 12,
            $"EventKit 文本“{text.Text}”小于 12px。"));
        Assert.DoesNotContain(visibleText, static text => string.Equals(text.Text, "代码关系", StringComparison.Ordinal));
        Assert.Contains(visibleText, static text => string.Equals(text.Text, "运行时间线", StringComparison.Ordinal));
        Assert.Contains(visibleText, static text => string.Equals(text.Text, "注册", StringComparison.Ordinal));
        Assert.Contains(visibleText, static text => string.Equals(text.Text, "注销", StringComparison.Ordinal));

        Border[] eventHubs = page.GetVisualDescendants()
            .OfType<Border>()
            .Where(static border => border.Classes.Contains("eventkit-event-hub"))
            .ToArray();
        Assert.NotEmpty(eventHubs);
        Assert.All(eventHubs, static hub =>
        {
            Assert.NotNull(hub.Background);
            Assert.True(hub.BorderThickness.Left >= 1, "EventKit 通道卡片缺少可见边框。 ");
            Assert.True(hub.CornerRadius.TopLeft > 0, "EventKit 通道卡片缺少圆角。 ");
        });
    }

    /// <summary>判断事件卡的上下边界是否完整落在列表视口内。</summary>
    /// <param name="item">待检查的事件卡容器。</param>
    /// <param name="list">承载事件卡的列表。</param>
    /// <returns>事件卡完整可见时返回 true。</returns>
    private static bool IsFullyVisible(ListBoxItem item, ListBox list)
    {
        Point? topLeft = item.TranslatePoint(default, list);
        if (topLeft == null)
        {
            return false;
        }

        return topLeft.Value.Y >= -1
            && topLeft.Value.Y + item.Bounds.Height <= list.Bounds.Height + 1;
    }

    /// <summary>按控件的窗口坐标检查四条边界，拒绝最小窗口下的裁切。</summary>
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
        Assert.True(topLeft.Value.X >= -1, $"{label}左侧越出窗口。 ");
        Assert.True(topLeft.Value.Y >= -1, $"{label}顶部越出窗口。 ");
        Assert.True(bottomRight.Value.X <= window.ClientSize.Width + 1, $"{label}右侧被窗口裁切。 ");
        Assert.True(bottomRight.Value.Y <= window.ClientSize.Height + 1, $"{label}底部被窗口裁切。 ");
    }

    /// <summary>创建七个具备完整发送、注册和注销位置的视觉扫描样本。</summary>
    /// <returns>可覆盖两种视口首屏密度的静态扫描结果。</returns>
    private static WorkbenchEventKitCodeScan CreateVisualCodeScan()
    {
        string[] eventKeys =
        {
            "SmokeSignal.Pulse",
            "SmokeSignal.Ready",
            "ActiveSceneChangedEvent",
            "PlayerDamagedEvent",
            "InventoryChangedEvent",
            "AudioBusChangedEvent",
            "LegacyMessage"
        };
        WorkbenchEventKitCodeRelation[] relations = eventKeys
            .Select((eventKey, index) => CreateRelation(eventKey, index))
            .ToArray();
        return new WorkbenchEventKitCodeScan(
            "C:/Project",
            true,
            42,
            relations.Length,
            TimeSpan.FromMilliseconds(2873),
            relations);
    }

    /// <summary>创建一个按序号区分源码行号的 EventKit 关系。</summary>
    /// <param name="eventKey">事件键。</param>
    /// <param name="index">用于生成稳定行号和通道的索引。</param>
    /// <returns>具备三类源码位置的扫描关系。</returns>
    private static WorkbenchEventKitCodeRelation CreateRelation(string eventKey, int index)
    {
        string channel = index % 3 == 0 ? "Type" : (index % 3 == 1 ? "Enum" : "String");
        int line = 40 + (index * 10);
        WorkbenchEventKitCodeLocation[] unregisters = index == 0
            ? Array.Empty<WorkbenchEventKitCodeLocation>()
            : new[] { new WorkbenchEventKitCodeLocation("Assets/Runtime/EventReceiver.cs", line + 6) };
        return new WorkbenchEventKitCodeRelation(
            channel,
            eventKey,
            eventKey + "Payload",
            new[] { new WorkbenchEventKitCodeLocation("Assets/Runtime/EventEmitter.cs", line) },
            new[] { new WorkbenchEventKitCodeLocation("Assets/Runtime/EventReceiver.cs", line + 2) },
            unregisters);
    }

    /// <summary>通过页面现有私有边界提交静态扫描结果。</summary>
    /// <param name="viewModel">目标 EventKit 页面视图模型。</param>
    /// <param name="scan">待展示的扫描结果。</param>
    private static void ApplyCodeScan(
        EventKitPageViewModel viewModel,
        WorkbenchEventKitCodeScan scan)
    {
        MethodInfo? method = typeof(EventKitPageViewModel).GetMethod(
            "ApplyCodeScan",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(viewModel, new object[] { scan });
    }

    /// <summary>保存 Headless 渲染帧，供 EventKit 两种目标窗口尺寸人工复核。</summary>
    /// <param name="window">已经完成布局的 Workbench 窗口。</param>
    /// <param name="fileName">目标截图文件名。</param>
    private static void SaveFrame(Window window, string fileName)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var outputDirectory = Path.Combine(FindWorkbenchRoot(), ".artifacts", "screenshots", "workbench");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, fileName);
        using FileStream stream = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        frame.Save(stream);
        Assert.True(stream.Length > 1024, "EventKit Headless 截图内容为空或异常小。 ");
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

        throw new DirectoryNotFoundException("无法定位 YokiFrameWorkbench~ 源码根。 ");
    }
}
