using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Client.Tests.Lifecycle;

/// <summary>覆盖统一 Client 在生命周期结束后的公共入口门禁。</summary>
public sealed class YokiFrameClientDisposalTests
{
    /// <summary>验证 FileBridge 与 Telemetry 入口在 Dispose 后统一拒绝访问。</summary>
    [Fact]
    public void DisposeRejectsFileBridgeAndTelemetryEntries()
    {
        var client = new YokiFrameClient(CreateProjectRoot());
        client.Dispose();

        AssertDisposed(() => _ = client.Paths);
        AssertDisposed(() => _ = client.ReadHarnessCapabilities());
        AssertDisposed(() => _ = client.ReadEngineEntries());
        AssertDisposed(() => _ = client.ReadSnapshot("unity-editor", "FsmKit", "state"));
        AssertDisposed(() => _ = client.ReadHeartbeat("unity-editor"));
        AssertDisposed(() => _ = client.ReadBridgeStatus("unity-editor"));
        AssertDisposed(() => _ = client.ReadTelemetry(
            "unity-editor", "FsmKit", "state", 1L,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES));
        AssertDisposed(() => _ = client.ReadTelemetryIfChanged(
            "unity-editor", "FsmKit", "state", 1L,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES, 1L));
    }

    /// <summary>验证 FastChannel 与可靠命令入口在 Dispose 后不能重新建立任何传输。</summary>
    [Fact]
    public void DisposeRejectsCommandTransportEntries()
    {
        var client = new YokiFrameClient(CreateProjectRoot());
        client.Dispose();

        AssertDisposed(() => _ = client.CanSendFastChannelReadOnlyCommand("unity-editor", "System", "ping"));
        AssertDisposed(() => _ = client.InvalidateFastChannelConnectionsAsync("unity-editor"));
        AssertDisposed(() => _ = client.SendFastChannelReadOnlyCommandAsync(
            "unity-editor", "System", "ping", "{}", "tests", 1000, CancellationToken.None));
        AssertDisposed(() => _ = client.SendFastChannelReadOnlySystemCommandAsync(
            "unity-editor", "ping", "tests", 1000, CancellationToken.None));
        AssertDisposed(() => _ = client.SendCommandAsync(
            "unity-editor", "System", "ping", "{}", "tests", 1000, CancellationToken.None));
    }

    /// <summary>验证多个线程同时结束 Client 生命周期时不会重复释放或恢复入口。</summary>
    [Fact]
    public void ConcurrentDisposeIsIdempotent()
    {
        var client = new YokiFrameClient(CreateProjectRoot());

        Parallel.For(0, 32, _ => client.Dispose());
        client.Dispose();

        AssertDisposed(() => _ = client.ReadEngineEntries());
    }

    /// <summary>断言操作以标准 ObjectDisposedException 拒绝，而不是继续触碰底层传输。</summary>
    /// <param name="operation">Dispose 后不得执行的公共入口。</param>
    private static void AssertDisposed(Action operation)
    {
        Assert.Throws<ObjectDisposedException>(operation);
    }

    /// <summary>创建无需落盘的唯一测试项目根，避免并行用例共享 Client 路径状态。</summary>
    /// <returns>当前用例独占的项目根路径。</returns>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-client-disposal-tests", Guid.NewGuid().ToString("N"));
    }
}
