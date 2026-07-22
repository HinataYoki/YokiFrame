using YokiFrame;

namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// 覆盖 Godot Runtime 为 capability catalog 提供的实时命令目录。
/// </summary>
public sealed partial class GodotFileBridgeHostTests
{
    /// <summary>
    /// 验证 System/bridge_status 返回协议计数和明确的 FileBridge fallback 状态。
    /// </summary>
    [Fact]
    public void BridgeStatusReturnsProtocolDiagnosticsAndFallbackState()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        _ = fixture.WriteSystemCommand("status-001", "bridge_status");

        _ = host.ProcessPendingCommands();

        var response = GodotFileBridgeHostFixture.ReadObject(fixture.GetResponsePath("status-001"));
        var result = System.Text.Json.Nodes.JsonNode.Parse(response["resultJson"]?.GetValue<string>() ?? "{}")?.AsObject();
        Assert.Equal("Success", response["status"]?.GetValue<string>());
        Assert.Equal("filebridge-fallback", result?["fastChannel"]?.GetValue<string>());
        Assert.True(result?["protocolFileCount"]?.GetValue<int>() > 0);
        Assert.True(result?["protocolBytes"]?.GetValue<long>() > 0);
        Assert.Equal(GodotFileBridgeHostFixture.ENGINE_ID, result?["engineId"]?.GetValue<string>());
    }

    /// <summary>
    /// 验证 Godot Runtime 暴露与实际 policy 一致的 System/list_commands 目录，供 harness catalog refresh 使用。
    /// </summary>
    [Fact]
    public void ListCommandsReturnsCurrentGodotPolicy()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        _ = fixture.WriteSystemCommand("commands-001", "list_commands");

        _ = host.ProcessPendingCommands();

        var response = GodotFileBridgeHostFixture.ReadObject(fixture.GetResponsePath("commands-001"));
        var result = System.Text.Json.Nodes.JsonNode.Parse(response["resultJson"]?.GetValue<string>() ?? "{}")?.AsObject();
        Assert.Equal("Success", response["status"]?.GetValue<string>());
        Assert.Equal(GodotFileBridgeHostFixture.ENGINE_ID, result?["engineId"]?.GetValue<string>());
        Assert.Equal(host.SessionId, result?["sessionId"]?.GetValue<string>());
        Assert.Equal(host.Generation, result?["generation"]?.GetValue<long>());
        var kits = result?["kits"]?.AsArray();
        Assert.NotNull(kits);
        Assert.Contains(kits!, kit => kit?["kit"]?.GetValue<string>() == "System");
        Assert.Contains(kits!, kit => kit?["kit"]?.GetValue<string>() == "FsmKit");
        var system = kits!.First(kit => kit?["kit"]?.GetValue<string>() == "System")?.AsObject();
        Assert.NotNull(system);
        var actions = system!["actions"]?.AsArray();
        Assert.NotNull(actions);
        Assert.Contains(actions!, action => action?["action"]?.GetValue<string>() == "list_commands");
    }
}
