using YokiFrame.Json;

namespace YokiFrame.RoslynKit.Tests;

/// <summary>
/// 验证 target 解析与两级可用性判定：操作是否支持该目标、宿主是否能承载该目标。
/// </summary>
public sealed class EngineTargetRoutingTests
{
    /// <summary>构造只读 scene_query，并限定它支持的目标。不执行。</summary>
    /// <param name="targets">操作声明支持的目标。</param>
    /// <returns>尚未调度的记录型操作。</returns>
    private static RecordingEngineOperation Operation(RoslynExecutionTarget targets)
    {
        return new RecordingEngineOperation("scene_query", YokiFrameCommandKind.ReadOnly, false, false, targets);
    }

    /// <summary>解析目标文本。解析失败时断言失败，不返回 None 冒充成功。</summary>
    /// <param name="json">目标文本，可以是别名或组合。</param>
    /// <returns>解析得到的目标标志。</returns>
    private static RoslynExecutionTarget TargetOf(string json)
    {
        RoslynExecutionTarget target = RoslynExecutionTarget.None;
        Assert.True(RoslynExecutionTargets.TryParse(json, out target));
        return target;
    }

    /// <summary>操作和宿主都支持请求目标时执行一次。空 payload 也按成功期望断言。</summary>
    /// <param name="payload">包含 target 或为空的请求 JSON。</param>
    /// <param name="expectedSuccess">期望的成功标记。</param>
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

    /// <summary>操作不支持 play 时返回 UNSUPPORTED，且不执行。开关保持开启。</summary>
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

    /// <summary>操作支持 runtime 但宿主不承载时返回 UNAVAILABLE，且不执行。</summary>
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

    /// <summary>无法识别的 target 文本或类型返回 INVALID_PAYLOAD，且不执行。</summary>
    /// <param name="payload">含非法 target 的请求 JSON。</param>
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

    /// <summary>来源校验早于目标校验。不受信来源即使目标也不支持，仍返回来源错误且不执行。</summary>
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

    /// <summary>目标格式化可被大小写和分隔符变体解析回来。不改变标志组合。</summary>
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
