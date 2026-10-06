namespace YokiFrame.EngineKit.Tests;

/// <summary>
/// 覆盖评审复现的目标绑定问题：组合 target、豁免越权、转义键名与载荷校验。
/// </summary>
public sealed class EngineTargetBindingTests
{
    private static RecordingEngineOperation Bound(
        string action,
        YokiFrameCommandKind kind,
        YokiFrameEngineExecutionTarget targets,
        bool cancellation = false)
    {
        return new RecordingEngineOperation(action, kind, false, cancellation, targets);
    }

    [Fact]
    public void Combined_request_target_is_rejected()
    {
        RecordingEngineOperation operation = Bound(
            "scene_query",
            YokiFrameCommandKind.ReadOnly,
            YokiFrameEngineExecutionTarget.Editor | YokiFrameEngineExecutionTarget.Play);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            YokiFrameEngineSettingsSnapshot.Enabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "scene_query", "{\"target\":\"editor|play\"}"));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }

    [Fact]
    public void Escaped_target_key_is_still_honored()
    {
        RecordingEngineOperation operation = Bound(
            "scene_query",
            YokiFrameCommandKind.ReadOnly,
            YokiFrameEngineExecutionTarget.Editor);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            YokiFrameEngineSettingsSnapshot.Enabled(), out _, operation);

        // 转义键名 \u0074arget 解码后就是 target；快速扫描会漏掉它，DOM 读取不会。
        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "scene_query", "{\"\\u0074arget\":\"play\"}"));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.UNSUPPORTED, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }

    [Theory]
    [InlineData("{\"target\":")]
    [InlineData("3")]
    public void Malformed_payload_is_rejected(string payload)
    {
        RecordingEngineOperation operation = Bound(
            "scene_query",
            YokiFrameCommandKind.ReadOnly,
            YokiFrameEngineExecutionTargets.ALL);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            YokiFrameEngineSettingsSnapshot.Enabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "scene_query", payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }

    [Fact]
    public void Cancellation_exemption_skips_only_the_switch()
    {
        RecordingEngineOperation operation = Bound(
            "entry_cancel",
            YokiFrameCommandKind.UserAction,
            YokiFrameEngineExecutionTargets.ALL,
            cancellation: true);

        // 关闭开关时仍可取消：豁免生效。
        YokiFrameCommandDispatcher disabledHost = EnginePipeline.CreateDispatcherWithHostTargets(
            YokiFrameEngineSettingsSnapshot.Disabled(),
            YokiFrameEngineExecutionTarget.Editor | YokiFrameEngineExecutionTarget.Play,
            out _,
            operation);
        Assert.True(disabledHost.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "entry_cancel")).IsSuccess);
        Assert.Equal(1, operation.InvocationCount);

        // 但不承载 runtime 的宿主上请求 runtime 必须被拒：豁免不跳过宿主校验。
        YokiFrameCommandResult runtimeResult = disabledHost.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "entry_cancel", "{\"target\":\"runtime\"}"));
        Assert.False(runtimeResult.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.UNAVAILABLE, runtimeResult.ErrorCode);
        Assert.Equal(1, operation.InvocationCount);
    }

    [Fact]
    public void Target_agnostic_diagnostic_ignores_host_binding_but_still_validates_payload()
    {
        var operation = new RecordingEngineOperation(
            "engine_capabilities",
            YokiFrameCommandKind.ReadOnly,
            isDiagnostic: true,
            isCancellation: false,
            targets: YokiFrameEngineExecutionTargets.ALL,
            isTargetAgnostic: true);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcherWithHostTargets(
            YokiFrameEngineSettingsSnapshot.Enabled(),
            YokiFrameEngineExecutionTarget.Editor,
            out _,
            operation);

        Assert.True(dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "engine_capabilities", "{\"target\":\"runtime\"}")).IsSuccess);

        YokiFrameCommandResult invalid = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "engine_capabilities", "{\"target\":\"bogus\"}"));
        Assert.False(invalid.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, invalid.ErrorCode);
    }
}
