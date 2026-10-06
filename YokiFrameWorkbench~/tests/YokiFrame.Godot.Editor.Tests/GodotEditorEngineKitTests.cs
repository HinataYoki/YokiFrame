using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame;

namespace YokiFrame.Godot.Editor.Tests;

/// <summary>
/// Godot Editor 宿主的 Engine Kit 契约（§14 P2）：editor 目标、命令面聚合与 catalog 重建。
/// </summary>
public sealed class GodotEditorEngineKitTests
{
    [Fact]
    public void Editor_provider_declares_editor_target_and_reports_mode()
    {
        var provider = new GodotEditorEngineOperationProvider
        {
            EngineVersionAccessor = () => "4.7.0-test",
            IsPlayingAccessor = () => false,
            SessionIdAccessor = () => "session-editor",
            GenerationAccessor = () => 21L
        };

        Assert.Equal("Godot", provider.EngineKind);
        Assert.Equal("4.7.0-test", provider.EngineVersion);
        Assert.Equal(YokiFrameEngineExecutionTarget.Editor, provider.HostTargets);

        YokiFrameEngineDomainState state = provider.ReadDomainState();
        Assert.Equal("Editor", state.Mode);
        Assert.Equal(YokiFrameEngineExecutionTargets.EDITOR, state.ActiveTarget);
        Assert.False(state.IsPlaying);
        Assert.Equal("session-editor", state.SessionId);
        Assert.Equal(21L, state.Generation);
        Assert.True(state.SessionIdentityAvailable);

        // Running the game does not move editor objects into the separate runtime process.
        var playing = new GodotEditorEngineOperationProvider
        {
            EngineVersionAccessor = () => "4.7.0-test",
            IsPlayingAccessor = () => true,
            SessionIdAccessor = () => "session-editor"
        };
        YokiFrameEngineDomainState playState = playing.ReadDomainState();
        Assert.Equal("PlayMode", playState.Mode);
        Assert.Equal(YokiFrameEngineExecutionTargets.EDITOR, playState.ActiveTarget);
        Assert.True(playState.IsPlaying);
    }

    [Fact]
    public void Editor_host_wires_script_eval_into_the_command_face()
    {
        string projectRoot = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-godot-editor-script-eval",
            Guid.NewGuid().ToString("N"));
        try
        {
            var settingsSource = new StubEditorSettingsSource(YokiFrameEngineSettingsSnapshot.Enabled());
            var engineProvider = new GodotEditorEngineOperationProvider
            {
                EngineVersionAccessor = () => "4.7.0-test",
                IsPlayingAccessor = () => false,
                SessionIdAccessor = () => "session-editor",
                ScriptEval = new YokiFrameEngineScriptEvalService(
                    new StubScriptEvalHost(),
                    new YokiFrameEngineEvalStore(projectRoot))
            };
            var provider = new YokiFrameEngineKitProvider(
                YokiFrameEngineGate.CreateDefault(settingsSource),
                engineProvider,
                settingsSource);

            var actions = new HashSet<string>(StringComparer.Ordinal);
            foreach (YokiFrameCommandDescriptor descriptor in provider.Commands)
            {
                actions.Add(descriptor.Action);
            }

            // 接线后编辑器命令面必须出现 eval 三件套（脚本路径，不是 C# 路径）
            Assert.Contains("eval", actions);
            Assert.Contains("eval_result", actions);
            Assert.Contains("eval_prune", actions);

            YokiFrameCommandResult submitted = provider.Handle(CreateRequest(
                "eval",
                "{\"confirmed\":true,\"language\":\"gdscript\",\"id\":\"editor-gd\",\"code\":\"return 7\"}"));
            Assert.True(submitted.IsSuccess, submitted.ErrorCode + " " + submitted.ErrorMessage);
            Assert.Equal(
                "Ready",
                JsonDocument.Parse(submitted.ResultJson).RootElement.GetProperty("state").GetString());

            YokiFrameCommandResult read = provider.Handle(CreateRequest("eval_result", "{\"id\":\"editor-gd\"}"));
            Assert.True(read.IsSuccess, read.ErrorCode + " " + read.ErrorMessage);
            Assert.Equal(
                "memory://script",
                JsonDocument.Parse(read.ResultJson).RootElement.GetProperty("sourcePath").GetString());
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }
        }
    }

    /// <summary>假脚本宿主：只回答"编译成功 + 返回固定 JSON"，不碰任何 Godot 原生类型。</summary>
    private sealed class StubScriptEvalHost : IYokiFrameEngineScriptEvalHost
    {
        public string Language => "gdscript";

        public bool TryCompile(string id, string token, string code, out string error)
        {
            error = string.Empty;
            return true;
        }

        public bool TryInvoke(string id, string token, out string resultJson, out string error)
        {
            resultJson = "{\"ok\":true}";
            error = string.Empty;
            return true;
        }

        public void Unload(string id)
        {
        }
    }

    [Fact]
    public void Editor_kit_provider_exposes_discovery_and_history_without_entry_commands()
    {
        string projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-godot-editor-kit", Guid.NewGuid().ToString("N"));
        try
        {
            var settingsSource = new StubEditorSettingsSource(YokiFrameEngineSettingsSnapshot.Enabled());
            var store = new YokiFrameEngineRunStore(projectRoot);
            var host = new TestEditorRunHost();
            var scheduler = new YokiFrameEngineRunScheduler(store, host, () => false);
            var engineProvider = new GodotEditorEngineOperationProvider
            {
                EngineVersionAccessor = () => "4.7.0-test",
                IsPlayingAccessor = () => false,
                SessionIdAccessor = () => host.SessionId,
                GenerationAccessor = () => host.Generation,
                RunScheduler = scheduler
            };
            var gate = YokiFrameEngineGate.CreateDefault(settingsSource);
            var provider = new YokiFrameEngineKitProvider(gate, engineProvider, settingsSource);

            var actions = new HashSet<string>(StringComparer.Ordinal);
            foreach (YokiFrameCommandDescriptor descriptor in provider.Commands)
            {
                actions.Add(descriptor.Action);
            }

            Assert.Contains("engine_capabilities", actions);
            Assert.Contains("object_list", actions);
            Assert.Contains("object_describe", actions);
            Assert.Contains("run_result", actions);
            Assert.Contains("run_lookup", actions);
            Assert.Contains("run_cancel", actions);
            Assert.DoesNotContain(actions, a => a.StartsWith("entry_", StringComparison.Ordinal));
            Assert.DoesNotContain("script_run", actions);

            // editor target 被接受
            YokiFrameCommandResult accepted = provider.Handle(CreateRequest(
                "object_list",
                "{\"target\":\"editor\"}"));
            Assert.True(accepted.IsSuccess, accepted.ErrorCode + " " + accepted.ErrorMessage);
            store.Save(new YokiFrameEngineRunRecord { RunId = "history", LegacyEntry = "old", State = YokiFrameRunStatus.Passed });
            YokiFrameCommandResult result = provider.Handle(CreateRequest("run_result", "{\"runId\":\"history\"}"));
            Assert.True(result.IsSuccess);
            Assert.Equal("Passed", JsonDocument.Parse(result.ResultJson).RootElement.GetProperty("state").GetString());

            // 该宿主不承载 runtime：请求 runtime 必须被判为不可用
            YokiFrameCommandResult wrong = provider.Handle(CreateRequest(
                "object_list",
                "{\"target\":\"runtime\"}"));
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

    [Fact]
    public void Editor_command_catalog_groups_registered_kits_and_refreshes_on_revision()
    {
        using GodotEditorFileBridgeHostFixture fixture = GodotEditorFileBridgeHostFixture.Create();

        // 宿主建立之前注册的 Kit：应出现在初始命令面里。
        YokiFrameToolKitInteractionCatalog.Register(new ProbeKitProvider("ProbeKitA", "probe_a"));
        using GodotEditorFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();

        // 宿主建立之后注册的 Kit：应触发按 revision 重建，无需重启宿主。
        YokiFrameToolKitInteractionCatalog.Register(new ProbeKitProvider("ProbeKitB", "probe_b"));

        fixture.WriteSystemCommand("editor-catalog-kits-001", "list_commands");
        var processed = host.ProcessPendingCommands();
        if (!File.Exists(fixture.GetResponsePath("editor-catalog-kits-001")))
        {
            processed += host.ProcessPendingCommands();
        }

        var catalogResponse = fixture.ReadObject(fixture.GetResponsePath("editor-catalog-kits-001"));
        var catalog = JsonNode.Parse(catalogResponse["resultJson"]?.GetValue<string>() ?? "{}")?.AsObject();
        JsonArray kits = catalog?["kits"]?.AsArray() ?? new JsonArray();

        var kitNames = kits.Select(static kit => kit?["kit"]?.GetValue<string>() ?? string.Empty).ToArray();
        Assert.Contains("System", kitNames);
        Assert.Contains("ProbeKitA", kitNames);
        Assert.Contains("ProbeKitB", kitNames);
        Assert.Equal("System", kitNames[0]);

        var probeA = kits.First(static kit => kit?["kit"]?.GetValue<string>() == "ProbeKitA");
        Assert.Equal("probe_a", probeA?["actions"]?[0]?["action"]?.GetValue<string>());
    }

    [Fact]
    public void Editor_publishes_snapshots_for_versioned_kits_and_skips_unchanged_versions()
    {
        using GodotEditorFileBridgeHostFixture fixture = GodotEditorFileBridgeHostFixture.Create();
        var probe = new SnapshotProbeKit("ProbeSnapshotKit");
        YokiFrameToolKitInteractionCatalog.Register(probe);
        using GodotEditorFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();

        fixture.WriteSystemCommand("editor-snapshot-001", "list_commands");
        host.ProcessPendingCommands();

        string snapshotPath = Path.Combine(fixture.EngineRoot, "snapshots", "ProbeSnapshotKit", "state.json");
        Assert.True(File.Exists(snapshotPath), "snapshot file is missing: " + snapshotPath);

        var envelope = JsonNode.Parse(File.ReadAllText(snapshotPath))!.AsObject();
        Assert.Equal("ProbeSnapshotKit", envelope["kit"]!.GetValue<string>());
        Assert.Equal("state", envelope["name"]!.GetValue<string>());
        Assert.Equal("godot-editor", envelope["engineId"]!.GetValue<string>());
        Assert.Contains("\"probe\":1", envelope["payloadJson"]!.GetValue<string>());
        long firstSequence = envelope["sequence"]!.GetValue<long>();
        Assert.True(firstSequence > 0);

        // 版本未变：不重写文件（序列号保持不变）
        fixture.WriteSystemCommand("editor-snapshot-002", "list_commands");
        host.ProcessPendingCommands();
        var unchanged = JsonNode.Parse(File.ReadAllText(snapshotPath))!.AsObject();
        Assert.Equal(firstSequence, unchanged["sequence"]!.GetValue<long>());

        // 版本推进：重写并带上新 payload
        probe.Version = 2;
        probe.Payload = "{\"probe\":2}";
        fixture.WriteSystemCommand("editor-snapshot-003", "list_commands");
        host.ProcessPendingCommands();
        var updated = JsonNode.Parse(File.ReadAllText(snapshotPath))!.AsObject();
        Assert.True(updated["sequence"]!.GetValue<long>() > firstSequence);
        Assert.Contains("\"probe\":2", updated["payloadJson"]!.GetValue<string>());
    }

    private static YokiFrameCommandRequest CreateRequest(string action, string payload)
    {
        return new YokiFrameCommandRequest("cli", "Engine", action, payload, 5000, 0L, "req-" + Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
    }

    /// <summary>探针 Kit：用于验证编辑器命令面按 catalog 聚合与重建。</summary>
    private sealed class ProbeKitProvider : IYokiFrameKitInteractionProvider
    {
        private readonly YokiFrameCommandDescriptor[] _commands;

        internal ProbeKitProvider(string kit, string action)
        {
            Kit = kit;
            _commands = new[] { new YokiFrameCommandDescriptor(kit, action, YokiFrameCommandKind.ReadOnly) };
        }

        public string Kit { get; }

        public IReadOnlyList<string> SnapshotNames => Array.Empty<string>();

        public IReadOnlyList<YokiFrameCommandDescriptor> Commands => _commands;

        public string CreateSnapshot(string snapshotName) => "{}";

        public bool CanHandle(YokiFrameCommandRequest request)
        {
            return request != null && string.Equals(request.Kit, Kit, StringComparison.Ordinal);
        }

        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request)
        {
            return YokiFrameCommandResult.Success("{\"probe\":true}");
        }
    }

    /// <summary>声明版本化 state 快照的探针 Kit：用于验证编辑器宿主的快照发布链。</summary>
    private sealed class SnapshotProbeKit : IYokiFrameSnapshotVersionedKitInteractionProvider
    {
        private readonly string[] _snapshotNames = { "state" };

        internal SnapshotProbeKit(string kit)
        {
            Kit = kit;
        }

        internal long Version { get; set; } = 1L;

        internal string Payload { get; set; } = "{\"probe\":1}";

        public string Kit { get; }

        public IReadOnlyList<string> SnapshotNames => _snapshotNames;

        public IReadOnlyList<YokiFrameCommandDescriptor> Commands => Array.Empty<YokiFrameCommandDescriptor>();

        public long StateVersion => Version;

        public string CreateSnapshot(string snapshotName) => Payload;

        public bool CanHandle(YokiFrameCommandRequest request) => false;

        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request)
        {
            return YokiFrameCommandResult.Error("UnknownCommand", "SnapshotProbeKit only publishes snapshots.");
        }
    }
}

/// <summary>开关源替身。</summary>
internal sealed class StubEditorSettingsSource : IYokiFrameEngineSettingsSource
{
    private readonly YokiFrameEngineSettingsSnapshot _snapshot;

    internal StubEditorSettingsSource(YokiFrameEngineSettingsSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public YokiFrameEngineSettingsSnapshot Read() => _snapshot;
}

/// <summary>入口宿主替身。</summary>
internal sealed class TestEditorRunHost : IYokiFrameEngineRunHost
{
    public string SessionId => "session-editor";

    public long Generation => 5L;

    public string HostTarget => "editor";

    public Task LoadSceneAsync(string sceneName, System.Threading.CancellationToken cancellationToken) => Task.CompletedTask;

    public void Log(string message)
    {
    }
}

/// <summary>
/// inspect 在 Godot 宿主的命令面上必须可用：它是引擎无关的纯反射读取（零编译、只读、断开关也能用）。
/// </summary>
public sealed class GodotInspectCommandFaceTests
{
    [Fact]
    public void Godot_editor_command_face_includes_inspect()
    {
        var settingsSource = new StubEditorSettingsSource(YokiFrameEngineSettingsSnapshot.Enabled());
        var engineProvider = new GodotEditorEngineOperationProvider
        {
            EngineVersionAccessor = () => "4.7.0-test",
            IsPlayingAccessor = () => false,
            SessionIdAccessor = () => "session-inspect",
            GenerationAccessor = () => 7L
        };
        var provider = new YokiFrameEngineKitProvider(
            YokiFrameEngineGate.CreateDefault(settingsSource),
            engineProvider,
            settingsSource);

        var actions = new HashSet<string>(StringComparer.Ordinal);
        foreach (YokiFrameCommandDescriptor descriptor in provider.Commands)
        {
            actions.Add(descriptor.Action);
        }

        Assert.Contains("inspect", actions);
        Assert.Contains("domain_state", actions);
        Assert.Contains("engine_capabilities", actions);
    }
}
