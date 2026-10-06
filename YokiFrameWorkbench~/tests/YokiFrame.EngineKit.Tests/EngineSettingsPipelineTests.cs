namespace YokiFrame.EngineKit.Tests;

/// <summary>
/// 用真实配置文件读取端口驱动 Dispatcher → Policy → Gate，
/// 验证验收要求的三种阻断状态（关闭 / 配置缺失 / 解析失败）、诊断豁免与来源限制的组合。
/// </summary>
public sealed class EngineSettingsPipelineTests : IDisposable
{
    private const string DangerousPayload = "{\"confirmed\":true}";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "yokiframe-engine-pipeline-tests",
        Guid.NewGuid().ToString("N"));

    public EngineSettingsPipelineTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // 临时目录清理失败不影响断言结果。
        }
    }

    private string SettingsPath => Path.Combine(_root, "editor-settings.json");

    private void WriteSettings(string json)
    {
        File.WriteAllText(SettingsPath, json);
    }

    private YokiFrameCommandDispatcher CreateDispatcher(params RecordingEngineOperation[] operations)
    {
        var gate = YokiFrameEngineGate.CreateDefault(new YokiFrameEngineJsonSettingsSource(SettingsPath));
        var handler = new YokiFrameEngineCommandHandler(gate, operations);
        var policy = YokiFrameCommandPolicy.CreateWithDefaultSources(handler.Descriptors.ToArray());
        return new YokiFrameCommandDispatcher(policy, new IYokiFrameCommandHandler[] { handler });
    }

    [Fact]
    public void Enabled_file_allows_execution()
    {
        WriteSettings("{\"engineOperations\":{\"enabled\":true}}");
        var operation = new RecordingEngineOperation("play_control", YokiFrameCommandKind.Dangerous);
        YokiFrameCommandDispatcher dispatcher = CreateDispatcher(operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "play_control", DangerousPayload));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    [Theory]
    [InlineData("Disabled", "{\"engineOperations\":{\"enabled\":false}}")]
    [InlineData("InvalidConfig", "{\"engineOperations\":3}")]
    public void Blocking_settings_file_rejects_execution_with_reason(string expectedState, string json)
    {
        WriteSettings(json);
        var operation = new RecordingEngineOperation("play_control", YokiFrameCommandKind.Dangerous);
        YokiFrameCommandDispatcher dispatcher = CreateDispatcher(operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "play_control", DangerousPayload));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.OPERATION_DISABLED, result.ErrorCode);
        Assert.Contains(expectedState, result.ErrorMessage);
        Assert.Equal(0, operation.InvocationCount);
    }

    [Fact]
    public void Missing_settings_file_rejects_execution_with_reason()
    {
        var operation = new RecordingEngineOperation("play_control", YokiFrameCommandKind.Dangerous);
        YokiFrameCommandDispatcher dispatcher = CreateDispatcher(operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "play_control", DangerousPayload));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.OPERATION_DISABLED, result.ErrorCode);
        Assert.Contains("MissingConfig", result.ErrorMessage);
        Assert.Equal(0, operation.InvocationCount);
    }

    [Fact]
    public void Diagnostic_operation_stays_available_when_settings_file_is_missing()
    {
        var operation = new RecordingEngineOperation("engine_capabilities", YokiFrameCommandKind.ReadOnly, isDiagnostic: true);
        YokiFrameCommandDispatcher dispatcher = CreateDispatcher(operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "engine_capabilities"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    [Fact]
    public void Source_restriction_still_applies_when_settings_enable_execution()
    {
        WriteSettings("{\"engineOperations\":{\"enabled\":true}}");
        var operation = new RecordingEngineOperation("play_control", YokiFrameCommandKind.Dangerous);
        YokiFrameCommandDispatcher dispatcher = CreateDispatcher(operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CodexSource, "play_control", DangerousPayload));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.SOURCE_NOT_PERMITTED, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }
}
