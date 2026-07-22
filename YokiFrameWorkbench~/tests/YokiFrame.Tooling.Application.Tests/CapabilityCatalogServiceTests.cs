using System.Text.Json.Nodes;
using YokiFrame;
using YokiFrame.Client;
using YokiFrame.Client.Commands;
using YokiFrame.Client.FileBridge;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.ProjectModel;
using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Capabilities;
using YokiFrame.Tooling.Application.Models.Capabilities;
using YokiFrame.Tooling.Application.ProjectModel;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖静态 harness、宿主身份和实时命令目录的能力聚合语义。
/// </summary>
public sealed partial class CapabilityCatalogServiceTests
{
    private const string ENGINE_ID = "unity-editor";
    private const string SESSION_ID = "session-a";
    private const long GENERATION = 7L;
    private static readonly DateTimeOffset sNowUtc = new(2026, 7, 12, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 验证静态声明与新鲜宿主身份一致时目录为 Ready，且未显式刷新就不会发送命令。
    /// </summary>
    [Fact]
    public async Task BuildReturnsReadyWithoutRequestingCommandCatalog()
    {
        var client = new CatalogTestClient(sNowUtc);

        var result = await CreateService(client).BuildAsync(
            string.Empty,
            false,
            "tests",
            1000,
            CancellationToken.None);

        Assert.Equal("Ready", result.State);
        Assert.True(result.IsReady);
        Assert.Empty(result.Catalog.Issues);
        var engine = Assert.Single(result.Catalog.Engines);
        Assert.True(engine.Online);
        Assert.Equal("Match", engine.IdentityState);
        Assert.Equal("NotRequested", engine.CommandCatalog.State);
        Assert.Equal(0, client.CommandCallCount);
        Assert.Empty(result.Catalog.Project.DeclaredKitIds);
        Assert.Equal("Ready", result.Catalog.Project.ModelState);
        Assert.NotEmpty(result.Catalog.Project.ModelId);
        Assert.NotEmpty(result.Catalog.Project.ModelGeneration);
        Assert.Equal(".yokiframe/project/project-model.json", result.Catalog.Project.ModelPath);
        Assert.NotEmpty(result.Catalog.Project.InputHash);
    }

    /// <summary>
    /// 验证实时目录出现静态 harness 未声明的 FsmKit 命令时，目录显式标记 Drifted 并保留证据。
    /// </summary>
    [Fact]
    public async Task BuildReportsDriftWhenObservedCommandKitsDifferFromHarness()
    {
        var client = new CatalogTestClient(sNowUtc)
        {
            CommandCatalogJson = CreateCommandCatalogJson(includeFsmKit: true)
        };

        var result = await CreateService(client).BuildAsync(
            ENGINE_ID,
            true,
            YokiFrameCommandSourceContract.CODEX,
            1000,
            CancellationToken.None);

        Assert.Equal("Drifted", result.State);
        Assert.False(result.IsReady);
        Assert.Contains(result.Catalog.Issues, issue => issue.Code == "HarnessCommandCatalogDrift");
        var engine = Assert.Single(result.Catalog.Engines);
        Assert.Equal("Observed", engine.CommandCatalog.State);
        Assert.Equal("file-bridge", engine.CommandCatalog.Transport);
        Assert.Equal(2, engine.CommandCatalog.EvidencePaths.Count);
        var fsmKit = FindKit(result.Catalog, "FsmKit");
        Assert.Equal("Drifted", fsmKit.Availability);
        Assert.False(fsmKit.DeclaredCommand);
        Assert.Contains(fsmKit.ObservedCommands, command => command.Action == "get_state");
        Assert.Equal(1, client.CommandCallCount);
        Assert.Equal("System", client.LastCommandKit);
        Assert.Equal("list_commands", client.LastCommandAction);
        Assert.Equal(YokiFrameCommandSourceContract.CODEX, client.LastCommandSource);
    }

    /// <summary>
    /// 验证命令执行期间宿主 generation 改变时，旧目录只保留为 Stale 证据，不提升 Kit 可用性。
    /// </summary>
    [Fact]
    public async Task BuildKeepsCatalogPartialWhenEngineChangesDuringRefresh()
    {
        var client = new CatalogTestClient(sNowUtc)
        {
            AdvanceRegistryAfterCommand = true,
            CommandCatalogJson = CreateCommandCatalogJson(includeFsmKit: false)
        };

        var result = await CreateService(client).BuildAsync(
            ENGINE_ID,
            true,
            "tests",
            1000,
            CancellationToken.None);

        Assert.Equal("Partial", result.State);
        Assert.Contains(result.Catalog.Issues, issue => issue.Code == "CommandCatalogStale");
        var engine = Assert.Single(result.Catalog.Engines);
        Assert.Equal("Stale", engine.CommandCatalog.State);
        Assert.NotEmpty(engine.CommandCatalog.Commands);
        var systemKit = FindKit(result.Catalog, "System");
        Assert.Equal("Declared", systemKit.Availability);
        Assert.Empty(systemKit.ObservedCommands);
        Assert.DoesNotContain(result.Catalog.Issues, issue => issue.Code == "HarnessCommandCatalogDrift");
    }

    /// <summary>
    /// 使用固定时间源创建能力目录服务，避免 heartbeat freshness 断言受机器时间影响。
    /// </summary>
    /// <param name="client">可控的内存 Client。</param>
    /// <returns>使用固定当前时间的能力目录服务。</returns>
    private static CapabilityCatalogService CreateService(CatalogTestClient client)
    {
        return new CapabilityCatalogService(client, new FixedTimeProvider(sNowUtc));
    }

    /// <summary>
    /// 从聚合目录中读取唯一 Kit，缺失或重复时直接形成清晰测试失败。
    /// </summary>
    /// <param name="catalog">待检查能力目录。</param>
    /// <param name="kit">目标 Kit 标识。</param>
    /// <returns>唯一匹配的 Kit 能力。</returns>
    private static CapabilityCatalogKit FindKit(CapabilityCatalog catalog, string kit)
    {
        return Assert.Single(catalog.Kits, candidate => candidate.Kit == kit);
    }

    /// <summary>
    /// 创建宿主返回的结构化 System/list_commands 结果，并按需加入未在静态 harness 声明的 FsmKit。
    /// </summary>
    /// <param name="includeFsmKit">是否加入 FsmKit/get_state。</param>
    /// <returns>可写入 terminal response 的业务 JSON。</returns>
    private static string CreateCommandCatalogJson(bool includeFsmKit)
    {
        JsonArray kits = new()
        {
            CreateCommandKit("System", "ping", "ReadOnly")
        };
        if (includeFsmKit)
        {
            kits.Add(CreateCommandKit("FsmKit", "get_state", "ReadOnly"));
        }

        JsonObject catalog = new()
        {
            ["engineId"] = ENGINE_ID,
            ["mode"] = "EditMode",
            ["sessionId"] = SESSION_ID,
            ["generation"] = GENERATION,
            ["sequence"] = 12L,
            ["kits"] = kits
        };
        return catalog.ToJsonString();
    }

    /// <summary>
    /// 创建一个只含单条 action 的命令 Kit 节点，保持测试 JSON 与 Runtime 目录结构一致。
    /// </summary>
    /// <param name="kit">Kit 标识。</param>
    /// <param name="action">action 标识。</param>
    /// <param name="kind">命令风险类型。</param>
    /// <returns>命令 Kit JSON 节点。</returns>
    private static JsonObject CreateCommandKit(string kit, string action, string kind)
    {
        return new JsonObject
        {
            ["kit"] = kit,
            ["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["action"] = action,
                    ["kind"] = kind
                }
            }
        };
    }

    /// <summary>
    /// 为 freshness 测试提供不会随测试执行时间变化的 UTC 时间。
    /// </summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset mNowUtc;

        /// <summary>
        /// 使用指定 UTC 时间创建固定时间源。
        /// </summary>
        /// <param name="nowUtc">测试认定的当前 UTC 时间。</param>
        public FixedTimeProvider(DateTimeOffset nowUtc)
        {
            mNowUtc = nowUtc;
        }

        /// <summary>
        /// 返回固定 UTC 时间，使 heartbeat 阈值断言可重复。
        /// </summary>
        /// <returns>构造时指定的 UTC 时间。</returns>
        public override DateTimeOffset GetUtcNow()
        {
            return mNowUtc;
        }
    }

    /// <summary>
    /// 为能力目录测试提供静态 harness、宿主身份和可控命令响应，不访问真实 FileBridge。
    /// </summary>
    private sealed class CatalogTestClient : IYokiFrameClient
    {
        private const string REQUEST_ID = "catalog-test";
        private readonly HeartbeatInfo mHeartbeat;
        private bool mRegistryAdvanced;

        /// <summary>
        /// 创建默认一致且在线的 Unity Editor 测试宿主。
        /// </summary>
        /// <param name="nowUtc">用于生成新鲜 heartbeat 的固定时间。</param>
        public CatalogTestClient(DateTimeOffset nowUtc)
        {
            Paths = new YokiFramePaths(Path.Combine(
                Path.GetTempPath(),
                "yokiframe-capability-catalog-tests",
                Guid.NewGuid().ToString("N")));
            CreateUnityProject();
            mHeartbeat = new HeartbeatInfo(
                Paths.GetHeartbeatPath(ENGINE_ID),
                ENGINE_ID,
                nowUtc.AddSeconds(-1),
                SESSION_ID,
                GENERATION,
                "EditMode",
                11L);
            CommandCatalogJson = CreateCommandCatalogJson(includeFsmKit: false);
            var model = new ProjectModelService(this, new FixedTimeProvider(nowUtc)).Refresh();
            if (!model.IsReady)
            {
                throw new InvalidOperationException("Failed to create the catalog Project Model fixture: "
                    + string.Join("; ", model.Issues.Select(issue => issue.Code)));
            }
        }

        /// <summary>获取测试项目路径解析器。</summary>
        public YokiFramePaths Paths { get; }

        /// <summary>获取或设置是否在命令完成后模拟新 generation registry。</summary>
        public bool AdvanceRegistryAfterCommand { get; set; }

        /// <summary>获取或设置测试 Client 是否返回 heartbeat。</summary>
        public bool HeartbeatAvailable { get; set; } = true;

        /// <summary>获取或设置是否模拟部分 registry 解析失败。</summary>
        public bool ThrowPartialRegistryReadException { get; set; }

        /// <summary>获取或设置 System/list_commands 的业务 JSON。</summary>
        public string CommandCatalogJson { get; set; }

        /// <summary>
        /// 获取或设置静态 harness JSON，用于验证必填字段缺失时的降级状态。
        /// </summary>
        public string HarnessJson { get; set; } = string.Empty;

        /// <summary>获取 FileBridge 命令调用次数。</summary>
        public int CommandCallCount { get; private set; }

        /// <summary>获取最近一次命令 Kit。</summary>
        public string LastCommandKit { get; private set; } = string.Empty;

        /// <summary>获取最近一次命令 action。</summary>
        public string LastCommandAction { get; private set; } = string.Empty;

        /// <summary>获取最近一次命令的审计来源。</summary>
        public string LastCommandSource { get; private set; } = string.Empty;

        /// <summary>使当前权威输入发生变化，供 stale 状态测试使用。</summary>
        public void ChangeProjectInput()
        {
            File.AppendAllText(Path.Combine(Paths.ProjectRoot, "Packages", "manifest.json"), "\n");
        }

        /// <summary>篡改 capabilities 叶文件，供 Project Model hash 门禁测试使用。</summary>
        public void TamperProjectCapabilities()
        {
            File.AppendAllText(Paths.ProjectCapabilitiesPath, "\n");
        }

        /// <summary>删除 Project Model manifest，供缺失状态测试使用。</summary>
        public void RemoveProjectModelManifest()
        {
            File.Delete(Paths.ProjectModelManifestPath);
        }

        /// <summary>创建扫描器要求的最小 Unity 项目和本地 YokiFrame 包。</summary>
        private void CreateUnityProject()
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Paths.ProjectRoot, "Assets"));
            Directory.CreateDirectory(System.IO.Path.Combine(Paths.ProjectRoot, "Packages"));
            Directory.CreateDirectory(System.IO.Path.Combine(Paths.ProjectRoot, "ProjectSettings"));
            Directory.CreateDirectory(System.IO.Path.Combine(Paths.ProjectRoot, "Assets", "YokiFrame"));
            File.WriteAllText(
                System.IO.Path.Combine(Paths.ProjectRoot, "Packages", "manifest.json"),
                "{\"dependencies\":{}}\n");
            File.WriteAllText(
                System.IO.Path.Combine(Paths.ProjectRoot, "ProjectSettings", "ProjectVersion.txt"),
                "m_EditorVersion: 2022.3.0f1\n");
            File.WriteAllText(
                System.IO.Path.Combine(Paths.ProjectRoot, "Assets", "YokiFrame", "package.json"),
                "{\"name\":\"com.hinatayoki.yokiframe\",\"version\":\"test\"}\n");
        }

        /// <summary>
        /// 返回只声明 System command、同时声明 System/FsmKit snapshot 的 bootstrap harness。
        /// </summary>
        /// <returns>静态 harness JSON。</returns>
        public JsonNode ReadHarnessCapabilities()
        {
            if (!string.IsNullOrWhiteSpace(HarnessJson))
            {
                return JsonNode.Parse(HarnessJson)!;
            }

            return JsonNode.Parse("""
                {
                  "schemaVersion": 1,
                  "generatedAtUtc": "2026-07-12T09:59:00.0000000Z",
                  "package": {
                    "name": "com.hinatayoki.yokiframe",
                    "version": "2.0.0-preview",
                    "packageRoot": "Assets/YokiFrame"
                  },
                  "protocol": {
                    "fileBridgeVersion": 2,
                    "sharedMemoryTelemetryVersion": 1,
                    "fastChannelVersion": 1
                  },
                  "engines": { "knownKinds": ["Unity"] },
                  "kits": {
                    "snapshots": ["System", "FsmKit"],
                    "commands": ["System"]
                  }
                }
                """)!;
        }

        /// <summary>
        /// 返回当前 registry；命令完成后可切换到新 session/generation 以模拟 Domain Reload。
        /// </summary>
        /// <returns>单个 Unity Editor registry 条目。</returns>
        public IReadOnlyList<EngineRegistryEntry> ReadEngineEntries()
        {
            if (ThrowPartialRegistryReadException)
            {
                throw new EngineRegistryReadException(
                    new[] { CreateRegistry(SESSION_ID, GENERATION) },
                    new[] { System.IO.Path.Combine(Paths.EnginesRoot, "broken", "engine.json") },
                    "One registry file is invalid.");
            }

            return new[]
            {
                CreateRegistry(
                    mRegistryAdvanced ? "session-b" : SESSION_ID,
                    mRegistryAdvanced ? GENERATION + 1L : GENERATION)
            };
        }

        /// <summary>
        /// 返回固定且与命令前 registry 一致的新鲜 heartbeat。
        /// </summary>
        /// <param name="engineId">目标 engine。</param>
        /// <returns>测试 heartbeat。</returns>
        public HeartbeatInfo? ReadHeartbeat(string engineId)
        {
            return HeartbeatAvailable && engineId == ENGINE_ID ? mHeartbeat : null;
        }

        /// <summary>
        /// 返回受控 System/list_commands terminal response，并按配置推进后续 registry 身份。
        /// </summary>
        /// <param name="engineId">目标 engine。</param>
        /// <param name="kit">目标 Kit。</param>
        /// <param name="action">目标 action。</param>
        /// <param name="payloadJson">命令 payload。</param>
        /// <param name="source">审计来源。</param>
        /// <param name="timeoutMs">命令超时。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>带 command/response 证据路径的成功结果。</returns>
        public Task<CommandSendResult> SendCommandAsync(
            string engineId,
            string kit,
            string action,
            string payloadJson,
            string source,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CommandCallCount++;
            LastCommandKit = kit;
            LastCommandAction = action;
            LastCommandSource = source;
            var envelope = CommandEnvelope.Create(
                engineId,
                source,
                REQUEST_ID,
                kit,
                action,
                payloadJson,
                timeoutMs);
            CommandResponse response = new()
            {
                ProtocolVersion = 2,
                RequestId = REQUEST_ID,
                EngineId = engineId,
                Status = "Success",
                ResultJson = CommandCatalogJson,
                CompletedAtUtc = sNowUtc.ToString("O")
            };
            mRegistryAdvanced = AdvanceRegistryAfterCommand;
            return Task.FromResult(new CommandSendResult(
                envelope,
                Paths.GetPendingCommandPath(engineId, REQUEST_ID),
                Paths.GetResponsePath(engineId, REQUEST_ID),
                response));
        }

        /// <summary>
        /// 创建指定宿主身份的 registry 副本，避免命令后变更反向污染命令前证据。
        /// </summary>
        /// <param name="sessionId">宿主 session。</param>
        /// <param name="generation">宿主 generation。</param>
        /// <returns>独立 registry 条目。</returns>
        private static EngineRegistryEntry CreateRegistry(string sessionId, long generation)
        {
            return new EngineRegistryEntry
            {
                ProtocolVersion = 2,
                EngineId = ENGINE_ID,
                Engine = "Unity",
                Version = "6000.7.0a1",
                AdapterVersion = "tests",
                SessionId = sessionId,
                Generation = generation,
                Mode = "EditMode",
                Capabilities = new List<string> { "snapshot.read", "command.send" }
            };
        }

        /// <summary>能力目录测试不读取 snapshot，意外调用时立即失败。</summary>
        public JsonNode ReadSnapshot(string engineId, string kit, string name) => throw new NotSupportedException();

        /// <summary>能力目录测试不读取 bridge 状态，意外调用时立即失败。</summary>
        public FileBridgeStatus ReadBridgeStatus(string engineId) => throw new NotSupportedException();

        /// <summary>能力目录测试不读取 telemetry，意外调用时立即失败。</summary>
        public SharedMemoryTelemetryFrameReadResult ReadTelemetry(
            string engineId,
            string kit,
            string name,
            long? expectedGeneration,
            int maxPayloadBytes) => throw new NotSupportedException();

        /// <summary>能力目录测试不读取增量 telemetry，意外调用时立即失败。</summary>
        public SharedMemoryTelemetryFrameReadResult? ReadTelemetryIfChanged(
            string engineId,
            string kit,
            string name,
            long? expectedGeneration,
            int maxPayloadBytes,
            long afterSequence) => throw new NotSupportedException();

        /// <summary>list_commands 不属于 FastChannel 白名单，意外调用时立即失败。</summary>
        public Task<CommandResponse> SendFastChannelReadOnlySystemCommandAsync(
            string engineId,
            string action,
            string source,
            int timeoutMs,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
