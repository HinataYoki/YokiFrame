namespace YokiFrame.EngineKit.Tests;

/// <summary>
/// 验证关闭状态下的例外清单（设计文档 §9 决议 C）：
/// 诊断/查询与取消类操作在关闭时仍可用，Dangerous 操作永不被豁免。
/// </summary>
public sealed class EngineSwitchExemptionTests
{
    private static RecordingEngineOperation Cancellation()
    {
        return new RecordingEngineOperation("entry_cancel", YokiFrameCommandKind.UserAction, isDiagnostic: false, isCancellation: true);
    }

    [Fact]
    public void Cancellation_stays_available_while_disabled()
    {
        RecordingEngineOperation operation = Cancellation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            YokiFrameEngineSettingsSnapshot.Disabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "entry_cancel"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    [Fact]
    public void Cancellation_stays_available_when_config_is_missing()
    {
        RecordingEngineOperation operation = Cancellation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            YokiFrameEngineSettingsSnapshot.MissingConfig("file not found"), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "entry_cancel"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    [Fact]
    public void Cancellation_stays_available_when_config_is_invalid()
    {
        RecordingEngineOperation operation = Cancellation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            YokiFrameEngineSettingsSnapshot.InvalidConfig("broken json"), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "entry_cancel"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    [Fact]
    public void Dangerous_operation_can_never_exempt_itself()
    {
        var operation = new RecordingEngineOperation(
            "entry_run",
            YokiFrameCommandKind.Dangerous,
            isDiagnostic: true,
            isCancellation: true);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            YokiFrameEngineSettingsSnapshot.Disabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "entry_run", "{\"confirmed\":true}"));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.OPERATION_DISABLED, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }

    [Fact]
    public void Plain_user_action_is_blocked_while_disabled()
    {
        var operation = new RecordingEngineOperation("asset_ops", YokiFrameCommandKind.UserAction);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            YokiFrameEngineSettingsSnapshot.Disabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "asset_ops"));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.OPERATION_DISABLED, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }
}
