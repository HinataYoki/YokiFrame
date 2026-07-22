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
    public void PublicClientModelsBelongToClientAssembly()
    {
        var clientAssembly = typeof(IYokiFrameClient).Assembly;
        var clientType = typeof(IYokiFrameClient);
        var pathsType = clientType.GetProperty(nameof(IYokiFrameClient.Paths))!.PropertyType;
        var heartbeatType = clientType.GetMethod(nameof(IYokiFrameClient.ReadHeartbeat))!.ReturnType;
        var bridgeStatusType = clientType.GetMethod(nameof(IYokiFrameClient.ReadBridgeStatus))!.ReturnType;
        var sendResultType = clientType.GetMethod(nameof(IYokiFrameClient.SendCommandAsync))!
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
    public void ProtocolAssemblyDoesNotOwnClientTransportTypes()
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
    public void ConcreteTransportImplementationsAreInternalToClient()
    {
        var clientAssembly = typeof(IYokiFrameClient).Assembly;
        var typeNames = new[]
        {
            "YokiFrame.Client.Transports.FileBridge.FileBridgeTransport",
            "YokiFrame.Client.FileBridge.IO.AtomicJsonFileWriter",
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
    /// 验证 Protocol 源码目录不再保存具体 transport 文件或 named map 平台引用。
    /// </summary>
    [Fact]
    public void ProtocolSourceContainsOnlyTransportIndependentCode()
    {
        var protocolRoot = Path.Combine(FindWorkbenchRoot(), "src", "YokiFrame.Protocol");
        var forbiddenFileNames = new[]
        {
            "YokiFramePaths.cs",
            "YokiFrameFileBridgeClient.cs",
            "AtomicJsonFileWriter.cs",
            "PathSecurity.cs",
            "CommandSendResult.cs",
            "FileBridgeStatus.cs",
            "FileBridgeRetentionInfo.cs",
            "HeartbeatInfo.cs",
            "SharedMemoryTelemetryNamedMapReader.cs"
        };

        var sourceFiles = Directory.EnumerateFiles(protocolRoot, "*.cs", SearchOption.AllDirectories).ToArray();
        foreach (var fileName in forbiddenFileNames)
        {
            Assert.DoesNotContain(sourceFiles, path => string.Equals(Path.GetFileName(path), fileName, StringComparison.Ordinal));
        }

        Assert.DoesNotContain(sourceFiles, path => File.ReadAllText(path).Contains("System.IO.MemoryMappedFiles", StringComparison.Ordinal));
    }

    /// <summary>
    /// 从测试输出目录向上定位 Workbench 源码根目录。
    /// </summary>
    /// <returns>包含 src 和 tests 的 Workbench 目录。</returns>
    private static string FindWorkbenchRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "YokiFrame.Protocol")))
            {
                return directory.FullName;
            }

            var workspaceRoot = Path.Combine(directory.FullName, "Assets", "YokiFrame", "YokiFrameWorkbench~");
            if (Directory.Exists(Path.Combine(workspaceRoot, "src", "YokiFrame.Protocol")))
            {
                return workspaceRoot;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("无法定位 YokiFrameWorkbench~ 源码目录。");
    }
}
