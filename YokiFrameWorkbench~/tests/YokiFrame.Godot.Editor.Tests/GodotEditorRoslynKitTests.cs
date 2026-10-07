using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame;

namespace YokiFrame.Godot.Editor.Tests;

/// <summary>
/// Godot Editor 宿主的 RoslynKit 契约（§14 P2）：editor 目标、命令面聚合与 catalog 重建。
/// </summary>
public sealed class GodotEditorRoslynKitTests
{
    /// <summary>编辑器提供者声明 editor 目标。进入播放后模式变为 PlayMode，活动目标仍是 editor。</summary>
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
        Assert.Equal(RoslynExecutionTarget.Editor, provider.HostTargets);

        RoslynDomainState state = provider.ReadDomainState();
        Assert.Equal("Editor", state.Mode);
        Assert.Equal(RoslynExecutionTargets.EDITOR, state.ActiveTarget);
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
        RoslynDomainState playState = playing.ReadDomainState();
        Assert.Equal("PlayMode", playState.Mode);
        Assert.Equal(RoslynExecutionTargets.EDITOR, playState.ActiveTarget);
        Assert.True(playState.IsPlaying);
    }

    /// <summary>编辑器宿主把脚本 eval 接进命令面，并完成一次 gdscript 往返。结束后删除临时目录。</summary>
    [Fact]
    public void Editor_host_wires_script_eval_into_the_command_face()
    {
        string projectRoot = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-godot-editor-script-eval",
            Guid.NewGuid().ToString("N"));
        try
        {
            AssertEditorScriptEvalRoundTrip(CreateEditorScriptEvalProvider(projectRoot));
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }
        }
    }

    /// <summary>装配带脚本 eval 存储的编辑器提供者。不发送命令，不创建目录。</summary>
    /// <param name="projectRoot">eval 存储所在的项目根。</param>
    /// <returns>已接上 gdscript 宿主的提供者。</returns>
    private static RoslynKitProvider CreateEditorScriptEvalProvider(string projectRoot)
    {
        var settingsSource = new StubEditorSettingsSource(RoslynSettingsSnapshot.Enabled());
        var engineProvider = new GodotEditorEngineOperationProvider
        {
            EngineVersionAccessor = () => "4.7.0-test",
            IsPlayingAccessor = () => false,
            SessionIdAccessor = () => "session-editor",
            ScriptEval = new RoslynScriptEvalService(
                new StubScriptEvalHost(),
                new RoslynEvalStore(projectRoot))
        };
        return new RoslynKitProvider(
            RoslynGate.CreateDefault(settingsSource),
            engineProvider,
            settingsSource);
    }

    /// <summary>断言 eval 三件套已接线，并完成一次提交和结果读取。不加载程序集。</summary>
    /// <param name="provider">已接上脚本 eval 的提供者。</param>
    private static void AssertEditorScriptEvalRoundTrip(RoslynKitProvider provider)
    {
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

    /// <summary>假脚本宿主：只回答"编译成功 + 返回固定 JSON"，不碰任何 Godot 原生类型。</summary>
    private sealed class StubScriptEvalHost : IRoslynScriptEvalHost
    {
        public string Language => "gdscript";

        /// <summary>总是编译成功。不记录源码，错误信息为空。</summary>
        /// <param name="id">未使用的脚本标识。</param>
        /// <param name="token">未使用的令牌。</param>
        /// <param name="code">未使用的源码。</param>
        /// <param name="error">始终为空的错误。</param>
        /// <returns>始终为 true。</returns>
        public bool TryCompile(string id, string token, string code, out string error)
        {
            error = string.Empty;
            return true;
        }

        /// <summary>总是调用成功并返回固定 JSON。不接触 Godot 类型。</summary>
        /// <param name="id">未使用的脚本标识。</param>
        /// <param name="token">未使用的令牌。</param>
        /// <param name="resultJson">固定成功 JSON。</param>
        /// <param name="error">始终为空的错误。</param>
        /// <returns>始终为 true。</returns>
        public bool TryInvoke(string id, string token, out string resultJson, out string error)
        {
            resultJson = "{\"ok\":true}";
            error = string.Empty;
            return true;
        }

        /// <summary>卸载不做任何事。不记录标识，避免测试依赖宿主。</summary>
        /// <param name="id">未使用的脚本标识。</param>
        public void Unload(string id)
        {
        }
    }

    /// <summary>编辑器命令面暴露发现和历史，且不含入口命令。runtime 目标不可用，结束后删除临时目录。</summary>
    [Fact]
    public void Editor_kit_provider_exposes_discovery_and_history_without_entry_commands()
    {
        string projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-godot-editor-kit", Guid.NewGuid().ToString("N"));
        try
        {
            RoslynKitProvider provider = CreateEditorDiscoveryProvider(projectRoot, out RoslynRunStore store);
            AssertEditorDiscoveryAndHistory(store, provider);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }
        }
    }

    /// <summary>装配带运行调度器的编辑器提供者。不发送命令。</summary>
    /// <param name="projectRoot">运行存储所在的项目根。</param>
    /// <param name="store">接收本次创建的运行存储。</param>
    /// <returns>已接上调度器的提供者。</returns>
    private static RoslynKitProvider CreateEditorDiscoveryProvider(string projectRoot, out RoslynRunStore store)
    {
        var settingsSource = new StubEditorSettingsSource(RoslynSettingsSnapshot.Enabled());
        store = new RoslynRunStore(projectRoot);
        var host = new TestEditorRunHost();
        var scheduler = new RoslynRunScheduler(store, host, () => false);
        var engineProvider = new GodotEditorEngineOperationProvider
        {
            EngineVersionAccessor = () => "4.7.0-test",
            IsPlayingAccessor = () => false,
            SessionIdAccessor = () => host.SessionId,
            GenerationAccessor = () => host.Generation,
            RunScheduler = scheduler
        };
        var gate = RoslynGate.CreateDefault(settingsSource);
        return new RoslynKitProvider(gate, engineProvider, settingsSource);
    }

    /// <summary>断言发现与历史命令可用，editor 目标成功，runtime 目标不可用。会写入一条历史记录。</summary>
    /// <param name="store">接收历史记录的存储。</param>
    /// <param name="provider">被测提供者。</param>
    private static void AssertEditorDiscoveryAndHistory(RoslynRunStore store, RoslynKitProvider provider)
    {
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
        store.Save(new RoslynRunRecord { RunId = "history", LegacyEntry = "old", State = RunStatus.Passed });
        YokiFrameCommandResult result = provider.Handle(CreateRequest("run_result", "{\"runId\":\"history\"}"));
        Assert.True(result.IsSuccess);
        Assert.Equal("Passed", JsonDocument.Parse(result.ResultJson).RootElement.GetProperty("state").GetString());

        // 该宿主不承载 runtime：请求 runtime 必须被判为不可用
        YokiFrameCommandResult wrong = provider.Handle(CreateRequest(
            "object_list",
            "{\"target\":\"runtime\"}"));
        Assert.False(wrong.IsSuccess);
        Assert.Equal(RoslynErrorCodes.UNAVAILABLE, wrong.ErrorCode);
    }

    [Fact]
    /// <summary>命令目录按注册顺序聚合 Kit，宿主建立后的新 Kit 也会在下一次处理时出现。</summary>
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
    /// <summary>版本化 Kit 会发布快照。版本不变不重写序号，版本推进后写入新 payload。</summary>
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

    /// <summary>构造一条 cli 来源的 RoslynKit 请求。不发送。</summary>
    /// <param name="action">操作名。</param>
    /// <param name="payload">请求 JSON。</param>
    /// <returns>带新请求号的请求。</returns>
    private static YokiFrameCommandRequest CreateRequest(string action, string payload)
    {
        return new YokiFrameCommandRequest("cli", "RoslynKit", action, payload, 5000, 0L, "req-" + Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
    }

    /// <summary>探针 Kit：用于验证编辑器命令面按 catalog 聚合与重建。</summary>
    private sealed class ProbeKitProvider : IYokiFrameKitInteractionProvider
    {
        private readonly YokiFrameCommandDescriptor[] _commands;

        /// <summary>登记一个只读探针动作。不注册到全局目录。</summary>
        /// <param name="kit">Kit 名。</param>
        /// <param name="action">唯一动作名。</param>
        internal ProbeKitProvider(string kit, string action)
        {
            Kit = kit;
            _commands = new[] { new YokiFrameCommandDescriptor(kit, action, YokiFrameCommandKind.ReadOnly) };
        }

        public string Kit { get; }

        public IReadOnlyList<string> SnapshotNames => Array.Empty<string>();

        public IReadOnlyList<YokiFrameCommandDescriptor> Commands => _commands;

        /// <summary>返回空对象快照。不读取 snapshotName。</summary>
        /// <param name="snapshotName">未使用的快照名。</param>
        /// <returns>空 JSON 对象。</returns>
        public string CreateSnapshot(string snapshotName) => "{}";

        /// <summary>只处理 Kit 名相同的非空请求。不修改请求。</summary>
        /// <param name="request">待判断请求。</param>
        /// <returns>Kit 名一致时为 true。</returns>
        public bool CanHandle(YokiFrameCommandRequest request)
        {
            return request != null && string.Equals(request.Kit, Kit, StringComparison.Ordinal);
        }

        /// <summary>返回固定成功 JSON。不校验请求内容。</summary>
        /// <param name="request">未使用的请求。</param>
        /// <returns>probe 为 true 的成功结果。</returns>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request)
        {
            return YokiFrameCommandResult.Success("{\"probe\":true}");
        }
    }

    /// <summary>声明版本化 state 快照的探针 Kit：用于验证编辑器宿主的快照发布链。</summary>
    private sealed class SnapshotProbeKit : IYokiFrameSnapshotVersionedKitInteractionProvider
    {
        private readonly string[] _snapshotNames = { "state" };

        /// <summary>记录探针 Kit 名。版本和 payload 使用默认值。</summary>
        /// <param name="kit">Kit 名。</param>
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

        /// <summary>返回当前 payload，不读取快照名。</summary>
        /// <param name="snapshotName">未使用的快照名。</param>
        /// <returns>当前 payload 文本。</returns>
        public string CreateSnapshot(string snapshotName) => Payload;

        /// <summary>快照探针不处理命令。始终返回 false。</summary>
        /// <param name="request">未使用的请求。</param>
        /// <returns>始终为 false。</returns>
        public bool CanHandle(YokiFrameCommandRequest request) => false;

        /// <summary>拒绝命令并说明该 Kit 只发布快照。不写文件。</summary>
        /// <param name="request">未使用的请求。</param>
        /// <returns>UnknownCommand 错误。</returns>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request)
        {
            return YokiFrameCommandResult.Error("UnknownCommand", "SnapshotProbeKit only publishes snapshots.");
        }
    }
}

/// <summary>开关源替身。</summary>
internal sealed class StubEditorSettingsSource : IRoslynSettingsSource
{
    private readonly RoslynSettingsSnapshot _snapshot;

    /// <summary>固定开关快照。不读项目设置。</summary>
    /// <param name="snapshot">后续读取返回的快照。</param>
    internal StubEditorSettingsSource(RoslynSettingsSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    /// <summary>返回构造时固定的快照。不复制、不读磁盘。</summary>
    /// <returns>预设快照。</returns>
    public RoslynSettingsSnapshot Read() => _snapshot;
}

/// <summary>入口宿主替身。</summary>
internal sealed class TestEditorRunHost : IRoslynRunHost
{
    public string SessionId => "session-editor";

    public long Generation => 5L;

    public string HostTarget => "editor";

    /// <summary>立即完成场景加载。不接触 Godot，也不观察取消令牌。</summary>
    /// <param name="sceneName">未使用的场景名。</param>
    /// <param name="cancellationToken">未使用的取消令牌。</param>
    /// <returns>已完成的任务。</returns>
    public Task LoadSceneAsync(string sceneName, System.Threading.CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>丢弃日志。不写控制台。</summary>
    /// <param name="message">未使用的日志文本。</param>
    public void Log(string message)
    {
    }
}

/// <summary>
/// inspect 在 Godot 宿主的命令面上必须可用：它是引擎无关的纯反射读取（零编译、只读、断开关也能用）。
/// </summary>
public sealed class GodotInspectCommandFaceTests
{
    /// <summary>Godot 编辑器命令面包含 inspect、domain_state 和 engine_capabilities。不执行这些命令。</summary>
    [Fact]
    public void Godot_editor_command_face_includes_inspect()
    {
        var settingsSource = new StubEditorSettingsSource(RoslynSettingsSnapshot.Enabled());
        var engineProvider = new GodotEditorEngineOperationProvider
        {
            EngineVersionAccessor = () => "4.7.0-test",
            IsPlayingAccessor = () => false,
            SessionIdAccessor = () => "session-inspect",
            GenerationAccessor = () => 7L
        };
        var provider = new RoslynKitProvider(
            RoslynGate.CreateDefault(settingsSource),
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
