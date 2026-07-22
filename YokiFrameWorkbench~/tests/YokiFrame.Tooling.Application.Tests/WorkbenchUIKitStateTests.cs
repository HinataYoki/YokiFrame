using YokiFrame.Tooling.Application.Models.UIKit;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>验证 Unity UIKit snapshot 到 Workbench 强类型状态的字段和坏数据边界。</summary>
public sealed class WorkbenchUIKitStateTests
{
    /// <summary>验证 Root、统计、缓存、Modal、Panel、Stack 和截断元数据均能解析。</summary>
    [Fact]
    public void ParseState_ReadsRuntimeCollectionsAndCoverage()
    {
        WorkbenchUIKitState state = WorkbenchUIKitStateParser.Parse(CreateSource(
            "{\"schemaVersion\":1,\"kit\":\"UIKit\",\"root\":{\"exists\":true},"
            + "\"stats\":{\"panelCount\":3,\"stackCount\":2,\"stackMembershipCount\":4,\"states\":{\"preloaded\":1,\"opening\":0,\"open\":1,\"hiding\":0,\"hidden\":1,\"closing\":0,\"cached\":0,\"closed\":0}},"
            + "\"cache\":{\"capacity\":16,\"transient\":1,\"reusable\":1,\"reusableCached\":0,\"persistent\":1},"
            + "\"modal\":{\"blockerActive\":true,\"panelCount\":1},"
            + "\"panels\":{\"items\":[{\"type\":\"Game.MainPanel\",\"name\":\"Main\",\"state\":\"Open\",\"level\":\"Common\",\"levelOrder\":20,\"subLevel\":2,\"cachePolicy\":\"Persistent\",\"modal\":true,\"stack\":\"main\"}],\"total\":3,\"returned\":1,\"truncated\":true},"
            + "\"stacks\":{\"items\":[{\"name\":\"main\",\"depth\":2,\"topPanelType\":\"Game.MainPanel\",\"topPanelName\":\"Main\"}],\"total\":2,\"returned\":1,\"truncated\":true}}"));

        Assert.True(state.Root.Exists);
        Assert.Equal(3, state.Stats.PanelCount);
        Assert.Equal(1, state.Stats.States.Preloaded);
        Assert.Equal(16, state.Cache.Capacity);
        Assert.True(state.Modal.BlockerActive);
        WorkbenchUIKitPanel panel = Assert.Single(state.Panels);
        Assert.Equal("Game.MainPanel", panel.Type);
        Assert.Equal("main", panel.StackName);
        WorkbenchUIKitStack stack = Assert.Single(state.Stacks);
        Assert.Equal(2, stack.Depth);
        Assert.Equal(3, state.PanelTotal);
        Assert.Equal(1, state.PanelReturned);
        Assert.True(state.PanelsTruncated);
        Assert.True(state.StacksTruncated);
    }

    /// <summary>验证顶层对象缺失时不会把协议漂移误报成健康的全零状态。</summary>
    [Fact]
    public void ParseState_MissingObjects_PreservesStaleReason()
    {
        WorkbenchUIKitState state = WorkbenchUIKitStateParser.Parse(
            CreateSource("{\"schemaVersion\":1,\"root\":{\"exists\":true}}"));

        Assert.Contains("required objects", state.StaleReason, StringComparison.OrdinalIgnoreCase);
        Assert.False(state.Root.Exists);
        Assert.Empty(state.Panels);
        Assert.Empty(state.Stacks);
    }

    /// <summary>创建带固定来源身份的 UIKit 解析输入。</summary>
    private static WorkbenchUIKitDataSource CreateSource(string payload)
    {
        return new WorkbenchUIKitDataSource(
            "unity-editor",
            "uikit-session",
            7L,
            "PlayMode",
            DateTimeOffset.Parse("2026-07-20T08:00:00Z"),
            "telemetry",
            string.Empty,
            new[] { "Global\\YokiFrame.UIKit" },
            string.Empty,
            payload);
    }
}
