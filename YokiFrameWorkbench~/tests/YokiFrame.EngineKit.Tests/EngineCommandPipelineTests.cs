namespace YokiFrame.EngineKit.Tests;

/// <summary>
/// 验证 Engine Kit 的策略边界：真实 Dispatcher → Policy → Gate 的错误码、先后顺序与零执行保证。
/// </summary>
public sealed class EngineCommandPipelineTests
{
    private const string DangerousPayload = "{\"confirmed\":true}";

    private static RecordingEngineOperation DangerousOperation()
    {
        return new RecordingEngineOperation("play_control", YokiFrameCommandKind.Dangerous);
    }

    private static RecordingEngineOperation DiagnosticOperation()
    {
        return new RecordingEngineOperation("engine_capabilities", YokiFrameCommandKind.ReadOnly, isDiagnostic: true);
    }

    [Fact]
    public void Dangerous_from_cli_with_confirmed_executes()
    {
        RecordingEngineOperation operation = DangerousOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            YokiFrameEngineSettingsSnapshot.Enabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "play_control", DangerousPayload));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    [Fact]
    public void Dangerous_from_workbench_with_confirmed_executes()
    {
        RecordingEngineOperation operation = DangerousOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            YokiFrameEngineSettingsSnapshot.Enabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.WorkbenchSource, "play_control", DangerousPayload));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    [Theory]
    [InlineData("codex")]
    [InlineData("external-automation")]
    public void Dangerous_from_untrusted_source_is_rejected_by_gate_without_executing(string source)
    {
        RecordingEngineOperation operation = DangerousOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            YokiFrameEngineSettingsSnapshot.Enabled(), out StubEngineSettingsSource settings, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(source, "play_control", DangerousPayload));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.SOURCE_NOT_PERMITTED, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
        Assert.Equal(1, settings.ReadCount);
    }

    [Fact]
    public void Dangerous_without_confirmed_is_rejected_by_policy_before_gate_is_consulted()
    {
        RecordingEngineOperation operation = DangerousOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            YokiFrameEngineSettingsSnapshot.Enabled(), out StubEngineSettingsSource settings, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CodexSource, "play_control", "{}"));

        Assert.False(result.IsSuccess);
        Assert.Equal("ConfirmationRequired", result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
        Assert.Equal(0, settings.ReadCount);
    }

    [Fact]
    public void Execution_switch_is_evaluated_before_source_restriction()
    {
        RecordingEngineOperation operation = DangerousOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            YokiFrameEngineSettingsSnapshot.Disabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CodexSource, "play_control", DangerousPayload));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.OPERATION_DISABLED, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }

    [Theory]
    [InlineData("Disabled")]
    [InlineData("MissingConfig")]
    [InlineData("InvalidConfig")]
    public void Blocked_switch_states_reject_execution(string stateName)
    {
        YokiFrameEngineSettingsSnapshot snapshot = stateName switch
        {
            "Disabled" => YokiFrameEngineSettingsSnapshot.Disabled("explicitly disabled"),
            "MissingConfig" => YokiFrameEngineSettingsSnapshot.MissingConfig("file not found"),
            _ => YokiFrameEngineSettingsSnapshot.InvalidConfig("broken json")
        };

        RecordingEngineOperation operation = DangerousOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(snapshot, out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "play_control", DangerousPayload));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.OPERATION_DISABLED, result.ErrorCode);
        Assert.Contains(stateName, result.ErrorMessage);
        Assert.Equal(0, operation.InvocationCount);
    }

    [Fact]
    public void Diagnostic_queries_remain_available_while_disabled()
    {
        RecordingEngineOperation operation = DiagnosticOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            YokiFrameEngineSettingsSnapshot.Disabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "engine_capabilities"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    [Fact]
    public void Diagnostic_queries_remain_available_when_config_is_invalid()
    {
        RecordingEngineOperation operation = DiagnosticOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            YokiFrameEngineSettingsSnapshot.InvalidConfig("broken json"), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CodexSource, "engine_capabilities"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    [Fact]
    public void Non_diagnostic_readonly_operation_is_blocked_while_disabled()
    {
        var operation = new RecordingEngineOperation("scene_query", YokiFrameCommandKind.ReadOnly);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            YokiFrameEngineSettingsSnapshot.Disabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "scene_query"));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.OPERATION_DISABLED, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }

    [Fact]
    public void Unregistered_engine_action_is_rejected_by_policy()
    {
        RecordingEngineOperation operation = DangerousOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            YokiFrameEngineSettingsSnapshot.Enabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "not_registered"));

        Assert.False(result.IsSuccess);
        Assert.Equal("UnknownCommand", result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }

    [Fact]
    public void Other_kits_keep_their_policy_behaviour()
    {
        // Policy 未被修改：其它 Kit 的 UserAction 对 codex 来源仍然放行，
        // 因缺少 handler 而返回 HandlerMissing，而不是被策略拒绝。
        var foreignDescriptor = new YokiFrameCommandDescriptor("LogKit", "set_settings", YokiFrameCommandKind.UserAction);
        YokiFrameCommandPolicy policy = YokiFrameCommandPolicy.CreateWithDefaultSources(new[] { foreignDescriptor });
        var dispatcher = new YokiFrameCommandDispatcher(policy, Array.Empty<IYokiFrameCommandHandler>());

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CodexSource, "set_settings", "{}", "LogKit"));

        Assert.False(result.IsSuccess);
        Assert.Equal("HandlerMissing", result.ErrorCode);
    }

    [Fact]
    public void Duplicate_engine_actions_are_rejected_at_construction()
    {
        RecordingEngineOperation first = DangerousOperation();
        RecordingEngineOperation second = DangerousOperation();
        var gate = YokiFrameEngineGate.CreateDefault(new StubEngineSettingsSource(YokiFrameEngineSettingsSnapshot.Enabled()));

        Assert.Throws<ArgumentException>(() =>
            new YokiFrameEngineCommandHandler(gate, new IYokiFrameEngineOperation[] { first, second }));
    }

    [Fact]
    public void Handler_exposes_one_descriptor_per_operation()
    {
        RecordingEngineOperation play = DangerousOperation();
        RecordingEngineOperation capabilities = DiagnosticOperation();
        var gate = YokiFrameEngineGate.CreateDefault(new StubEngineSettingsSource(YokiFrameEngineSettingsSnapshot.Enabled()));
        var handler = new YokiFrameEngineCommandHandler(
            gate,
            new IYokiFrameEngineOperation[] { play, capabilities });

        Assert.Equal(2, handler.OperationCount);
        Assert.Equal(2, handler.Descriptors.Count);
        Assert.All(handler.Descriptors, descriptor => Assert.Equal(YokiFrameEngineCommandHandler.KIT_NAME, descriptor.Kit));
    }
}
