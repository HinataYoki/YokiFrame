using System.Reflection;
using System.IO.MemoryMappedFiles;
using System.Text;
using Avalonia.Threading;
using YokiFrame;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>覆盖 1 秒 dashboard 刷新对 FsmKit 高频 Shared Memory 游标的影响。</summary>
public sealed class WorkbenchFsmTelemetryCursorTests
{
    private const string ENGINE_ID = "unity-editor";
    private const long GENERATION = 7L;

    /// <summary>验证身份完全相同时保留游标，session 变化时才重置。</summary>
    [Fact]
    public async Task DashboardRefreshPreservesCursorUntilIdentityChanges()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var projectRoot = Path.Combine(Path.GetTempPath(), "fsm-cursor-tests", Guid.NewGuid().ToString("N"));
            WorkbenchWindow window = new(new WorkbenchDashboardService(projectRoot));
            try
            {
                object channel = GetTelemetryChannel(window);
                SetCursorIdentity(channel, "session-7", 17L);
                InvokeRefreshMode(window, CreateDashboardState(projectRoot, "session-7"));

                Assert.Equal(17L, ReadField<long>(channel, "mSequence"));

                InvokeRefreshMode(window, CreateDashboardState(projectRoot, "session-8"));

                Assert.Equal(long.MinValue, ReadField<long>(channel, "mSequence"));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>验证命名段不可用后暂停 100ms 请求，只等待低频 dashboard 再次启用。</summary>
    [Fact]
    public async Task UnavailableNamedSegmentSuspendsHighFrequencyRequest()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        var projectRoot = Path.Combine(Path.GetTempPath(), "fsm-cursor-tests", Guid.NewGuid().ToString("N"));
        WorkbenchWindow? window = null;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            window = new WorkbenchWindow(new WorkbenchDashboardService(projectRoot));
            var state = CreateDashboardState(projectRoot, "session-7");
            SetField(window, "mCurrentState", state);
            InvokeRefreshMode(window, state);
            InvokePollingLifecycle(window, "StartFsmTelemetryPolling");
        });

        await Task.Delay(300);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Assert.NotNull(window);
            Dispatcher.UIThread.RunJobs();
            Assert.Null(ReadFieldValue(GetTelemetryChannel(window), "mCurrentRequest"));
            InvokePollingLifecycle(window, "StopFsmTelemetryPolling");
            window.Close();
        });
    }

    /// <summary>验证协议坏帧在首次拒绝后暂停，并且不信任其中可能伪造的 sequence。</summary>
    [Fact]
    public async Task ProtocolRejectedFrameSuspendsWithoutAdvancingCursor()
    {
        // 命名 Shared Memory 仅 Windows 支持；Linux 上 CreateNew(name) 会 PlatformNotSupportedException。
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
            SetField(window, "mCurrentState", state);
            InvokeRefreshMode(window, state);
            InvokePollingLifecycle(window, "StartFsmTelemetryPolling");
        });

        await Task.Delay(300);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Assert.NotNull(window);
            Dispatcher.UIThread.RunJobs();
            object channel = GetTelemetryChannel(window);
            Assert.Null(ReadFieldValue(channel, "mCurrentRequest"));
            Assert.Equal(long.MinValue, ReadField<long>(channel, "mSequence"));
            InvokePollingLifecycle(window, "StopFsmTelemetryPolling");
            window.Close();
        });
    }

    /// <summary>验证页面提交触发实例切换时，旧 overview 请求不能覆盖新命名段的空游标。</summary>
    [Fact]
    public async Task ApplyThatRotatesSelectionDoesNotCommitOldRequestCursor()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var projectRoot = Path.Combine(Path.GetTempPath(), "fsm-cursor-tests", Guid.NewGuid().ToString("N"));
            WorkbenchWindow window = new(new WorkbenchDashboardService(projectRoot));
            try
            {
                var dashboardState = CreateDashboardState(projectRoot, "session-7");
                SetField(window, "mCurrentState", dashboardState);
                InvokeRefreshMode(window, dashboardState);
                object channel = GetTelemetryChannel(window);
                var oldRequest = ReadFieldValue(channel, "mCurrentRequest");
                Assert.NotNull(oldRequest);
                var acceptedState = FsmKitContractTestData.CreateState(
                    "default-instance",
                    "telemetry",
                    "{}",
                    "YokiFrame.FsmKit.default-instance",
                    "Idle");
                var result = CreateInternal<WorkbenchFsmKitTelemetryReadResult>(
                    WorkbenchFsmKitTelemetryReadStatus.Accepted,
                    acceptedState,
                    41L,
                    638880000000000000L,
                    true,
                    string.Empty);

                InvokeApplyPollResult(channel, oldRequest, result);

                Assert.Equal(long.MinValue, ReadField<long>(channel, "mSequence"));
                var newRequest = ReadFieldValue(channel, "mCurrentRequest");
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

    /// <summary>读取窗口持有的 FsmKit 遥测通道实例。</summary>
    /// <param name="window">目标窗口。</param>
    /// <returns>FsmKitTelemetryChannel 实例。</returns>
    private static object GetTelemetryChannel(WorkbenchWindow window)
    {
        var field = typeof(WorkbenchWindow).GetField(
            "mFsmTelemetryChannel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<object>(field.GetValue(window));
    }

    /// <summary>把测试通道设为已经消费一帧的 telemetry 身份。</summary>
    /// <param name="channel">FsmKit 遥测通道。</param>
    /// <param name="sessionId">宿主 session。</param>
    /// <param name="sequence">已接受帧序号。</param>
    private static void SetCursorIdentity(object channel, string sessionId, long sequence)
    {
        SetField(channel, "mEngineId", ENGINE_ID);
        SetField(channel, "mSessionId", sessionId);
        SetField(channel, "mGeneration", GENERATION);
        SetField(channel, "mTelemetrySource", "telemetry");
        SetField(channel, "mSelectionId", string.Empty);
        SetField(channel, "mSequence", sequence);
    }

    /// <summary>调用窗口的 Shared Memory 模式同步入口，模拟一次低频 dashboard 提交。</summary>
    /// <param name="window">目标窗口。</param>
    /// <param name="state">本轮 dashboard 状态。</param>
    private static void InvokeRefreshMode(WorkbenchWindow window, WorkbenchDashboardState state)
    {
        var method = typeof(WorkbenchWindow).GetMethod(
            "UpdateSharedMemoryRefreshMode",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(window, new object[] { state });
    }

    /// <summary>调用通道的单次 UI 提交入口，验证 Apply 与游标推进构成同一身份事务。</summary>
    private static void InvokeApplyPollResult(object channel, object request, object result)
    {
        // ApplyResult 声明在泛型基类的私有段，必须沿继承链查找。
        for (var current = channel.GetType(); current != null; current = current.BaseType)
        {
            var method = current.GetMethod(
                "ApplyResult",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null)
            {
                method.Invoke(channel, new[] { request, result });
                return;
            }
        }

        Assert.Fail("未找到 ApplyResult 方法。");
    }

    /// <summary>调用后台轮询的启动或停止入口，避免测试依赖真实窗口 Opened 生命周期。</summary>
    private static void InvokePollingLifecycle(WorkbenchWindow window, string methodName)
    {
        var method = typeof(WorkbenchWindow).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(window, null);
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

    /// <summary>通过受控内部构造器创建只携带来源身份的 FsmKit 状态。</summary>
    /// <param name="sessionId">宿主 session。</param>
    /// <returns>测试用 FsmKit telemetry 状态。</returns>
    private static WorkbenchFsmKitState CreateFsmState(string sessionId)
    {
        var dataSource = CreateInternal<WorkbenchFsmKitDataSource>(
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
        return CreateInternal<WorkbenchFsmKitState>(
            dataSource,
            string.Empty,
            string.Empty,
            0,
            Array.Empty<WorkbenchFsmMachineSummary>(),
            null!,
            Array.Empty<WorkbenchFsmTransition>(),
            0,
            Array.Empty<WorkbenchFsmStateEvent>(),
            0);
    }

    /// <summary>沿类型层次设置目标对象私有字段；基类字段声明在泛型基类上。</summary>
    /// <typeparam name="T">字段值类型。</typeparam>
    /// <param name="target">目标对象。</param>
    /// <param name="name">字段名。</param>
    /// <param name="value">字段值。</param>
    private static void SetField<T>(object target, string name, T value)
    {
        FindField(target.GetType(), name).SetValue(target, value);
    }

    /// <summary>沿类型层次读取目标对象私有字段以验证游标状态。</summary>
    /// <typeparam name="T">字段值类型。</typeparam>
    /// <param name="target">目标对象。</param>
    /// <param name="name">字段名。</param>
    /// <returns>字段当前值。</returns>
    private static T ReadField<T>(object target, string name)
    {
        return Assert.IsType<T>(FindField(target.GetType(), name).GetValue(target));
    }

    /// <summary>沿类型层次读取允许为空的私有字段，供后台请求暂停断言使用。</summary>
    /// <param name="target">目标对象。</param>
    /// <param name="name">字段名。</param>
    /// <returns>字段当前值。</returns>
    private static object? ReadFieldValue(object target, string name)
    {
        return FindField(target.GetType(), name).GetValue(target);
    }

    /// <summary>沿继承链查找字段声明，覆盖泛型基类中的私有字段。</summary>
    /// <param name="type">目标对象运行时类型。</param>
    /// <param name="name">字段名。</param>
    /// <returns>找到的字段；未找到时使断言失败。</returns>
    private static FieldInfo FindField(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                return field;
            }
        }

        Assert.Fail("未找到字段: " + name);
        return null!;
    }

    /// <summary>调用 Application 模型的内部构造器，避免测试复制生产 parser。</summary>
    /// <typeparam name="T">目标模型类型。</typeparam>
    /// <param name="arguments">构造参数。</param>
    /// <returns>创建出的强类型实例。</returns>
    private static T CreateInternal<T>(params object[] arguments)
    {
        var instance = Activator.CreateInstance(
            typeof(T),
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            arguments,
            null);
        return Assert.IsType<T>(instance);
    }
}
