using YokiFrame.Tooling.Application.Models.FsmKit;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 FsmKit Workbench 展示文案与详情合并规则的纯函数行为。
/// </summary>
public sealed class WorkbenchFsmKitPresentationAndRulesTests
{
    private const string DetailsPayload =
        "{\"fsmName\":\"Demo\",\"instanceId\":\"a\",\"fsms\":[{\"instanceId\":\"a\",\"name\":\"Demo\",\"machineState\":\"Running\",\"currentState\":\"A\",\"currentStateId\":1,\"stateCount\":1}],\"count\":1,\"selected\":{\"fsmName\":\"Demo\",\"instanceId\":\"a\",\"machineState\":\"Running\",\"currentState\":\"A\",\"currentStateId\":1,\"stateCount\":1,\"states\":[]},\"history\":{\"history\":[],\"count\":0},\"stateEvents\":{\"events\":[],\"count\":0}}";

    private const string OverviewPayload =
        "{\"fsmName\":\"Demo\",\"instanceId\":\"overview\",\"fsms\":[{\"instanceId\":\"a\",\"name\":\"Demo\",\"machineState\":\"Running\",\"currentState\":\"A\",\"currentStateId\":1,\"stateCount\":1}],\"count\":1,\"history\":{\"history\":[],\"count\":0},\"stateEvents\":{\"events\":[],\"count\":0}}";

    /// <summary>
    /// 验证数据通道文案按 source/transport 稳定投影。
    /// </summary>
    [Fact]
    public void CreateDataChannelTextMapsKnownSources()
    {
        Assert.Equal("Shared Memory", WorkbenchFsmKitPresentation.CreateDataChannelText("telemetry", string.Empty));
        Assert.Equal("文件 Snapshot", WorkbenchFsmKitPresentation.CreateDataChannelText("snapshot", string.Empty));
        Assert.Equal("FastChannel", WorkbenchFsmKitPresentation.CreateDataChannelText("command", "fastchannel"));
        Assert.Equal("FileBridge", WorkbenchFsmKitPresentation.CreateDataChannelText("command", "filebridge"));
        Assert.Equal("未知", WorkbenchFsmKitPresentation.CreateOptionalText(" "));
    }

    /// <summary>
    /// 验证精确详情判定要求 selected.instanceId 与期望一致。
    /// </summary>
    [Fact]
    public void ExpectedDetailsRequiresSelectedInstance()
    {
        WorkbenchFsmKitState overview = CreateState(OverviewPayload, "snapshot", DateTimeOffset.Parse("2026-07-21T00:00:00Z"));
        WorkbenchFsmKitState details = CreateState(DetailsPayload, "command", DateTimeOffset.Parse("2026-07-21T00:00:01Z"));

        Assert.False(WorkbenchFsmKitDetailsRules.IsExpectedDetailsState(overview, "a"));
        Assert.True(WorkbenchFsmKitDetailsRules.IsExpectedDetailsState(details, "a"));
        Assert.Equal("a", WorkbenchFsmKitDetailsRules.GetExpectedInstanceId(string.Empty, details));
    }

    /// <summary>
    /// 验证命名 Telemetry 权威会阻止周期 exact details 覆盖。
    /// </summary>
    [Fact]
    public void ShouldApplyExactDetailsRespectsTelemetryAuthorityAndFreshness()
    {
        WorkbenchFsmKitState older = CreateState(DetailsPayload, "command", DateTimeOffset.Parse("2026-07-21T00:00:00Z"));
        WorkbenchFsmKitState newer = CreateState(DetailsPayload, "command", DateTimeOffset.Parse("2026-07-21T00:00:02Z"));
        WorkbenchFsmKitState telemetry = CreateState(DetailsPayload, "telemetry", DateTimeOffset.UtcNow);

        Assert.True(WorkbenchFsmKitDetailsRules.ShouldApplyExactDetails(
            newer,
            "a",
            older,
            hasSequencedTelemetryAuthority: false));
        Assert.False(WorkbenchFsmKitDetailsRules.ShouldApplyExactDetails(
            newer,
            "a",
            older,
            hasSequencedTelemetryAuthority: true));
        Assert.True(WorkbenchFsmKitDetailsRules.IsSequencedTelemetryDetailsFrame(telemetry, "a"));
    }

    /// <summary>
    /// 用既有 parser 构造强类型状态，避免测试绕过 schema。
    /// </summary>
    private static WorkbenchFsmKitState CreateState(string payload, string source, DateTimeOffset updatedAtUtc)
    {
        WorkbenchFsmKitDataSource sourceData = new(
            "unity-editor",
            "session",
            1L,
            "EditMode",
            updatedAtUtc,
            source,
            string.Empty,
            Array.Empty<string>(),
            string.Empty,
            payload);
        return WorkbenchFsmKitStateParser.Parse(sourceData);
    }
}
