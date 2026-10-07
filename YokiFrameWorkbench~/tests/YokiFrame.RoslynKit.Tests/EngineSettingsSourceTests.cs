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

    /// <summary>创建本用例独占的临时目录。不写配置文件。</summary>
    public EngineSettingsSourceTests()
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

    /// <summary>指向临时目录中的 editor-settings.json。不创建文件。</summary>
    /// <returns>绑定到该路径的读取端口。</returns>
    private RoslynJsonSettingsSource SourceFor()
    {
        return new RoslynJsonSettingsSource(Path.Combine(_root, "editor-settings.json"));
    }

    /// <summary>覆盖写入测试配置。不保留旧内容。</summary>
    /// <param name="json">完整配置文本，可以是空串或损坏 JSON。</param>
    private void WriteSettings(string json)
    {
        File.WriteAllText(Path.Combine(_root, "editor-settings.json"), json);
    }

    [Fact]
    /// <summary>文件不存在时报告 MissingConfig，并阻断执行。不创建文件。</summary>
    public void Missing_file_reports_missing_config()
    {
        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.MissingConfig, snapshot.State);
        Assert.True(snapshot.BlocksExecution);
    }

    [Fact]
    /// <summary>roslynOperations.enabled 为 true 时报告 Enabled，且不阻断执行。</summary>
    public void Enabled_section_reports_enabled()
    {
        WriteSettings("{\"roslynOperations\":{\"enabled\":true}}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Enabled, snapshot.State);
        Assert.False(snapshot.BlocksExecution);
    }

    [Fact]
    /// <summary>enabled 为 false 时报告 Disabled，并阻断执行。</summary>
    public void Disabled_section_reports_disabled()
    {
        WriteSettings("{\"roslynOperations\":{\"enabled\":false}}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Disabled, snapshot.State);
        Assert.True(snapshot.BlocksExecution);
    }

    [Fact]
    /// <summary>文件合法但没有 roslynOperations 时按 Disabled 处理。不读取其他 Kit 的 enabled。</summary>
    public void Missing_section_reports_disabled()
    {
        WriteSettings("{\"logKit\":{\"saveInEditor\":true}}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Disabled, snapshot.State);
    }

    /// <summary>roslynOperations 不是对象时报告 InvalidConfig，并阻断执行。</summary>
    [Fact]
    public void Non_object_section_reports_invalid_config()
    {
        WriteSettings("{\"roslynOperations\":3}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.InvalidConfig, snapshot.State);
        Assert.True(snapshot.BlocksExecution);
    }

    [Fact]
    /// <summary>空文件报告 InvalidConfig。不把空文件当成缺失或关闭。</summary>
    public void Empty_file_reports_invalid_config()
    {
        WriteSettings(string.Empty);

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.InvalidConfig, snapshot.State);
    }

    [Fact]
    /// <summary>其他 Kit 的 enabled 不覆盖 roslynOperations。本段为 true 时仍是 Enabled。</summary>
    public void Nested_sections_do_not_shadow_the_engine_section()
    {
        WriteSettings("{\"logKit\":{\"enabled\":false},\"roslynOperations\":{\"enabled\":true}}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Enabled, snapshot.State);
    }

    [Fact]
    /// <summary>项目设置里 RoslynKit operations.enabled 为 true 时报告 Enabled。其他 Kit 条目不干扰。</summary>
    public void Project_settings_schema_enables_engine_operations()
    {
        WriteSettings("{\"formatVersion\":1,\"settings\":[{\"kit\":\"AudioKit\",\"key\":\"index.startId\",\"value\":\"1001\"},{\"kit\":\"RoslynKit\",\"key\":\"operations.enabled\",\"value\":\"true\"}]}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Enabled, snapshot.State);
    }

    [Fact]
    /// <summary>项目设置把 operations.enabled 写成 false 时报告 Disabled。</summary>
    public void Project_settings_schema_can_disable_engine_operations()
    {
        WriteSettings("{\"formatVersion\":1,\"settings\":[{\"kit\":\"RoslynKit\",\"key\":\"operations.enabled\",\"value\":\"false\"}]}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Disabled, snapshot.State);
    }

    [Fact]
    /// <summary>旧 Kit 名 Engine 不被接受，即使值为 true 也报告 Disabled。</summary>
    public void Legacy_engine_kit_name_is_not_accepted()
    {
        WriteSettings("{\"formatVersion\":1,\"settings\":[{\"kit\":\"Engine\",\"key\":\"operations.enabled\",\"value\":\"true\"}]}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Disabled, snapshot.State);
    }

    [Fact]
    /// <summary>项目设置没有 RoslynKit 条目时报告 Disabled。不回退到其他 Kit。</summary>
    public void Project_settings_without_engine_entry_reports_disabled()
    {
        WriteSettings("{\"formatVersion\":1,\"settings\":[{\"kit\":\"AudioKit\",\"key\":\"index.startId\",\"value\":\"1001\"}]}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Disabled, snapshot.State);
    }

    [Fact]
    /// <summary>截断的项目设置报告 InvalidConfig。不按缺失配置处理。</summary>
    public void Malformed_project_settings_file_reports_invalid_config()
    {
        WriteSettings("{\"formatVersion\":1,\"settings\":[");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.InvalidConfig, snapshot.State);
    }

    [Fact]
    /// <summary>enabled 为字符串而不是布尔值时报告 Disabled。不把 "true" 当成开启。</summary>
    public void Non_boolean_enabled_value_reports_disabled()
    {
        WriteSettings("{\"roslynOperations\":{\"enabled\":\"true\"}}");

        RoslynSettingsSnapshot snapshot = SourceFor().Read();

        Assert.Equal(RoslynSettingsState.Disabled, snapshot.State);
    }
}
