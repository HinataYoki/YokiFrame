namespace YokiFrame.RoslynKit.Tests;

/// <summary>
/// 验证关闭状态下的例外清单（设计文档 §9 决议 C）：
/// 诊断/查询与取消类操作在关闭时仍可用，Dangerous 操作永不被豁免。
/// </summary>
public sealed class EngineSwitchExemptionTests
{
    /// <summary>构造一个标记为取消、但不是诊断的用户操作。不注册、不执行。</summary>
    /// <returns>尚未被调度的 entry_cancel 操作。</returns>
    private static RecordingEngineOperation Cancellation()
    {
        return new RecordingEngineOperation("entry_cancel", YokiFrameCommandKind.UserAction, isDiagnostic: false, isCancellation: true);
    }

    /// <summary>开关关闭时取消操作仍成功执行一次。不改配置文件。</summary>
    [Fact]
    public void Cancellation_stays_available_while_disabled()
    {
        RecordingEngineOperation operation = Cancellation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Disabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "entry_cancel"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    /// <summary>配置缺失时取消操作仍成功执行一次。不把缺失配置当成禁用拒绝。</summary>
    [Fact]
    public void Cancellation_stays_available_when_config_is_missing()
    {
        RecordingEngineOperation operation = Cancellation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.MissingConfig("file not found"), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "entry_cancel"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    /// <summary>配置无效时取消操作仍成功执行一次。不因无效 JSON 拒绝取消。</summary>
    [Fact]
    public void Cancellation_stays_available_when_config_is_invalid()
    {
        RecordingEngineOperation operation = Cancellation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.InvalidConfig("broken json"), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "entry_cancel"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    /// <summary>Dangerous 操作即使同时标成诊断和取消，关闭时也被拒绝且不执行。</summary>
    [Fact]
    public void Dangerous_operation_can_never_exempt_itself()
    {
        var operation = new RecordingEngineOperation(
            "entry_run",
            YokiFrameCommandKind.Dangerous,
            isDiagnostic: true,
            isCancellation: true);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Disabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "entry_run", "{\"confirmed\":true}"));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.OPERATION_DISABLED, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }

    /// <summary>普通用户操作在关闭时被拒绝，错误码为禁用，且调用次数为零。</summary>
    [Fact]
    public void Plain_user_action_is_blocked_while_disabled()
    {
        var operation = new RecordingEngineOperation("asset_ops", YokiFrameCommandKind.UserAction);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Disabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "asset_ops"));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.OPERATION_DISABLED, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }
}
