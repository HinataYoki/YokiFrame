namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖能力目录的输入完整性、身份可信度和反向漂移状态。
/// </summary>
public sealed partial class CapabilityCatalogServiceTests
{
    /// <summary>验证 Project Model 输入变化会把能力目录降级为 Drifted。</summary>
    [Fact]
    public async Task BuildMarksStaleProjectModelAsDrifted()
    {
        var client = new CatalogTestClient(sNowUtc);
        client.ChangeProjectInput();

        var result = await CreateService(client).BuildAsync(
            string.Empty,
            false,
            "tests",
            1000,
            CancellationToken.None);

        Assert.Equal("Drifted", result.State);
        Assert.Equal("Stale", result.Catalog.Project.ModelState);
        Assert.Contains(result.Catalog.Issues, issue => issue.Code == "ProjectModelStale");
    }

    /// <summary>验证叶文件篡改触发 Project Model hash 门禁并阻断能力目录。</summary>
    [Fact]
    public async Task BuildBlocksTamperedProjectModel()
    {
        var client = new CatalogTestClient(sNowUtc);
        client.TamperProjectCapabilities();

        var result = await CreateService(client).BuildAsync(
            string.Empty,
            false,
            "tests",
            1000,
            CancellationToken.None);

        Assert.Equal("Blocked", result.State);
        Assert.Equal("Blocked", result.Catalog.Project.ModelState);
        Assert.Contains(result.Catalog.Issues, issue => issue.Code == "ProjectModelHashMismatch");
    }

    /// <summary>验证缺少 Project Model manifest 时目录返回 Partial 而不是假装 Ready。</summary>
    [Fact]
    public async Task BuildReportsMissingProjectModelAsPartial()
    {
        var client = new CatalogTestClient(sNowUtc);
        client.RemoveProjectModelManifest();

        var result = await CreateService(client).BuildAsync(
            string.Empty,
            false,
            "tests",
            1000,
            CancellationToken.None);

        Assert.Equal("Partial", result.State);
        Assert.Equal("Missing", result.Catalog.Project.ModelState);
        Assert.Contains(result.Catalog.Issues, issue => issue.Code == "ProjectModelMissing");
    }

    /// <summary>
    /// 验证 harness 缺少协议必填字段时只标记为 Partial，并保留结构化 HarnessInvalid 问题。
    /// </summary>
    [Fact]
    public async Task BuildMarksIncompleteHarnessAsPartial()
    {
        var client = new CatalogTestClient(sNowUtc)
        {
            HarnessJson = "{\"schemaVersion\":1,\"package\":{\"name\":\"YokiFrame\"}}"
        };

        var result = await CreateService(client).BuildAsync(
            string.Empty,
            false,
            "tests",
            1000,
            CancellationToken.None);

        Assert.Equal("Partial", result.State);
        Assert.Equal("Ready", result.Catalog.Project.ModelState);
        Assert.Equal("com.hinatayoki.yokiframe", result.Catalog.Project.PackageName);
        Assert.Contains(result.Catalog.Issues, issue => issue.Code == "HarnessInvalid");
    }

    /// <summary>
    /// 验证 refresh 无法选择在线 engine 时返回结构化 Partial，而不是让选择异常逃逸。
    /// </summary>
    [Fact]
    public async Task BuildReportsSelectionFailureWithoutSendingCommand()
    {
        var client = new CatalogTestClient(sNowUtc)
        {
            HeartbeatAvailable = false
        };

        var result = await CreateService(client).BuildAsync(
            string.Empty,
            true,
            "tests",
            1000,
            CancellationToken.None);

        Assert.Equal("Partial", result.State);
        Assert.Contains(result.Catalog.Issues, issue => issue.Code == "EngineUnavailable");
        Assert.Equal("NotRequested", Assert.Single(result.Catalog.Engines).CommandCatalog.State);
        Assert.Equal(0, client.CommandCallCount);
    }

    /// <summary>
    /// 验证显式 engine refresh 即使收到成功目录，也不会在 heartbeat 缺失时提升为 Observed。
    /// </summary>
    [Fact]
    public async Task BuildDoesNotTrustCommandCatalogWithoutCompletionHeartbeat()
    {
        var client = new CatalogTestClient(sNowUtc)
        {
            HeartbeatAvailable = false,
            CommandCatalogJson = CreateCommandCatalogJson(includeFsmKit: true)
        };

        var result = await CreateService(client).BuildAsync(
            ENGINE_ID,
            true,
            "tests",
            1000,
            CancellationToken.None);

        var engine = Assert.Single(result.Catalog.Engines);
        Assert.Equal("Stale", engine.CommandCatalog.State);
        Assert.Contains(result.Catalog.Issues, issue => issue.Code == "CommandCatalogHeartbeatMissing");
        Assert.Empty(FindKit(result.Catalog, "FsmKit").ObservedCommands);
    }

    /// <summary>
    /// 验证 Project Model 已有正式声明时，旧 harness 的额外 command Kit 不得覆盖正式目录。
    /// </summary>
    [Fact]
    public async Task BuildMarksDeclaredCommandKitMissingFromObservedCatalogAsDrifted()
    {
        var client = new CatalogTestClient(sNowUtc)
        {
            HarnessJson = """
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
                    "commands": ["System", "GhostKit"]
                  }
                }
                """
        };

        var result = await CreateService(client).BuildAsync(
            ENGINE_ID,
            true,
            "tests",
            1000,
            CancellationToken.None);

        Assert.Equal("Drifted", result.State);
        Assert.Equal("Declared", FindKit(result.Catalog, "GhostKit").Availability);
        Assert.Empty(FindKit(result.Catalog, "GhostKit").ObservedCommands);
        Assert.DoesNotContain("GhostKit", result.Catalog.Project.DeclaredKitIds);
    }

    /// <summary>
    /// 验证一个损坏 registry 不会抹掉其它健康 engine，并保留坏文件路径证据。
    /// </summary>
    [Fact]
    public async Task BuildPreservesHealthyEngineWhenAnotherRegistryIsInvalid()
    {
        var client = new CatalogTestClient(sNowUtc)
        {
            ThrowPartialRegistryReadException = true
        };

        var result = await CreateService(client).BuildAsync(
            string.Empty,
            false,
            "tests",
            1000,
            CancellationToken.None);

        Assert.Equal("Partial", result.State);
        Assert.Single(result.Catalog.Engines);
        var issue = Assert.Single(result.Catalog.Issues, candidate => candidate.Code == "EngineRegistryInvalid");
        Assert.Contains("broken", issue.EvidencePaths.Single(), StringComparison.OrdinalIgnoreCase);
    }
}
