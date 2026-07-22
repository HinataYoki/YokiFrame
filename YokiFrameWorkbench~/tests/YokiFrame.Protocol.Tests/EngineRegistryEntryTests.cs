using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.FastChannel;

namespace YokiFrame.Protocol.Tests;

/// <summary>
/// 覆盖 engine registry 的序列化和反序列化 roundtrip。
/// </summary>
public sealed class EngineRegistryEntryTests
{
    /// <summary>
    /// 验证已知字段和扩展字段在 roundtrip 后保持可读。
    /// </summary>
    [Fact]
    public void EngineRegistryRoundtripKeepsCoreFields()
    {
        const string json = "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"engine\":\"Unity\",\"version\":\"6000.7.0a1\",\"projectPath\":\"F:/Project\",\"adapterVersion\":\"2.0.0\",\"sessionId\":\"session-a\",\"generation\":12,\"mode\":\"Edit\",\"capabilities\":[\"commands\",\"snapshots\"],\"fastChannels\":[{\"protocolVersion\":1,\"engineId\":\"unity-editor\",\"sessionId\":\"session-a\",\"generation\":12,\"transport\":\"namedPipe\",\"endpoint\":\"YokiFrame.FastChannel.unity-editor\",\"enabled\":true,\"fallback\":\"filebridge\"}],\"extraField\":{\"ok\":true}}";
        var entry = EngineRegistryEntry.FromJson(json);
        var roundtrip = EngineRegistryEntry.FromJson(entry.ToJson());

        Assert.Equal(2, roundtrip.ProtocolVersion);
        Assert.Equal("unity-editor", roundtrip.EngineId);
        Assert.Equal("Unity", roundtrip.Engine);
        Assert.Equal("session-a", roundtrip.SessionId);
        Assert.Equal(12L, roundtrip.Generation);
        Assert.Equal("Edit", roundtrip.Mode);
        Assert.Contains("commands", roundtrip.Capabilities);
        Assert.Single(roundtrip.FastChannels);
        Assert.Equal(FastChannelTransport.NamedPipe, roundtrip.FastChannels[0].Transport);
        Assert.False(roundtrip.ExtensionData.ContainsKey("sessionId"));
        Assert.False(roundtrip.ExtensionData.ContainsKey("generation"));
        Assert.False(roundtrip.ExtensionData.ContainsKey("mode"));
        Assert.False(roundtrip.ExtensionData.ContainsKey("fastChannels"));
        Assert.True(roundtrip.ExtensionData.ContainsKey("extraField"));
    }

}
