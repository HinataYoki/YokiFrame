namespace YokiFrame.RoslynKit.Tests;

/// <summary>
/// 覆盖评审复现的目标绑定问题：组合 target、豁免越权、转义键名与载荷校验。
/// </summary>
public sealed class EngineTargetBindingTests
{
    /// <summary>构造绑定到指定目标的记录型操作。不注册、不执行。</summary>
    /// <param name="action">操作名。</param>
    /// <param name="kind">命令种类。</param>
    /// <param name="targets">操作声明支持的目标。</param>
    /// <param name="cancellation">是否标记为取消豁免。</param>
    /// <returns>尚未调度的操作。</returns>
    private static RecordingEngineOperation Bound(
        string action,
        YokiFrameCommandKind kind,
        RoslynExecutionTarget targets,
        bool cancellation = false)
    {
        return new RecordingEngineOperation(action, kind, false, cancellation, targets);
    }

    /// <summary>请求里的组合 target 被拒绝为 INVALID_PAYLOAD，且不执行。</summary>
    [Fact]
    public void Combined_request_target_is_rejected()
    {
        RecordingEngineOperation operation = Bound(
            "scene_query",
            YokiFrameCommandKind.ReadOnly,
            RoslynExecutionTarget.Editor | RoslynExecutionTarget.Play);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Enabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "scene_query", "{\"target\":\"editor|play\"}"));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.INVALID_PAYLOAD, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }

    /// <summary>Unicode 转义后的 target 键仍按 play 校验，操作不支持时返回 UNSUPPORTED 且不执行。</summary>
    [Fact]
    public void Escaped_target_key_is_still_honored()
    {
        RecordingEngineOperation operation = Bound(
            "scene_query",
            YokiFrameCommandKind.ReadOnly,
            RoslynExecutionTarget.Editor);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Enabled(), out _, operation);

        // 转义键名 \u0074arget 解码后就是 target；快速扫描会漏掉它，DOM 读取不会。
        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "scene_query", "{\"\\u0074arget\":\"play\"}"));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.UNSUPPORTED, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }

    /// <summary>截断或非对象载荷返回 INVALID_PAYLOAD，且不执行。</summary>
    /// <param name="payload">故意损坏的请求 JSON。</param>
    [Theory]
    [InlineData("{\"target\":")]
    [InlineData("3")]
    public void Malformed_payload_is_rejected(string payload)
    {
        RecordingEngineOperation operation = Bound(
            "scene_query",
            YokiFrameCommandKind.ReadOnly,
            RoslynExecutionTargets.ALL);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Enabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "scene_query", payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.INVALID_PAYLOAD, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }

    /// <summary>取消豁免只跳过开关，不跳过宿主目标校验。runtime 请求仍不可用，且只执行一次。</summary>
    [Fact]
    public void Cancellation_exemption_skips_only_the_switch()
    {
        RecordingEngineOperation operation = Bound(
            "entry_cancel",
            YokiFrameCommandKind.UserAction,
            RoslynExecutionTargets.ALL,
            cancellation: true);

        // 关闭开关时仍可取消：豁免生效。
        YokiFrameCommandDispatcher disabledHost = EnginePipeline.CreateDispatcherWithHostTargets(
            RoslynSettingsSnapshot.Disabled(),
            RoslynExecutionTarget.Editor | RoslynExecutionTarget.Play,
            out _,
            operation);
        Assert.True(disabledHost.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "entry_cancel")).IsSuccess);
        Assert.Equal(1, operation.InvocationCount);

        // 但不承载 runtime 的宿主上请求 runtime 必须被拒：豁免不跳过宿主校验。
        YokiFrameCommandResult runtimeResult = disabledHost.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "entry_cancel", "{\"target\":\"runtime\"}"));
        Assert.False(runtimeResult.IsSuccess);
        Assert.Equal(RoslynErrorCodes.UNAVAILABLE, runtimeResult.ErrorCode);
        Assert.Equal(1, operation.InvocationCount);
    }

    /// <summary>与目标无关的诊断忽略宿主绑定，但仍拒绝非法 target。合法 runtime 请求会执行。</summary>
    [Fact]
    public void Target_agnostic_diagnostic_ignores_host_binding_but_still_validates_payload()
    {
        var operation = new RecordingEngineOperation(
            "engine_capabilities",
            YokiFrameCommandKind.ReadOnly,
            isDiagnostic: true,
            isCancellation: false,
            targets: RoslynExecutionTargets.ALL,
            isTargetAgnostic: true);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcherWithHostTargets(
            RoslynSettingsSnapshot.Enabled(),
            RoslynExecutionTarget.Editor,
            out _,
            operation);

        Assert.True(dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "engine_capabilities", "{\"target\":\"runtime\"}")).IsSuccess);

        YokiFrameCommandResult invalid = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "engine_capabilities", "{\"target\":\"bogus\"}"));
        Assert.False(invalid.IsSuccess);
        Assert.Equal(RoslynErrorCodes.INVALID_PAYLOAD, invalid.ErrorCode);
    }
}
