using YokiFrame.Protocol.FileBridge;

namespace YokiFrame.Client.Tests.Architecture;

/// <summary>
/// 验证具体本机 transport 与诊断 read model 的程序集所有权。
/// </summary>
public sealed class ClientTransportOwnershipTests
{
    private static readonly string[] sProtocolTransportTypeNames =
    {
        "YokiFrame.Protocol.FileBridge.YokiFramePaths",
        "YokiFrame.Protocol.FileBridge.YokiFrameFileBridgeClient",
        "YokiFrame.Protocol.IO.AtomicJsonFileWriter",
        "YokiFrame.Protocol.IO.PathSecurity",
        "YokiFrame.Protocol.FileBridge.CommandSendResult",
        "YokiFrame.Protocol.FileBridge.FileBridgeStatus",
        "YokiFrame.Protocol.FileBridge.FileBridgeRetentionInfo",
        "YokiFrame.Protocol.FileBridge.HeartbeatInfo",
        "YokiFrame.Protocol.Telemetry.SharedMemory.SharedMemoryTelemetryNamedMapReader"
    };

    /// <summary>
    /// 验证统一 Client 的公开路径、诊断和操作结果不再由 Protocol 程序集定义。
    /// </summary>
    [Fact]
    public void PublicClientModels_归属Client程序集_AssemblyBoundary()
    {
        var clientAssembly = typeof(IYokiFrameClient).Assembly;
        var pathsType = typeof(IEngineStateReader).GetProperty(nameof(IEngineStateReader.Paths))!.PropertyType;
        var heartbeatType = typeof(IEngineStateReader).GetMethod(nameof(IEngineStateReader.ReadHeartbeat))!.ReturnType;
        var bridgeStatusType = typeof(IEngineStateReader).GetMethod(nameof(IEngineStateReader.ReadBridgeStatus))!.ReturnType;
        var sendResultType = typeof(ICommandTransport).GetMethod(nameof(ICommandTransport.SendCommandAsync))!
            .ReturnType
            .GetGenericArguments()[0];

        Assert.Same(clientAssembly, pathsType.Assembly);
        Assert.Same(clientAssembly, heartbeatType.Assembly);
        Assert.Same(clientAssembly, bridgeStatusType.Assembly);
        Assert.Same(clientAssembly, sendResultType.Assembly);
    }

    /// <summary>
    /// 验证 Protocol 程序集不再暴露本机路径、IO、状态或 named map transport 类型。
    /// </summary>
    [Fact]
    public void ProtocolAssembly_不含本机Transport_AssemblyBoundary()
    {
        var protocolAssembly = typeof(CommandEnvelope).Assembly;

        foreach (var typeName in sProtocolTransportTypeNames)
        {
            Assert.Null(protocolAssembly.GetType(typeName));
        }
    }

    /// <summary>
    /// 验证具体 transport 实现存在于 Client 程序集且保持内部可见性。
    /// </summary>
    [Fact]
    public void ConcreteTransports_内部可见_AssemblyBoundary()
    {
        var clientAssembly = typeof(IYokiFrameClient).Assembly;
        var typeNames = new[]
        {
            "YokiFrame.Client.Transports.FileBridge.FileBridgeTransport",
            // 原子写已单源为源码链接的共享实现；守卫意图不变：具体 IO 实现必须保持 Client 程序集内部可见。
            "YokiFrame.YokiFrameAtomicFileWriter",
            "YokiFrame.Client.FileBridge.IO.PathSecurity",
            "YokiFrame.Client.Telemetry.SharedMemory.SharedMemoryTelemetryNamedMapReader"
        };

        foreach (var typeName in typeNames)
        {
            var transportType = clientAssembly.GetType(typeName);
            Assert.NotNull(transportType);
            Assert.False(transportType.IsPublic);
        }
    }

    /// <summary>
    /// 验证聚合 Client 同时实现状态、命令和 telemetry 窄端口，Application 可以按能力组合依赖。
    /// </summary>
    [Fact]
    public void AggregatedClientExposesNarrowCapabilityPorts()
    {
        var clientType = typeof(YokiFrameClient);

        Assert.True(typeof(IEngineStateReader).IsAssignableFrom(clientType));
        Assert.True(typeof(ICommandTransport).IsAssignableFrom(clientType));
        Assert.True(typeof(ITelemetryReader).IsAssignableFrom(clientType));
        Assert.True(typeof(IFastChannelCommandTransport).IsAssignableFrom(clientType));
    }

    /// <summary>
    /// 验证可靠命令端口不再夹带可选 FastChannel 方法，避免不支持能力被伪装成默认传输行为。
    /// </summary>
    [Fact]
    public void ReliableCommandPortDoesNotOwnFastChannelMethods()
    {
        Assert.DoesNotContain(
            typeof(ICommandTransport).GetMethods(),
            method => method.Name.Contains("FastChannel", StringComparison.Ordinal));
        Assert.Contains(
            typeof(IFastChannelCommandTransport).GetMethods(),
            method => method.Name == nameof(IFastChannelCommandTransport.SendFastChannelReadOnlyCommandAsync));
    }

}
