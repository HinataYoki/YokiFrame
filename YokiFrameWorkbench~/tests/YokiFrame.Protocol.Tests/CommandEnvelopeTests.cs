using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Protocol.Tests;

/// <summary>
/// 覆盖 FileBridge 命令信封 roundtrip。
/// </summary>
public sealed class CommandEnvelopeTests
{
    /// <summary>
    /// 验证 command envelope 包含 Runtime 侧识别所需字段。
    /// </summary>
    [Fact]
    public void CommandEnvelopeRoundtripKeepsProtocolFields()
    {
        var envelope = CommandEnvelope.Create(
            "unity-editor",
            "cli",
            "cli-1783429791734-b01525ba",
            "System",
            "ping",
            "{}",
            10000);
        var roundtrip = CommandEnvelope.FromJson(envelope.ToJson());

        Assert.Equal(2, roundtrip.ProtocolVersion);
        Assert.Equal("unity-editor", roundtrip.EngineId);
        Assert.Equal("cli", roundtrip.Source);
        Assert.Equal("System", roundtrip.Kit);
        Assert.Equal("ping", roundtrip.Action);
        Assert.Equal("{}", roundtrip.PayloadJson);
        Assert.Equal(10000, roundtrip.TimeoutMs);
    }

    /// <summary>
    /// 验证 payload 超过 CommandPolicy 上限时会在工具侧被拒绝。
    /// </summary>
    [Fact]
    public void CommandEnvelopeRejectsOversizedPayload()
    {
        var oversizedPayload = "{\"data\":\"" + new string('x', CommandEnvelope.PAYLOAD_MAX_BYTES) + "\"}";

        var exception = Assert.Throws<YokiFrameProtocolException>(() => CommandEnvelope.Create(
            "unity-editor",
            "cli",
            "cli-1783429791734-b01525ba",
            "System",
            "ping",
            oversizedPayload,
            10000));

        Assert.Equal("PayloadTooLarge", exception.Error.Code);
    }

    /// <summary>
    /// 验证 timeout 越界时会在工具侧被拒绝，避免写出 Runtime 必拒命令。
    /// </summary>
    [Fact]
    public void CommandEnvelopeRejectsTimeoutOutsidePolicyRange()
    {
        var exception = Assert.Throws<YokiFrameProtocolException>(() => CommandEnvelope.Create(
            "unity-editor",
            "cli",
            "cli-1783429791734-b01525ba",
            "System",
            "ping",
            "{}",
            CommandEnvelope.COMMAND_TIMEOUT_MIN_MS - 1));

        Assert.Equal("InvalidTimeout", exception.Error.Code);
    }

    /// <summary>
    /// 验证缺失 protocolVersion 的外部 JSON 不会被工具模型静默提升为当前版本。
    /// </summary>
    [Fact]
    public void MissingProtocolVersionDoesNotBecomeCurrentVersion()
    {
        const string json = "{\"engineId\":\"unity-editor\",\"source\":\"cli\",\"requestId\":\"request-a\",\"kit\":\"System\",\"action\":\"ping\",\"payloadJson\":\"{}\",\"timeoutMs\":10000}";

        var envelope = CommandEnvelope.FromJson(json);

        Assert.Equal(0, envelope.ProtocolVersion);
    }
}
