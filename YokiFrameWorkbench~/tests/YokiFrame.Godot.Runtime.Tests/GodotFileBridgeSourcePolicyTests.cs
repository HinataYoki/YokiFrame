using YokiFrame;

namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// 验证 Godot Runtime FileBridge 对产品中立自动化来源的接纳与拒绝边界。
/// </summary>
[Collection(GodotFileBridgeHostCollection.NAME)]
public sealed class GodotFileBridgeSourcePolicyTests
{
    /// <summary>
    /// 验证产品中立的外部自动化来源可以通过 Runtime FileBridge 执行已登记命令。
    /// </summary>
    [Fact]
    public void ExternalAutomationSourceProducesTerminalSuccessResponse()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        fixture.WriteSystemCommand(
            "automation-ping-001",
            "ping",
            YokiFrameCommandSourceContract.EXTERNAL_AUTOMATION);

        Assert.Equal(1, host.ProcessPendingCommands());

        var response = GodotFileBridgeHostFixture.ReadObject(
            fixture.GetResponsePath("automation-ping-001"));
        Assert.Equal("Success", response["status"]?.GetValue<string>());
    }

    /// <summary>
    /// 验证未登记的历史自动化来源不会绕过 Runtime 默认来源策略。
    /// </summary>
    [Fact]
    public void UnregisteredAutomationSourceProducesPolicyRejectedResponse()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        fixture.WriteSystemCommand("legacy-source-001", "ping", "legacy-automation");

        Assert.Equal(1, host.ProcessPendingCommands());

        var response = GodotFileBridgeHostFixture.ReadObject(
            fixture.GetResponsePath("legacy-source-001"));
        Assert.Equal("Error", response["status"]?.GetValue<string>());
        Assert.Equal("PolicyRejected", response["errorCode"]?.GetValue<string>());
    }
}
