using YokiFrame.Json;

namespace YokiFrame.RoslynKit.Tests;

/// <summary>
/// 验证 RoslynKit Provider：命令面组合（内建诊断 + 引擎侧操作）、state snapshot 与逐 target 能力矩阵。
/// </summary>
public sealed class EngineCatalogProviderTests
{
    private static RoslynKitProvider CreateProvider(
        RoslynSettingsSnapshot settings,
        RoslynExecutionTarget hostTargets,
        out StubEngineOperationProvider engineProvider,
        out StubEngineSettingsSource settingsSource,
        params RecordingEngineOperation[] extraOperations)
    {
        settingsSource = new StubEngineSettingsSource(settings);
        engineProvider = new StubEngineOperationProvider("Unity", hostTargets, extraOperations);
        var gate = RoslynGate.CreateDefault(settingsSource);
        return new RoslynKitProvider(gate, engineProvider, settingsSource);
    }

    private static JsonElement Capabilities(RoslynKitProvider provider)
    {
        YokiFrameCommandResult result = provider.Handle(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "engine_capabilities"));
        Assert.True(result.IsSuccess);
        return JsonDocument.Parse(result.ResultJson).RootElement;
    }

    private static JsonElement Operation(JsonElement capabilities, string action)
    {
        JsonElement operations = capabilities.GetProperty("operations");
        for (var index = 0; index < operations.GetArrayLength(); index++)
        {
            if (operations[index].GetProperty("action").GetString() == action)
            {
                return operations[index];
            }
        }

        throw new InvalidOperationException("capabilities report does not contain action: " + action);
    }

    private static string TargetAvailability(JsonElement operation, string target)
    {
        JsonElement entries = operation.GetProperty("targetAvailability");
        for (var index = 0; index < entries.GetArrayLength(); index++)
        {
            if (entries[index].GetProperty("target").GetString() == target)
            {
                return entries[index].GetProperty("availability").GetString()!;
            }
        }

        throw new InvalidOperationException("capability entry does not declare target: " + target);
    }

    [Fact]
    public void Provider_composes_builtin_and_engine_operations()
    {
        RoslynKitProvider provider = CreateProvider(
            RoslynSettingsSnapshot.Enabled(),
            RoslynExecutionTargets.ALL,
            out _,
            out _,
            new RecordingEngineOperation("scene_query", YokiFrameCommandKind.ReadOnly));

        Assert.Equal("RoslynKit", provider.Kit);
        Assert.Single(provider.SnapshotNames);
        Assert.Equal("state", provider.SnapshotNames[0]);
        Assert.Equal(6, provider.Commands.Count);

        string[] actions = provider.Commands.Select(command => command.Action).OrderBy(action => action, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "domain_state", "engine_capabilities", "inspect", "object_describe", "object_list", "scene_query" }, actions);
    }

    [Fact]
    public void Unknown_snapshot_name_is_rejected()
    {
        RoslynKitProvider provider = CreateProvider(
            RoslynSettingsSnapshot.Enabled(),
            RoslynExecutionTargets.ALL,
            out _,
            out _,
            new RecordingEngineOperation("scene_query", YokiFrameCommandKind.ReadOnly));

        Assert.Throws<ArgumentException>(() => provider.CreateSnapshot("other"));
    }

    [Fact]
    public void Capabilities_report_lists_per_target_matrix()
    {
        var sceneQuery = new RecordingEngineOperation("scene_query", YokiFrameCommandKind.ReadOnly, false, false,
            RoslynExecutionTarget.Editor | RoslynExecutionTarget.Play);
        var runtimeOnly = new RecordingEngineOperation("entry_run", YokiFrameCommandKind.Dangerous, false, false,
            RoslynExecutionTarget.Runtime);

        RoslynKitProvider provider = CreateProvider(
            RoslynSettingsSnapshot.Enabled(),
            RoslynExecutionTarget.Editor | RoslynExecutionTarget.Play,
            out _,
            out _,
            sceneQuery,
            runtimeOnly);

        JsonElement capabilities = Capabilities(provider);
        Assert.Equal("Unity", capabilities.GetProperty("engineKind").GetString());
        Assert.Equal("editor|play", capabilities.GetProperty("hostTargets").GetString());
        Assert.False(capabilities.GetProperty("executionBlocked").GetBoolean());
        Assert.Equal(7, capabilities.GetProperty("operations").GetArrayLength());

        JsonElement scene = Operation(capabilities, "scene_query");
        Assert.Equal("editor|play", scene.GetProperty("targets").GetString());
        Assert.False(scene.GetProperty("isTargetAgnostic").GetBoolean());
        Assert.Equal("Enabled", TargetAvailability(scene, "editor"));
        Assert.Equal("Enabled", TargetAvailability(scene, "play"));

        // 宿主不承载 runtime 时，逐目标矩阵必须直接表达出来，而不是汇总成 Enabled。
        JsonElement entryRun = Operation(capabilities, "entry_run");
        Assert.Equal("UnsupportedByHostTarget", TargetAvailability(entryRun, "runtime"));

        // 目标无关的诊断列出声明目标，但不受宿主承载限制。
        JsonElement domainState = Operation(capabilities, "domain_state");
        Assert.True(domainState.GetProperty("isTargetAgnostic").GetBoolean());
        Assert.Equal("Enabled", TargetAvailability(domainState, "runtime"));
    }

    [Fact]
    public void Snapshot_carries_capability_matrix_objects_and_runs_without_an_entry_catalog()
    {
        var settingsSource = new StubEngineSettingsSource(RoslynSettingsSnapshot.Enabled());
        string storeRoot = Path.Combine(Path.GetTempPath(), "yokiframe-snapshot-tests", Guid.NewGuid().ToString("N"));
        var engineProvider = new StubEngineOperationProvider("Unity", RoslynExecutionTarget.Editor)
        {
            Runs = new RoslynRunScheduler(
                new RoslynRunStore(storeRoot),
                new TestRunHost())
        };
        var gate = RoslynGate.CreateDefault(settingsSource);
        var provider = new RoslynKitProvider(gate, engineProvider, settingsSource);

        JsonDocument snapshot = JsonDocument.Parse(provider.CreateSnapshot("state"));
        JsonElement root = snapshot.RootElement;

        Assert.Equal("editor", root.GetProperty("hostTargets").GetString());
        Assert.False(root.GetProperty("objectCatalogAvailable").GetBoolean());
        Assert.False(root.TryGetProperty("entries", out _));

        JsonElement capabilities = root.GetProperty("capabilities");
        var actions = new List<string>();
        for (var index = 0; index < capabilities.GetArrayLength(); index++)
        {
            JsonElement capability = capabilities[index];
            actions.Add(capability.GetProperty("action").GetString()!);
            Assert.True(capability.GetProperty("targetAvailability").GetArrayLength() >= 1);
        }

        Assert.Contains("engine_capabilities", actions);
        Assert.Contains("object_list", actions);
        Assert.Contains("run_result", actions);

        // 最近运行也进快照（Workbench 只读文件，看不到 entry_* 的返回值）
        Assert.True(root.TryGetProperty("recentRunCount", out _));
        Assert.True(root.TryGetProperty("recentRuns", out _));
    }

    [Fact]
    public void Recent_runs_read_is_cached_between_version_probes()
    {
        var settingsSource = new StubEngineSettingsSource(RoslynSettingsSnapshot.Enabled());
        string storeRoot = Path.Combine(Path.GetTempPath(), "yokiframe-runs-cache", Guid.NewGuid().ToString("N"));
        var scheduler = new RoslynRunScheduler(
            new RoslynRunStore(storeRoot),
            new TestRunHost());
        var engineProvider = new StubEngineOperationProvider("Unity", RoslynExecutionTarget.Editor)
        {
            Runs = scheduler
        };
        var provider = new RoslynKitProvider(
            RoslynGate.CreateDefault(settingsSource),
            engineProvider,
            settingsSource);

        // 版本指纹会被宿主泵每帧读取：目录枚举必须被 TTL 缓存挡住。
        Assert.True(JsonDocument.Parse(provider.CreateSnapshot("state")).RootElement
            .GetProperty("recentRunCount").TryGetInt32(out int before));
        Assert.Equal(0, before);

        Assert.NotNull(scheduler.SubmitWork("editor", "req-cache-1", "cli", "hash", 30000, () => new TestRunWork()));

        // TTL 内不重新枚举：新运行还没体现（这正是缓存生效的证据）
        Assert.True(JsonDocument.Parse(provider.CreateSnapshot("state")).RootElement
            .GetProperty("recentRunCount").TryGetInt32(out int cached));
        Assert.Equal(0, cached);

        // 超过 TTL 后刷新到最新
        Thread.Sleep(400);
        Assert.True(JsonDocument.Parse(provider.CreateSnapshot("state")).RootElement
            .GetProperty("recentRunCount").TryGetInt32(out int refreshed));
        Assert.True(refreshed >= 1);
    }

    [Fact]
    public void Snapshot_records_recent_runs_and_bumps_version()
    {
        var settingsSource = new StubEngineSettingsSource(RoslynSettingsSnapshot.Enabled());
        string storeRoot = Path.Combine(Path.GetTempPath(), "yokiframe-runs-snapshot", Guid.NewGuid().ToString("N"));
        var scheduler = new RoslynRunScheduler(
            new RoslynRunStore(storeRoot),
            new TestRunHost());
        var engineProvider = new StubEngineOperationProvider("Unity", RoslynExecutionTarget.Editor)
        {
            Runs = scheduler
        };
        var provider = new RoslynKitProvider(
            RoslynGate.CreateDefault(settingsSource),
            engineProvider,
            settingsSource);

        long before = provider.StateVersion;
        JsonDocument empty = JsonDocument.Parse(provider.CreateSnapshot("state"));
        Assert.True(empty.RootElement.GetProperty("recentRunCount").TryGetInt32(out int emptyCount));
        Assert.Equal(0, emptyCount);

        RoslynRunRecord run = scheduler.SubmitWork("editor", "req-snapshot-1", "cli", "hash", 30000, () => new TestRunWork());
        Assert.NotNull(run);
        for (var index = 0; index < 50 && !run.IsTerminal; index++)
        {
            scheduler.Tick();
            Assert.True(scheduler.TryReadRun(run.RunId, out run));
        }

        // 运行枚举带 250ms TTL 缓存（版本指纹每帧被读），因此等一个窗口再断言。
        Thread.Sleep(400);
        long after = provider.StateVersion;
        JsonDocument document = JsonDocument.Parse(provider.CreateSnapshot("state"));
        Assert.True(document.RootElement.GetProperty("recentRunCount").TryGetInt32(out int recorded));
        Assert.True(recorded >= 1);
        Assert.True(after > before, "run activity must bump the snapshot version");
        string snapshot = provider.CreateSnapshot("state");
        Assert.Contains(run.RunId, snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void State_version_changes_when_the_command_face_changes()
    {
        var settingsSource = new StubEngineSettingsSource(RoslynSettingsSnapshot.Enabled());
        var firstProvider = new StubEngineOperationProvider("Unity", RoslynExecutionTarget.Editor);
        var first = new RoslynKitProvider(
            RoslynGate.CreateDefault(settingsSource),
            firstProvider,
            settingsSource);
        long firstVersion = first.StateVersion;
        Assert.Equal(firstVersion, first.StateVersion);

        // 命令面变化（新增一个操作）必须推进版本，否则 Workbench 会一直看到旧矩阵。
        var secondProvider = new StubEngineOperationProvider(
            "Unity",
            RoslynExecutionTarget.Editor,
            new RecordingEngineOperation("scene_query", YokiFrameCommandKind.ReadOnly));
        var second = new RoslynKitProvider(
            RoslynGate.CreateDefault(settingsSource),
            secondProvider,
            settingsSource);
        long secondVersion = second.StateVersion;

        Assert.NotEqual(first.Commands.Count, second.Commands.Count);
        Assert.True(secondVersion > 0);
        Assert.Equal(secondVersion, second.StateVersion);
    }

    [Fact]
    public void Disabled_switch_marks_execution_operations_but_not_diagnostics()
    {
        var sceneQuery = new RecordingEngineOperation("scene_query", YokiFrameCommandKind.ReadOnly);
        var cancellation = new RecordingEngineOperation("entry_cancel", YokiFrameCommandKind.UserAction, false, true);

        RoslynKitProvider provider = CreateProvider(
            RoslynSettingsSnapshot.Disabled("switched off"),
            RoslynExecutionTargets.ALL,
            out _,
            out _,
            sceneQuery,
            cancellation);

        JsonElement capabilities = Capabilities(provider);
        Assert.True(capabilities.GetProperty("executionBlocked").GetBoolean());
        Assert.Equal("Disabled", capabilities.GetProperty("settingsState").GetString());
        Assert.Equal("switched off", capabilities.GetProperty("settingsReason").GetString());
        Assert.Equal("DisabledBySettings", TargetAvailability(Operation(capabilities, "scene_query"), "editor"));
        Assert.Equal("Enabled", TargetAvailability(Operation(capabilities, "entry_cancel"), "editor"));
        Assert.Equal("Enabled", TargetAvailability(Operation(capabilities, "engine_capabilities"), "runtime"));
        Assert.Equal("Enabled", TargetAvailability(Operation(capabilities, "domain_state"), "runtime"));
    }

    [Fact]
    public void Snapshot_matches_domain_state_action_payload()
    {
        RoslynKitProvider provider = CreateProvider(
            RoslynSettingsSnapshot.Enabled(),
            RoslynExecutionTargets.ALL,
            out StubEngineOperationProvider engineProvider,
            out _,
            new RecordingEngineOperation("scene_query", YokiFrameCommandKind.ReadOnly));
        engineProvider.State.IsPlaying = true;
        engineProvider.State.ActiveTarget = RoslynExecutionTargets.PLAY;
        engineProvider.State.Mode = "PlayMode";

        YokiFrameCommandResult result = provider.Handle(
            EnginePipeline.CreateRequest(EnginePipeline.CliSource, "domain_state"));
        string snapshot = provider.CreateSnapshot("state");

        Assert.True(result.IsSuccess);

        // 契约：domain_state 保持精简；snapshot 更富（domain 字段 + hostTargets + capabilities + 入口目录）。
        JsonDocument action = JsonDocument.Parse(result.ResultJson);
        JsonDocument document = JsonDocument.Parse(snapshot);
        Assert.True(document.RootElement.GetProperty("isPlaying").GetBoolean());
        Assert.Equal("play", document.RootElement.GetProperty("activeTarget").GetString());
        Assert.False(document.RootElement.GetProperty("sessionIdentityAvailable").GetBoolean());
        Assert.Equal("Enabled", document.RootElement.GetProperty("settingsState").GetString());

        // domain 字段在两边必须一致（同一份写出器）
        foreach (string field in new[] { "engineKind", "mode", "activeTarget", "isPlaying", "isCompiling", "isBusy", "settingsState" })
        {
            Assert.Equal(
                action.RootElement.GetProperty(field).ToString(),
                document.RootElement.GetProperty(field).ToString());
        }

        // 快照独有：承载目标、能力矩阵与入口目录摘要
        Assert.False(action.RootElement.TryGetProperty("capabilities", out _));
        Assert.True(document.RootElement.GetProperty("capabilities").GetArrayLength() >= 3);
        Assert.True(document.RootElement.TryGetProperty("hostTargets", out _));
        Assert.True(document.RootElement.TryGetProperty("objectCatalogAvailable", out _));
    }

    [Fact]
    public void State_version_advances_only_when_state_or_switch_changes()
    {
        var settingsSource = new StubEngineSettingsSource(RoslynSettingsSnapshot.Enabled());
        var engineProvider = new StubEngineOperationProvider("Unity", RoslynExecutionTargets.ALL);
        var gate = RoslynGate.CreateDefault(settingsSource);
        var provider = new RoslynKitProvider(gate, engineProvider, settingsSource);

        long initial = provider.StateVersion;
        Assert.Equal(initial, provider.StateVersion);

        engineProvider.State.IsPlaying = true;
        long afterPlay = provider.StateVersion;
        Assert.True(afterPlay > initial, "播放状态变化必须推进 snapshot 版本");
        Assert.Equal(afterPlay, provider.StateVersion);

        settingsSource.Snapshot = RoslynSettingsSnapshot.Disabled("turned off");
        Assert.True(provider.StateVersion > afterPlay, "开关变化必须推进 snapshot 版本");
    }
}
