using System.Text.Json;
using YokiFrame;

namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// Godot 宿主的 Engine Kit 契约（§14 P2）：fail-closed 开关、runtime 目标与入口执行器。
/// </summary>
public sealed class GodotEngineKitTests
{
    [Fact]
    public void Settings_source_is_fail_closed_for_missing_invalid_and_false()
    {
        // 缺失
        YokiFrameEngineSettingsSnapshot missing = new GodotEngineSettingsSource(new StubSettingsAccessor()).Read();
        Assert.True(missing.BlocksExecution);
        Assert.Contains("no '", missing.Reason, StringComparison.Ordinal);

        // 类型不符
        YokiFrameEngineSettingsSnapshot invalid = new GodotEngineSettingsSource(
            new StubSettingsAccessor { Exists = true, Boolean = false, IsBoolean = false }).Read();
        Assert.True(invalid.BlocksExecution);
        Assert.Contains("must be a boolean", invalid.Reason, StringComparison.Ordinal);

        // false
        YokiFrameEngineSettingsSnapshot disabled = new GodotEngineSettingsSource(
            new StubSettingsAccessor { Exists = true, Boolean = false }).Read();
        Assert.True(disabled.BlocksExecution);

        // true
        YokiFrameEngineSettingsSnapshot enabled = new GodotEngineSettingsSource(
            new StubSettingsAccessor { Exists = true, Boolean = true }).Read();
        Assert.False(enabled.BlocksExecution);

        // 访问器缺失
        Assert.True(new GodotEngineSettingsSource(null).Read().BlocksExecution);
    }

    [Fact]
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
        Assert.Equal(YokiFrameEngineExecutionTarget.Runtime, provider.HostTargets);

        YokiFrameEngineDomainState state = provider.ReadDomainState();
        Assert.Equal("Runtime", state.Mode);
        Assert.Equal(YokiFrameEngineExecutionTargets.RUNTIME, state.ActiveTarget);
        Assert.True(state.IsPlaying);
        Assert.False(state.IsCompiling);
        Assert.Equal("session-godot", state.SessionId);
        Assert.Equal(12L, state.Generation);
        Assert.True(state.SessionIdentityAvailable);

        // 未注入身份时不伪造（版本同样注入，避免在测试进程里调用 Godot 原生静态 API）
        var bare = new GodotRuntimeEngineOperationProvider { EngineVersionAccessor = () => string.Empty };
        YokiFrameEngineDomainState bareState = bare.ReadDomainState();
        Assert.False(bareState.SessionIdentityAvailable);
        Assert.Equal(string.Empty, bareState.SessionId);
        Assert.Equal(0L, bareState.Generation);
    }

    [Fact]
    public void Runtime_host_exposes_discovery_and_read_only_history_without_entry_commands()
    {
        string projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-godot-entry", Guid.NewGuid().ToString("N"));
        try
        {
            var settingsSource = new StubSettingsSource(YokiFrameEngineSettingsSnapshot.Enabled());
            var store = new YokiFrameEngineRunStore(projectRoot);
            var host = new TestGodotRunHost();
            var scheduler = new YokiFrameEngineRunScheduler(store, host, () => false);
            var engineProvider = new GodotRuntimeEngineOperationProvider
            {
                EngineVersionAccessor = () => "4.7.0-test",
                SessionIdAccessor = () => host.SessionId,
                GenerationAccessor = () => host.Generation,
                RunScheduler = scheduler
            };
            var gate = YokiFrameEngineGate.CreateDefault(settingsSource);
            var provider = new YokiFrameEngineKitProvider(gate, engineProvider, settingsSource);
            YokiFrameCommandDispatcher dispatcher = CreateDispatcher(provider);

            Assert.DoesNotContain(provider.Commands, c => c.Action.StartsWith("entry_", StringComparison.Ordinal));
            Assert.DoesNotContain(provider.Commands, c => c.Action == "script_run");
            YokiFrameCommandResult accepted = dispatcher.Dispatch(YokiFrameCommandRequestFor(
                "object_list", "{\"target\":\"runtime\"}"));
            Assert.True(accepted.IsSuccess, accepted.ErrorCode + " " + accepted.ErrorMessage);
            JsonElement acceptedJson = JsonDocument.Parse(accepted.ResultJson).RootElement;
            Assert.Equal("runtime", acceptedJson.GetProperty("target").GetString());
            store.Save(new YokiFrameEngineRunRecord { RunId = "history", LegacyEntry = "old", State = YokiFrameRunStatus.Passed });
            YokiFrameCommandResult result = provider.Handle(YokiFrameCommandRequestFor("run_result", "{\"runId\":\"history\"}"));
            Assert.True(result.IsSuccess);
            JsonElement resultJson = JsonDocument.Parse(result.ResultJson).RootElement;
            Assert.Equal("Passed", resultJson.GetProperty("state").GetString());
            Assert.Equal("legacy", resultJson.GetProperty("kind").GetString());

            // 该宿主只承载 runtime：请求 editor 必须被判为不可用
            YokiFrameCommandResult wrong = dispatcher.Dispatch(YokiFrameCommandRequestFor(
                "object_list", "{\"target\":\"editor\"}"));
            Assert.False(wrong.IsSuccess);
            Assert.Equal(YokiFrameEngineErrorCodes.UNAVAILABLE, wrong.ErrorCode);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }
        }
    }

    private static YokiFrameCommandDispatcher CreateDispatcher(YokiFrameEngineKitProvider provider)
    {
        YokiFrameCommandPolicy policy = YokiFrameCommandPolicy.CreateWithDefaultSources(provider.Commands.ToArray());
        return new YokiFrameCommandDispatcher(policy, new IYokiFrameCommandHandler[] { provider });
    }

    private static YokiFrameCommandRequest YokiFrameCommandRequestFor(string action, string payload)
    {
        return new YokiFrameCommandRequest("cli", "Engine", action, payload, 5000, 0L, "req-" + Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
    }
}

/// <summary>可注入的 Godot 项目设置访问器替身。</summary>
internal sealed class StubSettingsAccessor : IGodotProjectSettingsAccessor
{
    internal bool Exists { get; set; }

    internal bool IsBoolean { get; set; } = true;

    internal bool Boolean { get; set; }

    public bool HasSetting(string key) => Exists;

    public bool TryReadBoolean(string key, out bool value)
    {
        value = Boolean;
        return Exists && IsBoolean;
    }
}

/// <summary>最小开关源替身。</summary>
internal sealed class StubSettingsSource : IYokiFrameEngineSettingsSource
{
    private readonly YokiFrameEngineSettingsSnapshot _snapshot;

    internal StubSettingsSource(YokiFrameEngineSettingsSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public YokiFrameEngineSettingsSnapshot Read() => _snapshot;
}

/// <summary>不依赖 Godot 引擎的入口宿主替身。</summary>
internal sealed class TestGodotRunHost : IYokiFrameEngineRunHost
{
    public string SessionId => "session-test";

    public long Generation => 3L;

    public string HostTarget => "runtime";

    public Task LoadSceneAsync(string sceneName, System.Threading.CancellationToken cancellationToken) => Task.CompletedTask;

    public void Log(string message)
    {
    }
}
