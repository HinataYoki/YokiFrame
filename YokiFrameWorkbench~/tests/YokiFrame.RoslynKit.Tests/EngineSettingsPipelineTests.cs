namespace YokiFrame.RoslynKit.Tests;

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

    /// <summary>创建本用例独占的临时目录。不写配置文件。</summary>
    public EngineSettingsPipelineTests()
    {
        Directory.CreateDirectory(_root);
    }

    /// <summary>删除临时目录。遇到 IOException 时忽略，避免清理失败覆盖断言。</summary>
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

    /// <summary>覆盖写入测试配置。不创建额外备份。</summary>
    /// <param name="json">完整配置文本。</param>
    private void WriteSettings(string json)
    {
        File.WriteAllText(SettingsPath, json);
    }

    /// <summary>用当前配置文件装配调度管线。每次调用都重新读取文件。</summary>
    /// <param name="operations">要注册的记录型操作。</param>
    /// <returns>可直接 Dispatch 的调度器。</returns>
    private YokiFrameCommandDispatcher CreateDispatcher(params RecordingEngineOperation[] operations)
    {
        var gate = RoslynGate.CreateDefault(new RoslynJsonSettingsSource(SettingsPath));
        var handler = new RoslynCommandHandler(gate, operations);
        var policy = YokiFrameCommandPolicy.CreateWithDefaultSources(handler.Descriptors.ToArray());
        return new YokiFrameCommandDispatcher(policy, new IYokiFrameCommandHandler[] { handler });
    }

    /// <summary>配置明确开启时危险操作执行一次。不绕过来源校验。</summary>
    [Fact]
    public void Enabled_file_allows_execution()
    {
        WriteSettings("{\"roslynOperations\":{\"enabled\":true}}");
        var operation = new RecordingEngineOperation("play_control", YokiFrameCommandKind.Dangerous);
        YokiFrameCommandDispatcher dispatcher = CreateDispatcher(operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "play_control", DangerousPayload));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, operation.InvocationCount);
    }

    /// <summary>关闭或无法解析的配置拒绝执行，错误信息带上对应状态，且调用次数为零。</summary>
    /// <param name="expectedState">期望出现在错误信息中的状态名。</param>
    /// <param name="json">写入磁盘的配置文本。</param>
    [Theory]
    [InlineData("Disabled", "{\"roslynOperations\":{\"enabled\":false}}")]
    [InlineData("InvalidConfig", "{\"roslynOperations\":3}")]
    public void Blocking_settings_file_rejects_execution_with_reason(string expectedState, string json)
    {
        WriteSettings(json);
        var operation = new RecordingEngineOperation("play_control", YokiFrameCommandKind.Dangerous);
        YokiFrameCommandDispatcher dispatcher = CreateDispatcher(operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "play_control", DangerousPayload));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.OPERATION_DISABLED, result.ErrorCode);
        Assert.Contains(expectedState, result.ErrorMessage);
        Assert.Equal(0, operation.InvocationCount);
    }

    /// <summary>配置文件不存在时拒绝执行，原因是 MissingConfig，且不调用操作。</summary>
    [Fact]
    public void Missing_settings_file_rejects_execution_with_reason()
    {
        var operation = new RecordingEngineOperation("play_control", YokiFrameCommandKind.Dangerous);
        YokiFrameCommandDispatcher dispatcher = CreateDispatcher(operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "play_control", DangerousPayload));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.OPERATION_DISABLED, result.ErrorCode);
        Assert.Contains("MissingConfig", result.ErrorMessage);
        Assert.Equal(0, operation.InvocationCount);
    }

    /// <summary>配置文件缺失时诊断操作仍成功执行一次。不因缺文件被禁用。</summary>
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

    /// <summary>配置开启后，不受信来源仍被拒绝且不执行。来源限制不随开关放行。</summary>
    [Fact]
    public void Source_restriction_still_applies_when_settings_enable_execution()
    {
        WriteSettings("{\"roslynOperations\":{\"enabled\":true}}");
        var operation = new RecordingEngineOperation("play_control", YokiFrameCommandKind.Dangerous);
        YokiFrameCommandDispatcher dispatcher = CreateDispatcher(operation);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            EnginePipeline.CreateRequest(EnginePipeline.CodexSource, "play_control", DangerousPayload));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.SOURCE_NOT_PERMITTED, result.ErrorCode);
        Assert.Equal(0, operation.InvocationCount);
    }
}
