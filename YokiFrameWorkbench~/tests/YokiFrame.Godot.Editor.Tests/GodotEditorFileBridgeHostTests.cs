using System.Text.Json.Nodes;
using YokiFrame;

namespace YokiFrame.Godot.Editor.Tests;

/// <summary>
/// 验证正式 Godot Editor Host 的身份、心跳、最小命令面和退出释放契约。
/// </summary>
public sealed class GodotEditorFileBridgeHostTests
{
    /// <summary>
    /// 验证 Host 启动后只发布 Editor 身份，不伪造 Runtime snapshot、Telemetry 或 FastChannel。
    /// </summary>
    [Fact]
    public void StartPublishesEditorOnlyIdentityAndStopReleasesActiveState()
    {
        using GodotEditorFileBridgeHostFixture fixture = GodotEditorFileBridgeHostFixture.Create();
        using GodotEditorFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");

        host.Start();

        var registry = fixture.ReadObject(fixture.RegistryPath);
        var heartbeat = fixture.ReadObject(fixture.HeartbeatPath);
        Assert.Equal("godot-editor", registry["engineId"]?.GetValue<string>());
        Assert.Equal("Godot Editor", registry["displayName"]?.GetValue<string>());
        Assert.Equal("Editor", registry["mode"]?.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(registry["sessionId"]?.GetValue<string>()));
        Assert.True(registry["generation"]?.GetValue<long>() > 0);
        Assert.Equal(registry["sessionId"]?.GetValue<string>(), heartbeat["sessionId"]?.GetValue<string>());
        Assert.Equal(registry["generation"]?.GetValue<long>(), heartbeat["generation"]?.GetValue<long>());
        Assert.Equal("Editor", heartbeat["mode"]?.GetValue<string>());
        Assert.Empty(registry["fastChannels"]?.AsArray() ?? new JsonArray());
        Assert.False(Directory.Exists(Path.Combine(fixture.EngineRoot, "snapshots")));

        host.Stop();

        Assert.False(File.Exists(fixture.RegistryPath));
        Assert.False(File.Exists(fixture.HeartbeatPath));
    }

    /// <summary>
    /// 验证 Editor Host 只接受三个 System 只读命令，并为 ping 写入与当前身份一致的 terminal response。
    /// </summary>
    [Fact]
    public void ProcessPendingCommandsReturnsEditorPingAndCatalog()
    {
        using GodotEditorFileBridgeHostFixture fixture = GodotEditorFileBridgeHostFixture.Create();
        using GodotEditorFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        fixture.WriteSystemCommand("editor-ping-001", "ping");
        fixture.WriteSystemCommand("editor-catalog-001", "list_commands");

        var processed = host.ProcessPendingCommands();
        if (!File.Exists(fixture.GetResponsePath("editor-ping-001"))
            || !File.Exists(fixture.GetResponsePath("editor-catalog-001")))
        {
            processed += host.ProcessPendingCommands();
        }

        Assert.Equal(2, processed);

        var pingResponse = fixture.ReadObject(fixture.GetResponsePath("editor-ping-001"));
        Assert.Equal("Success", pingResponse["status"]?.GetValue<string>());
        var ping = JsonNode.Parse(pingResponse["resultJson"]?.GetValue<string>() ?? "{}")?.AsObject();
        Assert.Equal("pong", ping?["message"]?.GetValue<string>());
        Assert.Equal("godot-editor", ping?["engineId"]?.GetValue<string>());
        Assert.Equal("Editor", ping?["mode"]?.GetValue<string>());

        var catalogResponse = fixture.ReadObject(fixture.GetResponsePath("editor-catalog-001"));
        var catalog = JsonNode.Parse(catalogResponse["resultJson"]?.GetValue<string>() ?? "{}")?.AsObject();
        var actions = catalog?["kits"]?[0]?["actions"]?.AsArray();
        var actionNames = (actions ?? new JsonArray()).Select(
            static action => action?["action"]?.GetValue<string>() ?? string.Empty).ToArray();
        Assert.Equal(["bridge_status", "list_commands", "ping"], actionNames);
        Assert.All(actions ?? new JsonArray(), static action =>
            Assert.Equal("ReadOnly", action?["kind"]?.GetValue<string>()));
    }

    /// <summary>
    /// 验证产品中立的外部自动化来源可以调用 Editor Host 已登记的只读命令。
    /// </summary>
    [Fact]
    public void ExternalAutomationSourceProducesEditorTerminalSuccessResponse()
    {
        using GodotEditorFileBridgeHostFixture fixture = GodotEditorFileBridgeHostFixture.Create();
        using GodotEditorFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        fixture.WriteSystemCommand(
            "editor-automation-001",
            "ping",
            YokiFrameCommandSourceContract.EXTERNAL_AUTOMATION);

        Assert.Equal(1, host.ProcessPendingCommands());

        var response = fixture.ReadObject(fixture.GetResponsePath("editor-automation-001"));
        Assert.Equal("Success", response["status"]?.GetValue<string>());
    }

    /// <summary>
    /// 验证未登记的历史自动化来源在 Editor Host 返回明确策略拒绝终态。
    /// </summary>
    [Fact]
    public void UnregisteredAutomationSourceProducesEditorPolicyRejectedResponse()
    {
        using GodotEditorFileBridgeHostFixture fixture = GodotEditorFileBridgeHostFixture.Create();
        using GodotEditorFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        fixture.WriteSystemCommand("editor-legacy-source-001", "ping", "legacy-automation");

        Assert.Equal(1, host.ProcessPendingCommands());

        var response = fixture.ReadObject(fixture.GetResponsePath("editor-legacy-source-001"));
        Assert.Equal("Error", response["status"]?.GetValue<string>());
        Assert.Equal("PolicyRejected", response["errorCode"]?.GetValue<string>());
    }
}
