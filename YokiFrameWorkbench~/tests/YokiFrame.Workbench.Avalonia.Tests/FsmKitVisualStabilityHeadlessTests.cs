using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YokiFrame.Workbench.Avalonia.Components;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views.Pages;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 使用真实 FsmKit 页面像素覆盖连续等价帧、重复 Render 和状态切换的视觉稳定性。
/// </summary>
public sealed class FsmKitVisualStabilityHeadlessTests
{
    private const int EQUIVALENT_FRAME_COUNT = 3;

    /// <summary>
    /// 验证等价 telemetry 帧不会产生空白中间帧，真实状态变化后也不会回退旧图。
    /// </summary>
    [Fact]
    public async Task EquivalentFramesKeepPixelsStableAndChangedStateDoesNotRollBack()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(AssertVisualSequence);
    }

    /// <summary>
    /// 在 UI 线程显示真实页面，依次验证 Ready 稳态、Combat 切换和 Combat 稳态。
    /// </summary>
    private static void AssertVisualSequence()
    {
        FsmKitPageViewModel viewModel = new();
        ApplyTelemetryFrame(viewModel, "Ready", "Boot", "Ready");
        FsmKitPageView view = new() { DataContext = viewModel };
        Window window = new() { Width = 1200, Height = 760, Content = view };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var graph = Assert.Single(view.GetVisualDescendants().OfType<ObservedFsmGraph>());
            var readyHash = CaptureFrameHash(window);
            AssertEquivalentFrames(window, graph, viewModel, "Ready", "Boot", "Ready", readyHash);

            ApplyTelemetryFrame(viewModel, "Combat", "Ready", "Combat");
            Dispatcher.UIThread.RunJobs();
            var combatHash = CaptureFrameHash(window);
            Assert.NotEqual(readyHash, combatHash);
            AssertEquivalentFrames(window, graph, viewModel, "Combat", "Ready", "Combat", combatHash);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// 连续提交引用不同但语义相同的帧，并强制重复 Render 后比较完整帧缓冲哈希。
    /// </summary>
    /// <param name="window">承载真实 FsmKit 页面的窗口。</param>
    /// <param name="graph">需要显式触发重绘的状态图控件。</param>
    /// <param name="viewModel">接收等价 telemetry 帧的页面模型。</param>
    /// <param name="currentState">当前状态名称。</param>
    /// <param name="historyFrom">最近转换起点。</param>
    /// <param name="historyTo">最近转换终点。</param>
    /// <param name="expectedHash">当前稳定页面的像素哈希。</param>
    private static void AssertEquivalentFrames(
        Window window,
        ObservedFsmGraph graph,
        FsmKitPageViewModel viewModel,
        string currentState,
        string historyFrom,
        string historyTo,
        string expectedHash)
    {
        for (var index = 0; index < EQUIVALENT_FRAME_COUNT; index++)
        {
            ApplyTelemetryFrame(viewModel, currentState, historyFrom, historyTo);
            graph.InvalidateVisual();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(expectedHash, CaptureFrameHash(window));
        }
    }

    /// <summary>
    /// 创建并提交一帧 DTO 引用全新、但内容由参数明确控制的 FsmKit telemetry 状态。
    /// </summary>
    /// <param name="viewModel">目标页面模型。</param>
    /// <param name="currentState">chosen 实例当前状态。</param>
    /// <param name="historyFrom">最近转换起点。</param>
    /// <param name="historyTo">最近转换终点。</param>
    private static void ApplyTelemetryFrame(
        FsmKitPageViewModel viewModel,
        string currentState,
        string historyFrom,
        string historyTo)
    {
        viewModel.ApplyPeriodicState(FsmKitContractTestData.CreateState(
            "chosen-instance",
            "telemetry",
            "{\"active\":true}",
            "test://fsm-visual-stability",
            historyTo,
            transitions: new[] { (historyFrom, historyTo) },
            selectedCurrentState: currentState));
    }

    /// <summary>
    /// 捕获完整 Headless 帧缓冲并计算 SHA-256，避免只以 PNG 大小判断视觉有效性。
    /// </summary>
    /// <param name="window">已经显示且完成布局的窗口。</param>
    /// <returns>包含全部像素行的稳定十六进制哈希。</returns>
    private static string CaptureFrameHash(Window window)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        using var framebuffer = frame.Lock();
        var byteCount = checked(framebuffer.RowBytes * framebuffer.Size.Height);
        byte[] pixels = new byte[byteCount];
        Marshal.Copy(framebuffer.Address, pixels, 0, byteCount);
        return Convert.ToHexString(SHA256.HashData(pixels));
    }
}
