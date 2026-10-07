using YokiFrame.Json;

namespace YokiFrame.RoslynKit.Tests;

/// <summary>
/// 验证 target 解析与两级可用性判定：操作是否支持该目标、宿主是否能承载该目标。
/// </summary>
public sealed class EngineTargetRoutingTests
{
    private static RecordingEngineOperation Operation(RoslynExecutionTarget targets)
    {
        return new RecordingEngineOperation("scene_query", YokiFrameCommandKind.ReadOnly, false, false, targets);
    }

    private static RoslynExecutionTarget TargetOf(string json)
    {
        RoslynExecutionTarget target = RoslynExecutionTarget.None;
        Assert.True(RoslynExecutionTargets.TryParse(json, out target));
        return target;
    }

    [Theory]
    [InlineData("{\"target\":\"play\"}", true)]
    [InlineData("{\"target\":\"editor\"}", true)]
    [InlineData("{}", true)]
    public void Requested_target_supported_by_action_executes(string payload, bool expectedSuccess)
    {
        RecordingEngineOperation operation = Operation(
            RoslynExecutionTarget.Editor | RoslynExecutionTarget.Play);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Enabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "scene_query", payload));

        Assert.Equal(expectedSuccess, result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    [Fact]
    public void Target_not_supported_by_action_is_rejected()
    {
        RecordingEngineOperation operation = Operation(RoslynExecutionTarget.Editor);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Enabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "scene_query", "{\"target\":\"play\"}"));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.UNSUPPORTED, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }

    [Fact]
    public void Target_supported_by_action_but_not_by_host_is_unavailable()
    {
        RecordingEngineOperation operation = Operation(RoslynExecutionTargets.ALL);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcherWithHostTargets(
            RoslynSettingsSnapshot.Enabled(),
            RoslynExecutionTarget.Editor | RoslynExecutionTarget.Play,
            out _,
            operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "scene_query", "{\"target\":\"runtime\"}"));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.UNAVAILABLE, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }

    [Theory]
    [InlineData("{\"target\":\"bogus\"}")]
    [InlineData("{\"target\":3}")]
    public void Invalid_target_value_is_rejected(string payload)
    {
        RecordingEngineOperation operation = Operation(RoslynExecutionTargets.ALL);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Enabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "scene_query", payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.INVALID_PAYLOAD, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }

    [Fact]
    public void Target_validation_happens_after_switch_and_source_checks()
    {
        var operation = new RecordingEngineOperation(
            "entry_run",
            YokiFrameCommandKind.Dangerous,
            false,
            false,
            RoslynExecutionTarget.Editor);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Enabled(), out _, operation);

        // 来源不受信 + 目标也不被操作支持：必须先返回来源错误（③ 早于 ④）。
        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CodexSource, "entry_run", "{\"confirmed\":true,\"target\":\"play\"}"));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.SOURCE_NOT_PERMITTED, result.ErrorCode);
    }

    [Fact]
    public void Target_formatting_round_trips()
    {
        RoslynExecutionTarget target =
            RoslynExecutionTarget.Editor | RoslynExecutionTarget.Runtime;

        Assert.Equal("editor|runtime", RoslynExecutionTargets.Format(target));
        Assert.Equal(target, TargetOf("editor|runtime"));
        Assert.Equal(target, TargetOf("RUNTIME,EDITOR"));
    }
}
