namespace YokiFrame.RoslynKit.Tests;

/// <summary>
/// 验证配置文件读取端口：缺失、解析失败、明确关闭与开启，以及顶层其它小节的干扰。
/// </summary>
public sealed class EngineSettingsSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "yokiframe-engine-settings-tests",
        Guid.NewGuid().ToString("N"));

    public EngineSettingsSourceTests()
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

    private RoslynJsonSettingsSource SourceFor()
    {
        return new RoslynJsonSettingsSource(Path.Combine(_root, "editor-settings.json"));
    }

    private void WriteSettings(string json)
    {
        File.WriteAllText(Path.Combine(_root, "editor-settings.json"), json);
    }

    [Fact]
    public void Missing_file_reports_missing_config()
    {
        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.MissingConfig, snapshot.State);
        Assert.True(snapshot.BlocksExecution);
    }

    [Fact]
    public void Enabled_section_reports_enabled()
    {
        WriteSettings("{\"roslynOperations\":{\"enabled\":true}}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Enabled, snapshot.State);
        Assert.False(snapshot.BlocksExecution);
    }

    [Fact]
    public void Disabled_section_reports_disabled()
    {
        WriteSettings("{\"roslynOperations\":{\"enabled\":false}}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Disabled, snapshot.State);
        Assert.True(snapshot.BlocksExecution);
    }

    [Fact]
    public void Missing_section_reports_disabled()
    {
        WriteSettings("{\"logKit\":{\"saveInEditor\":true}}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Disabled, snapshot.State);
    }

    [Fact]
    public void Non_object_section_reports_invalid_config()
    {
        WriteSettings("{\"roslynOperations\":3}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.InvalidConfig, snapshot.State);
        Assert.True(snapshot.BlocksExecution);
    }

    [Fact]
    public void Empty_file_reports_invalid_config()
    {
        WriteSettings(string.Empty);

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.InvalidConfig, snapshot.State);
    }

    [Fact]
    public void Nested_sections_do_not_shadow_the_engine_section()
    {
        WriteSettings("{\"logKit\":{\"enabled\":false},\"roslynOperations\":{\"enabled\":true}}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Enabled, snapshot.State);
    }

    [Fact]
    public void Project_settings_schema_enables_engine_operations()
    {
        WriteSettings("{\"formatVersion\":1,\"settings\":[{\"kit\":\"AudioKit\",\"key\":\"index.startId\",\"value\":\"1001\"},{\"kit\":\"RoslynKit\",\"key\":\"operations.enabled\",\"value\":\"true\"}]}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Enabled, snapshot.State);
    }

    [Fact]
    public void Project_settings_schema_can_disable_engine_operations()
    {
        WriteSettings("{\"formatVersion\":1,\"settings\":[{\"kit\":\"RoslynKit\",\"key\":\"operations.enabled\",\"value\":\"false\"}]}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Disabled, snapshot.State);
    }

    [Fact]
    public void Legacy_engine_kit_name_is_not_accepted()
    {
        WriteSettings("{\"formatVersion\":1,\"settings\":[{\"kit\":\"Engine\",\"key\":\"operations.enabled\",\"value\":\"true\"}]}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Disabled, snapshot.State);
    }

    [Fact]
    public void Project_settings_without_engine_entry_reports_disabled()
    {
        WriteSettings("{\"formatVersion\":1,\"settings\":[{\"kit\":\"AudioKit\",\"key\":\"index.startId\",\"value\":\"1001\"}]}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Disabled, snapshot.State);
    }

    [Fact]
    public void Malformed_project_settings_file_reports_invalid_config()
    {
        WriteSettings("{\"formatVersion\":1,\"settings\":[");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.InvalidConfig, snapshot.State);
    }

    [Fact]
    public void Non_boolean_enabled_value_reports_disabled()
    {
        WriteSettings("{\"roslynOperations\":{\"enabled\":\"true\"}}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Disabled, snapshot.State);
    }
}
