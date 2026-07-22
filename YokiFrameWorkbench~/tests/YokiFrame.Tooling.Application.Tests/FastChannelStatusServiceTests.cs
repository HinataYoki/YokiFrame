using YokiFrame.Client;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 FastChannelStatusService 对 registry endpoint 与 FileBridge fallback 的应用层选择。
/// </summary>
public sealed class FastChannelStatusServiceTests
{
    /// <summary>
    /// 验证已发布的启用 endpoint 会原样作为 Application read model 返回。
    /// </summary>
    [Fact]
    public void GetStatusReturnsPublishedEnabledEndpoint()
    {
        using var projectRoot = FastChannelStatusProjectRoot.Create(
            "\"fastChannels\":[{\"protocolVersion\":1,\"engineId\":\"unity-editor\",\"sessionId\":\"session-a\",\"generation\":9,\"transport\":\"namedPipe\",\"endpoint\":\"YokiFrame.FastChannel.unity-editor\",\"enabled\":true,\"fallback\":\"filebridge\"}]");

        var status = new FastChannelStatusService(new YokiFrameClient(projectRoot.Path)).GetStatus("unity-editor");

        Assert.Equal("unity-editor", status.EngineId);
        Assert.Equal("engineRegistry", status.Source);
        Assert.True(status.Endpoint.Enabled);
        Assert.Equal("namedPipe", status.Endpoint.Transport);
        Assert.Equal("YokiFrame.FastChannel.unity-editor", status.Endpoint.Endpoint);
        Assert.Equal("filebridge", status.Endpoint.Fallback);
    }

    /// <summary>
    /// 验证 registry 未发布 FastChannel 时 Application 生成 disabled endpoint，调用侧可直接选择可靠 FileBridge。
    /// </summary>
    [Fact]
    public void GetStatusReturnsDisabledFileBridgeFallbackWhenEndpointIsMissing()
    {
        using var projectRoot = FastChannelStatusProjectRoot.Create(string.Empty);

        var status = new FastChannelStatusService(new YokiFrameClient(projectRoot.Path)).GetStatus("unity-editor");

        Assert.Equal("fallback", status.Source);
        Assert.False(status.Endpoint.Enabled);
        Assert.Equal("none", status.Endpoint.Transport);
        Assert.Equal("filebridge", status.Endpoint.Fallback);
        Assert.Equal("session-a", status.Endpoint.SessionId);
        Assert.Equal(9L, status.Endpoint.Generation);
    }

    /// <summary>
    /// 创建 FastChannel status 测试所需的最小 project root，并在释放时清理目录。
    /// </summary>
    private sealed class FastChannelStatusProjectRoot : IDisposable
    {
        private const string ENGINE_ID = "unity-editor";

        /// <summary>
        /// 初始化临时 project root 并写入指定 FastChannel 字段。
        /// </summary>
        /// <param name="extraJsonFields">追加到 engine registry 的 JSON 字段；空字符串表示不发布 endpoint。</param>
        private FastChannelStatusProjectRoot(string extraJsonFields)
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "yokiframe-fastchannel-status-tests",
                Guid.NewGuid().ToString("N"));
            var engineRoot = System.IO.Path.Combine(Path, ".yokiframe", "engines", ENGINE_ID);
            Directory.CreateDirectory(engineRoot);
            var suffix = string.IsNullOrWhiteSpace(extraJsonFields) ? string.Empty : "," + extraJsonFields;
            File.WriteAllText(
                System.IO.Path.Combine(engineRoot, "engine.json"),
                "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"engine\":\"Unity\",\"sessionId\":\"session-a\",\"generation\":9" + suffix + "}");
        }

        /// <summary>
        /// 获取临时 project root 路径。
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// 创建包含指定 FastChannel registry 字段的测试 project root。
        /// </summary>
        /// <param name="extraJsonFields">追加到 engine registry 的 JSON 字段。</param>
        /// <returns>可释放的临时 project root。</returns>
        public static FastChannelStatusProjectRoot Create(string extraJsonFields)
        {
            return new FastChannelStatusProjectRoot(extraJsonFields);
        }

        /// <summary>
        /// 删除测试创建的临时 project root。
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
