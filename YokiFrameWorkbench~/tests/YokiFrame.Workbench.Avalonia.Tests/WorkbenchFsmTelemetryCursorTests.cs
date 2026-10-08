using System.IO.MemoryMappedFiles;
using System.Text;
using Avalonia.Threading;
using YokiFrame;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Tooling.Application.Models.Telemetry;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>覆盖 1 秒 dashboard 刷新对 FsmKit 高频 Shared Memory 游标的影响。</summary>
public sealed class WorkbenchFsmTelemetryCursorTests
{
    private const string ENGINE_ID = "unity-editor";
    private const long GENERATION = 7L;

    /// <summary>验证身份完全相同时保留游标，session 变化时才重置。</summary>
    [Fact]
    public async Task 刷新模式_宿主身份变化_才重置游标()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var projectRoot = Path.Combine(Path.GetTempPath(), "fsm-cursor-tests", Guid.NewGuid().ToString("N"));
            WorkbenchWindow window = new(new WorkbenchDashboardService(projectRoot));
            try
            {
                var sameSession = CreateDashboardState(projectRoot, "session-7");
                window.SeedFsmTelemetryCursor(sameSession, 17L);
                window.UpdateSharedMemoryRefreshMode(sameSession);

                Assert.Equal(17L, window.FsmTelemetryObservedSequence);

                window.UpdateSharedMemoryRefreshMode(CreateDashboardState(projectRoot, "session-8"));

                Assert.Equal(long.MinValue, window.FsmTelemetryObservedSequence);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>验证命名段不可用后暂停高频请求，只等待低频 dashboard 再次启用。</summary>
    [Fact]
    public async Task 命名段不可用_高频轮询_暂停当前请求()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        var projectRoot = Path.Combine(Path.GetTempPath(), "fsm-cursor-tests", Guid.NewGuid().ToString("N"));
        WorkbenchWindow? window = null;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            window = new WorkbenchWindow(new WorkbenchDashboardService(projectRoot));
            var state = CreateDashboardState(projectRoot, "session-7");
            window.SetCurrentDashboardState(state);
            window.UpdateSharedMemoryRefreshMode(state);
            window.StartFsmTelemetryPolling();
        });

        await Task.Delay(300);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Assert.NotNull(window);
            Dispatcher.UIThread.RunJobs();
            Assert.Null(window.FsmTelemetryActiveRequest);
            _ = window.StopFsmTelemetryPolling();
            window.Close();
        });
    }

    /// <summary>验证协议坏帧在首次拒绝后暂停，并且不信任其中可能伪造的 sequence。</summary>
    [Fact]
    public async Task 协议坏帧_首次拒绝_不推进游标()
    {
        if (!WorkbenchTestPlatform.SupportsNamedMemoryMaps)
        {
            return;
        }

        InstallerHeadlessTestApplication.EnsureInitialized();
        var projectRoot = Path.Combine(Path.GetTempPath(), "fsm-cursor-tests", Guid.NewGuid().ToString("N"));
        var frame = CreateCorruptTelemetryFrame();
        var segmentName = SharedMemoryTelemetrySegmentName.Create(projectRoot, ENGINE_ID, "FsmKit", "state");
        using var memoryMap = MemoryMappedFile.CreateNew(segmentName, frame.Length, MemoryMappedFileAccess.ReadWrite);
        using var accessor = memoryMap.CreateViewAccessor(0, frame.Length, MemoryMappedFileAccess.Write);
        accessor.WriteArray(0, frame, 0, frame.Length);
        WorkbenchWindow? window = null;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            window = new WorkbenchWindow(new WorkbenchDashboardService(projectRoot));
            var state = CreateDashboardState(projectRoot, "session-7");
            window.SetCurrentDashboardState(state);
            window.UpdateSharedMemoryRefreshMode(state);
            window.StartFsmTelemetryPolling();
        });

        await Task.Delay(300);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Assert.NotNull(window);
            Dispatcher.UIThread.RunJobs();
            Assert.Null(window.FsmTelemetryActiveRequest);
            Assert.Equal(long.MinValue, window.FsmTelemetryObservedSequence);
            _ = window.StopFsmTelemetryPolling();
            window.Close();
        });
    }

    /// <summary>验证页面提交触发实例切换时，旧 overview 请求不能覆盖新命名段的空游标。</summary>
    [Fact]
    public async Task 实例切换_旧请求完成_不提交旧游标()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var projectRoot = Path.Combine(Path.GetTempPath(), "fsm-cursor-tests", Guid.NewGuid().ToString("N"));
            WorkbenchWindow window = new(new WorkbenchDashboardService(projectRoot));
            try
            {
                var dashboardState = CreateDashboardState(projectRoot, "session-7");
                window.SetCurrentDashboardState(dashboardState);
                window.UpdateSharedMemoryRefreshMode(dashboardState);
                var oldRequest = window.FsmTelemetryActiveRequest;
                Assert.NotNull(oldRequest);
                var acceptedState = FsmKitContractTestData.CreateState(
                    "default-instance",
                    "telemetry",
                    "{}",
                    "YokiFrame.FsmKit.default-instance",
                    "Idle");
                var result = new WorkbenchTelemetryReadResult<WorkbenchFsmKitState>(
                    WorkbenchTelemetryReadStatus.Accepted,
                    acceptedState,
                    41L,
                    true,
                    string.Empty);

                window.ApplyFsmTelemetryResult(oldRequest, result);

                Assert.Equal(long.MinValue, window.FsmTelemetryObservedSequence);
                var newRequest = window.FsmTelemetryActiveRequest;
                Assert.NotNull(newRequest);
                Assert.NotSame(oldRequest, newRequest);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>创建 engine 身份正确但 CRC 错误的稳定提交帧。</summary>
    /// <returns>会被底层 reader 明确归类为 CrcMismatch 的帧。</returns>
    private static byte[] CreateCorruptTelemetryFrame()
    {
        var payload = Encoding.UTF8.GetBytes("{}");
        var frame = new byte[SharedMemoryTelemetryFrameHeader.HEADER_SIZE + payload.Length];
        SharedMemoryTelemetryFrameHeader header = new(
            SharedMemoryTelemetryFrameHeader.MAGIC,
            SharedMemoryTelemetryFrameHeader.PROTOCOL_VERSION,
            YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(ENGINE_ID),
            GENERATION,
            23L,
            638880000000000000L,
            payload.Length,
            SharedMemoryTelemetryCrc32.Compute(payload) + 1U,
            SharedMemoryTelemetryWriteState.Committed);
        header.WriteTo(frame.AsSpan(0, SharedMemoryTelemetryFrameHeader.HEADER_SIZE));
        payload.CopyTo(frame.AsSpan(SharedMemoryTelemetryFrameHeader.HEADER_SIZE));
        return frame;
    }

    /// <summary>创建指定 session 的 telemetry dashboard 状态。</summary>
    /// <param name="projectRoot">测试项目根。</param>
    /// <param name="sessionId">宿主 session。</param>
    /// <returns>包含 FsmKit telemetry 的 dashboard。</returns>
    private static WorkbenchDashboardState CreateDashboardState(string projectRoot, string sessionId)
    {
        var health = new WorkbenchBridgeHealth(
            WorkbenchBridgeConnectionState.Online,
            string.Empty,
            string.Empty,
            Array.Empty<string>(),
            0,
            5,
            sessionId,
            GENERATION,
            "EditMode",
            1);
        return new WorkbenchDashboardState(
            projectRoot,
            DateTimeOffset.UtcNow,
            Array.Empty<EngineRegistryEntry>(),
            ENGINE_ID,
            null,
            health,
            null,
            Array.Empty<WorkbenchSnapshotState>(),
            string.Empty,
            Array.Empty<string>(),
            CreateFsmState(sessionId));
    }

    /// <summary>通过已有 internal 构造器创建只携带来源身份的 FsmKit 状态。</summary>
    /// <param name="sessionId">宿主 session。</param>
    /// <returns>测试用 FsmKit telemetry 状态。</returns>
    private static WorkbenchFsmKitState CreateFsmState(string sessionId)
    {
        var dataSource = new WorkbenchFsmKitDataSource(
            ENGINE_ID,
            sessionId,
            GENERATION,
            "EditMode",
            DateTimeOffset.UtcNow,
            "telemetry",
            string.Empty,
            new[] { "YokiFrame.FsmKit.state" },
            string.Empty,
            "{}");
        return new WorkbenchFsmKitState(
            dataSource,
            string.Empty,
            string.Empty,
            0,
            Array.Empty<WorkbenchFsmMachineSummary>(),
            null,
            Array.Empty<WorkbenchFsmTransition>(),
            0,
            Array.Empty<WorkbenchFsmStateEvent>(),
            0);
    }
}
