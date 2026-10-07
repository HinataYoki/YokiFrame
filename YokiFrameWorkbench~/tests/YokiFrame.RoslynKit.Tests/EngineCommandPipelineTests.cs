namespace YokiFrame.RoslynKit.Tests;

/// <summary>
/// 验证 RoslynKit 的策略边界：真实 Dispatcher → Policy → Gate 的错误码、先后顺序与零执行保证。
/// </summary>
public sealed class EngineCommandPipelineTests
{
    private const string DangerousPayload = "{\"confirmed\":true}";

    /// <summary>构造 play_control 危险操作。不注册。</summary>
    /// <returns>调用次数为零的操作。</returns>
    private static RecordingEngineOperation DangerousOperation()
    {
        return new RecordingEngineOperation("play_control", YokiFrameCommandKind.Dangerous);
    }

    /// <summary>构造诊断型 engine_capabilities。不注册。</summary>
    /// <returns>调用次数为零的只读操作。</returns>
    private static RecordingEngineOperation DiagnosticOperation()
    {
        return new RecordingEngineOperation("engine_capabilities", YokiFrameCommandKind.ReadOnly, isDiagnostic: true);
    }

    [Fact]
    /// <summary>已确认的 cli 危险操作在开启时执行一次。</summary>
    public void Dangerous_from_cli_with_confirmed_executes()
    {
        RecordingEngineOperation operation = DangerousOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Enabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "play_control", DangerousPayload));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    [Fact]
    /// <summary>已确认的 workbench 危险操作在开启时执行一次。</summary>
    public void Dangerous_from_workbench_with_confirmed_executes()
    {
        RecordingEngineOperation operation = DangerousOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Enabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.WorkbenchSource, "play_control", DangerousPayload));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    [Theory]
    [InlineData("codex")]
    [InlineData("external-automation")]
    /// <summary>不受信来源被 Gate 拒绝且不执行。设置仍被读取一次。</summary>
    /// <param name="source">codex 或 external-automation。</param>
    public void Dangerous_from_untrusted_source_is_rejected_by_gate_without_executing(string source)
    {
        RecordingEngineOperation operation = DangerousOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Enabled(), out StubEngineSettingsSource settings, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(source, "play_control", DangerousPayload));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.SOURCE_NOT_PERMITTED, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
        Assert.Equal(1, settings.ReadCount);
    }

    [Fact]
    /// <summary>未确认时策略先拒绝，Gate 不被读取，操作也不执行。</summary>
    public void Dangerous_without_confirmed_is_rejected_by_policy_before_gate_is_consulted()
    {
        RecordingEngineOperation operation = DangerousOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Enabled(), out StubEngineSettingsSource settings, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CodexSource, "play_control", "{}"));

        Assert.False(result.IsSuccess);
        Assert.Equal("ConfirmationRequired", result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
        Assert.Equal(0, settings.ReadCount);
    }

    [Fact]
    /// <summary>开关关闭早于来源限制。不受信来源得到的是禁用错误，而不是来源错误。</summary>
    public void Execution_switch_is_evaluated_before_source_restriction()
    {
        RecordingEngineOperation operation = DangerousOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Disabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CodexSource, "play_control", DangerousPayload));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.OPERATION_DISABLED, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }

    [Theory]
    [InlineData("Disabled")]
    [InlineData("MissingConfig")]
    [InlineData("InvalidConfig")]
    /// <summary>关闭、缺失和无效配置都拒绝执行，错误信息包含状态名，且不调用操作。</summary>
    /// <param name="stateName">期望出现在错误信息中的状态名。</param>
    public void Blocked_switch_states_reject_execution(string stateName)
    {
        RoslynSettingsSnapshot snapshot = stateName switch
        {
            "Disabled" => RoslynSettingsSnapshot.Disabled("explicitly disabled"),
            "MissingConfig" => RoslynSettingsSnapshot.MissingConfig("file not found"),
            _ => RoslynSettingsSnapshot.InvalidConfig("broken json")
        };

        RecordingEngineOperation operation = DangerousOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(snapshot, out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "play_control", DangerousPayload));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.OPERATION_DISABLED, result.ErrorCode);
        Assert.Contains(stateName, result.ErrorMessage);
        Assert.Equal(0, operation.InvocationCount);
    }

    [Fact]
    /// <summary>开关关闭时诊断查询仍成功执行一次。</summary>
    public void Diagnostic_queries_remain_available_while_disabled()
    {
        RecordingEngineOperation operation = DiagnosticOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Disabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "engine_capabilities"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    [Fact]
    /// <summary>配置无效时诊断查询仍成功。来源即使是 codex 也不被这条诊断拦住。</summary>
    public void Diagnostic_queries_remain_available_when_config_is_invalid()
    {
        RecordingEngineOperation operation = DiagnosticOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.InvalidConfig("broken json"), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CodexSource, "engine_capabilities"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    [Fact]
    /// <summary>非诊断只读操作在关闭时被拒绝且不执行。</summary>
    public void Non_diagnostic_readonly_operation_is_blocked_while_disabled()
    {
        var operation = new RecordingEngineOperation("scene_query", YokiFrameCommandKind.ReadOnly);
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Disabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "scene_query"));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.OPERATION_DISABLED, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }

    [Fact]
    /// <summary>未注册动作由策略返回 UnknownCommand，且不执行已注册操作。</summary>
    public void Unregistered_engine_action_is_rejected_by_policy()
    {
        RecordingEngineOperation operation = DangerousOperation();
        YokiFrameCommandDispatcher dispatcher = EnginePipeline.CreateDispatcher(
            RoslynSettingsSnapshot.Enabled(), out _, operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "not_registered"));

        Assert.False(result.IsSuccess);
        Assert.Equal("UnknownCommand", result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }

    [Fact]
    /// <summary>其他 Kit 的用户操作仍按原策略放行到来源，最终因没有处理器返回 HandlerMissing。</summary>
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
    /// <summary>重复引擎动作在构造处理器时抛出 ArgumentException。不进入调度。</summary>
    public void Duplicate_engine_actions_are_rejected_at_construction()
    {
        RecordingEngineOperation first = DangerousOperation();
        RecordingEngineOperation second = DangerousOperation();
        var gate = RoslynGate.CreateDefault(new StubEngineSettingsSource(RoslynSettingsSnapshot.Enabled()));

        Assert.Throws<ArgumentException>(() =>
            new RoslynCommandHandler(gate, new IRoslynOperation[] { first, second }));
    }

    [Fact]
    /// <summary>处理器为每个操作暴露一个描述符，且 Kit 名都是 RoslynKit。</summary>
    public void Handler_exposes_one_descriptor_per_operation()
    {
        RecordingEngineOperation play = DangerousOperation();
        RecordingEngineOperation capabilities = DiagnosticOperation();
        var gate = RoslynGate.CreateDefault(new StubEngineSettingsSource(RoslynSettingsSnapshot.Enabled()));
        var handler = new RoslynCommandHandler(
            gate,
            new IRoslynOperation[] { play, capabilities });

        Assert.Equal(2, handler.OperationCount);
        Assert.Equal(2, handler.Descriptors.Count);
        Assert.All(handler.Descriptors, descriptor => Assert.Equal(RoslynCommandHandler.KIT_NAME, descriptor.Kit));
    }
}
