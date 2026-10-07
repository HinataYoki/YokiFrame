using System.Text.Json;
using YokiFrame;

namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// Godot 宿主的 RoslynKit 契约（§14 P2）：fail-closed 开关、runtime 目标与入口执行器。
/// </summary>
public sealed class GodotRoslynKitTests
{
    [Fact]
    /// <summary>缺失、类型不符、false 和空访问器都阻断执行；只有布尔 true 放行。</summary>
    public void Settings_source_is_fail_closed_for_missing_invalid_and_false()
    {
        // 缺失
        RoslynSettingsSnapshot missing = new GodotEngineSettingsSource(new StubSettingsAccessor()).Read();
        Assert.True(missing.BlocksExecution);
        Assert.Contains("no '", missing.Reason, StringComparison.Ordinal);

        // 类型不符
        RoslynSettingsSnapshot invalid = new GodotEngineSettingsSource(
            new StubSettingsAccessor { Exists = true, Boolean = false, IsBoolean = false }).Read();
        Assert.True(invalid.BlocksExecution);
        Assert.Contains("must be a boolean", invalid.Reason, StringComparison.Ordinal);

        // false
        RoslynSettingsSnapshot disabled = new GodotEngineSettingsSource(
            new StubSettingsAccessor { Exists = true, Boolean = false }).Read();
        Assert.True(disabled.BlocksExecution);

        // true
        RoslynSettingsSnapshot enabled = new GodotEngineSettingsSource(
            new StubSettingsAccessor { Exists = true, Boolean = true }).Read();
        Assert.False(enabled.BlocksExecution);

        // 访问器缺失
        Assert.True(new GodotEngineSettingsSource(null).Read().BlocksExecution);
    }

    [Fact]
    /// <summary>运行时提供者声明 runtime 目标，并使用注入的会话身份。未注入时不伪造身份。</summary>
    public void Runtime_provider_declares_runtime_target_and_real_session_identity()
    {
        var provider = new GodotRuntimeEngineOperationProvider
        {
            // 注入版本：Godot 原生静态 API 只在本机宿主机进程内可用。
            EngineVersionAccessor = () => "4.7.0-test",
            SessionIdAccessor = () => "session-godot",
            GenerationAccessor = () => 12L
        };

        Assert.Equal("Godot", provider.EngineKind);
        Assert.Equal("4.7.0-test", provider.EngineVersion);
        Assert.Equal(RoslynExecutionTarget.Runtime, provider.HostTargets);

        RoslynDomainState state = provider.ReadDomainState();
        Assert.Equal("Runtime", state.Mode);
        Assert.Equal(RoslynExecutionTargets.RUNTIME, state.ActiveTarget);
        Assert.True(state.IsPlaying);
        Assert.False(state.IsCompiling);
        Assert.Equal("session-godot", state.SessionId);
        Assert.Equal(12L, state.Generation);
        Assert.True(state.SessionIdentityAvailable);

        // 未注入身份时不伪造（版本同样注入，避免在测试进程里调用 Godot 原生静态 API）
        var bare = new GodotRuntimeEngineOperationProvider { EngineVersionAccessor = () => string.Empty };
        RoslynDomainState bareState = bare.ReadDomainState();
        Assert.False(bareState.SessionIdentityAvailable);
        Assert.Equal(string.Empty, bareState.SessionId);
        Assert.Equal(0L, bareState.Generation);
    }

    [Fact]
    /// <summary>运行时命令面提供发现和只读历史，不含入口或 script_run。editor 目标不可用。</summary>
    public void Runtime_host_exposes_discovery_and_read_only_history_without_entry_commands()
    {
        string projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-godot-entry", Guid.NewGuid().ToString("N"));
        try
        {
            var settingsSource = new StubSettingsSource(RoslynSettingsSnapshot.Enabled());
            var store = new RoslynRunStore(projectRoot);
            var host = new TestGodotRunHost();
            var scheduler = new RoslynRunScheduler(store, host, () => false);
            var engineProvider = new GodotRuntimeEngineOperationProvider
            {
                EngineVersionAccessor = () => "4.7.0-test",
                SessionIdAccessor = () => host.SessionId,
                GenerationAccessor = () => host.Generation,
                RunScheduler = scheduler
            };
            var gate = RoslynGate.CreateDefault(settingsSource);
            var provider = new RoslynKitProvider(gate, engineProvider, settingsSource);
            YokiFrameCommandDispatcher dispatcher = CreateDispatcher(provider);

            Assert.DoesNotContain(provider.Commands, c => c.Action.StartsWith("entry_", StringComparison.Ordinal));
            Assert.DoesNotContain(provider.Commands, c => c.Action == "script_run");
            YokiFrameCommandResult accepted = dispatcher.Dispatch(YokiFrameCommandRequestFor(
                "object_list", "{\"target\":\"runtime\"}"));
            Assert.True(accepted.IsSuccess, accepted.ErrorCode + " " + accepted.ErrorMessage);
            JsonElement acceptedJson = JsonDocument.Parse(accepted.ResultJson).RootElement;
            Assert.Equal("runtime", acceptedJson.GetProperty("target").GetString());
            store.Save(new RoslynRunRecord { RunId = "history", LegacyEntry = "old", State = RunStatus.Passed });
            YokiFrameCommandResult result = provider.Handle(YokiFrameCommandRequestFor("run_result", "{\"runId\":\"history\"}"));
            Assert.True(result.IsSuccess);
            JsonElement resultJson = JsonDocument.Parse(result.ResultJson).RootElement;
            Assert.Equal("Passed", resultJson.GetProperty("state").GetString());
            Assert.Equal("legacy", resultJson.GetProperty("kind").GetString());

            // 该宿主只承载 runtime：请求 editor 必须被判为不可用
            YokiFrameCommandResult wrong = dispatcher.Dispatch(YokiFrameCommandRequestFor(
                "object_list", "{\"target\":\"editor\"}"));
            Assert.False(wrong.IsSuccess);
            Assert.Equal(RoslynErrorCodes.UNAVAILABLE, wrong.ErrorCode);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }
        }
    }

    /// <summary>用提供者的命令面装配调度器。不发送命令。</summary>
    /// <param name="provider">已接线的 RoslynKit 提供者。</param>
    /// <returns>可直接 Dispatch 的调度器。</returns>
    private static YokiFrameCommandDispatcher CreateDispatcher(RoslynKitProvider provider)
    {
        YokiFrameCommandPolicy policy = YokiFrameCommandPolicy.CreateWithDefaultSources(provider.Commands.ToArray());
        return new YokiFrameCommandDispatcher(policy, new IYokiFrameCommandHandler[] { provider });
    }

    /// <summary>构造一条 cli 来源的 RoslynKit 请求。不发送。</summary>
    /// <param name="action">操作名。</param>
    /// <param name="payload">请求 JSON。</param>
    /// <returns>带新请求号的请求。</returns>
    private static YokiFrameCommandRequest YokiFrameCommandRequestFor(string action, string payload)
    {
        return new YokiFrameCommandRequest("cli", "RoslynKit", action, payload, 5000, 0L, "req-" + Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
    }
}

/// <summary>可注入的 Godot 项目设置访问器替身。</summary>
internal sealed class StubSettingsAccessor : IGodotProjectSettingsAccessor
{
    internal bool Exists { get; set; }

    internal bool IsBoolean { get; set; } = true;

    internal bool Boolean { get; set; }

    /// <summary>按测试开关报告设置是否存在。不读取 key。</summary>
    /// <param name="key">未使用的设置键。</param>
    /// <returns>预设的存在标记。</returns>
    public bool HasSetting(string key) => Exists;

    /// <summary>仅当设置存在且类型是布尔时读出预设值。否则失败，但仍写出预设值。</summary>
    /// <param name="key">未使用的设置键。</param>
    /// <param name="value">预设布尔值。</param>
    /// <returns>存在且为布尔类型时为 true。</returns>
    public bool TryReadBoolean(string key, out bool value)
    {
        value = Boolean;
        return Exists && IsBoolean;
    }
}

/// <summary>最小开关源替身。</summary>
internal sealed class StubSettingsSource : IRoslynSettingsSource
{
    private readonly RoslynSettingsSnapshot _snapshot;

    /// <summary>固定返回的开关快照。不读项目设置。</summary>
    /// <param name="snapshot">后续读取返回的快照。</param>
    internal StubSettingsSource(RoslynSettingsSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    /// <summary>返回构造时固定的快照。不复制、不读磁盘。</summary>
    /// <returns>预设快照。</returns>
    public RoslynSettingsSnapshot Read() => _snapshot;
}

/// <summary>不依赖 Godot 引擎的入口宿主替身。</summary>
internal sealed class TestGodotRunHost : IRoslynRunHost
{
    public string SessionId => "session-test";

    public long Generation => 3L;

    public string HostTarget => "runtime";

    /// <summary>立即完成场景加载。不接触 Godot，也不观察取消令牌。</summary>
    /// <param name="sceneName">未使用的场景名。</param>
    /// <param name="cancellationToken">未使用的取消令牌。</param>
    /// <returns>已完成的任务。</returns>
    public Task LoadSceneAsync(string sceneName, System.Threading.CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>丢弃日志。不写控制台，避免测试依赖 Godot。</summary>
    /// <param name="message">未使用的日志文本。</param>
    public void Log(string message)
    {
    }
}
