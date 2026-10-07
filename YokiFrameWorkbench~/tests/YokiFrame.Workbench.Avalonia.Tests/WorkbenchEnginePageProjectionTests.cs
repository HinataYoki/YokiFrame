using System.Reflection;
using System.Text.Json;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Services;
using YokiFrame.Workbench.Avalonia.Pages;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// Engine 页面数据投影：能力矩阵与入口目录摘要必须来自 Engine state snapshot。
/// </summary>
public sealed class WorkbenchEnginePageProjectionTests
{
    [Fact]
    public void Engine_page_projects_capability_matrix_and_live_services()
    {
        string projectRoot = CreateProjectRoot();
        try
        {
            WriteEngineRegistry(projectRoot, withHeartbeat: true);
            WriteEngineSnapshot(projectRoot);

            string text = Project(new WorkbenchDashboardService(projectRoot).LoadDashboard(string.Empty));

            // 基础状态
            Assert.Contains("Host targets", text, StringComparison.Ordinal);
            Assert.Contains("editor|play", text, StringComparison.Ordinal);
            Assert.Contains("sess-page", text, StringComparison.Ordinal);
            Assert.Contains("generation 3", text, StringComparison.Ordinal);

            // 能力矩阵：数量 + 逐目标可用性 + 开关造成的禁用状态
            Assert.Contains("Operations: 2 registered (ReadOnly 2)", text, StringComparison.Ordinal);
            Assert.Contains("Run an action", text, StringComparison.Ordinal);
            Assert.Contains("scene_query: ReadOnly -> editor:Enabled, play:DisabledBySettings", text, StringComparison.Ordinal);
            Assert.Contains("domain_state: ReadOnly -> editor:Enabled, play:Enabled", text, StringComparison.Ordinal);

            // 入口目录摘要：数量、截断标记、清单与不满足契约的原因
            Assert.Contains("Live services: 2 discovered (list truncated)", text, StringComparison.Ordinal);
            Assert.Contains("Game.StatsModel: Game.Architecture (Game)", text, StringComparison.Ordinal);
            Assert.Contains("Game.StatsSystem: Game.Architecture (Game)", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Entries:", text, StringComparison.Ordinal);
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    [Fact]
    public void Engine_page_marks_dangerous_actions_and_groups_by_kind()
    {
        string projectRoot = CreateProjectRoot();
        try
        {
            // 自包含：registry + heartbeat + 快照的 generation 必须一致，否则读取器按「陈旧」处理。
            string engineRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor");
            Directory.CreateDirectory(Path.Combine(engineRoot, "status"));
            File.WriteAllText(
                Path.Combine(engineRoot, "engine.json"),
                "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"engine\":\"Unity\",\"sessionId\":\"sess-page\",\"generation\":5}");
            File.WriteAllText(
                Path.Combine(engineRoot, "status", "heartbeat.json"),
                "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"sessionId\":\"sess-page\",\"generation\":5,\"mode\":\"EditMode\",\"sequence\":7,\"createdAtUtc\":\""
                    + DateTimeOffset.UtcNow.ToString("O") + "\"}");

            // 覆盖成"含 Dangerous 操作 + 开关关闭 + 未接入口执行器"的 payload；用序列化构造，避免手写转义。
            string payload = JsonSerializer.Serialize(new
            {
                engineKind = "Unity",
                engineVersion = "2022.3.16f1",
                mode = "EditMode",
                activeTarget = "editor",
                isPlaying = false,
                isCompiling = false,
                isBusy = false,
                sessionId = "sess-page",
                generation = 3L,
                sessionIdentityAvailable = true,
                settingsState = "Disabled",
                settingsReason = "switch is off",
                hostTargets = "editor|play",
                capabilities = new object[]
                {
                    new
                    {
                        action = "domain_state",
                        kind = "ReadOnly",
                        targets = "editor|play",
                        targetAvailability = new[] { new { target = "editor", availability = "Enabled" } }
                    },
                    new
                    {
                        action = "eval",
                        kind = "Dangerous",
                        targets = "editor",
                        targetAvailability = new[] { new { target = "editor", availability = "DisabledBySettings" } }
                    }
                },
                objectCatalogAvailable = true,
                objectCount = 1,
                truncatedObjects = false,
                objects = new object[]
                {
                    new { type = "Game.StatsModel", architecture = "Game.Architecture", assembly = "Game" }
                },
                recentRunCount = 1,
                truncatedRuns = false,
                recentRuns = new object[]
                {
                    new
                    {
                        runId = "run-42",
                        requestId = "req-42",
                        kind = "script",
                        target = "editor",
                        state = "Passed",
                        errorCode = string.Empty,
                        resultPath = ".yokiframe/engine/runs/run-42.result.json",
                        submittedAtUtc = "2026-10-05 14:00:00Z",
                        updatedAtUtc = "2026-10-05 14:00:02Z"
                    }
                }
            });

            string snapshotRoot = Path.Combine(engineRoot, "snapshots", "RoslynKit");
            Directory.CreateDirectory(snapshotRoot);
            File.WriteAllText(Path.Combine(snapshotRoot, "state.json"), JsonSerializer.Serialize(new
            {
                protocolVersion = 2,
                engineId = "unity-editor",
                kit = "RoslynKit",
                name = "state",
                generation = 5,
                sequence = 10,
                writtenAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                payloadJson = payload
            }));

            WorkbenchDashboardState state = new WorkbenchDashboardService(projectRoot).LoadDashboard(string.Empty);
            string text = Project(state);

            // 按 kind 分组计数，Dangerous 单独可见
            Assert.Contains("Operations: 2 registered (ReadOnly 1 · Dangerous 1)", text, StringComparison.Ordinal);
            Assert.Contains("[needs confirmed:true + cli/workbench source]", text, StringComparison.Ordinal);
            Assert.Contains("DisabledBySettings", text, StringComparison.Ordinal);
            // 开关状态与原因要就地可见，而不是让人自己推断
            Assert.Contains("switch is off", text, StringComparison.Ordinal);
            Assert.Contains("Game.StatsModel: Game.Architecture (Game)", text, StringComparison.Ordinal);
            Assert.Contains("Recent runs: 1 recorded", text, StringComparison.Ordinal);
            Assert.Contains("script: Passed @ editor (run-42)", text, StringComparison.Ordinal);
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    [Fact]
    public void Engine_page_reports_unavailable_snapshot_with_recovery_hint()
    {
        // engine 在线（有 heartbeat）但 Engine state 快照文件缺失：给出原因与可执行的恢复提示。
        string projectRoot = CreateProjectRoot();
        try
        {
            WriteEngineRegistry(projectRoot, withHeartbeat: true);

            string text = Project(new WorkbenchDashboardService(projectRoot).LoadDashboard(string.Empty));

            Assert.Contains("Status: unavailable", text, StringComparison.Ordinal);
            Assert.Contains("Source: snapshot", text, StringComparison.Ordinal);
            Assert.Contains("Reason: ", text, StringComparison.Ordinal);
            Assert.Contains("Engine state snapshot", text, StringComparison.Ordinal);
            Assert.Contains("rebuild the host", text, StringComparison.Ordinal);
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    [Fact]
    public void Engine_page_reports_unregistered_snapshot_with_rebuild_hint()
    {
        // 没有在线 engine：Engine 未注册 state 快照，页面必须给出重建宿主的指引而不是空白。
        string projectRoot = CreateProjectRoot();
        try
        {
            WriteEngineRegistry(projectRoot, withHeartbeat: false);

            string text = Project(new WorkbenchDashboardService(projectRoot).LoadDashboard(string.Empty));

            Assert.Contains("Status: Engine snapshot is not registered", text, StringComparison.Ordinal);
            Assert.Contains("Rebuild the host", text, StringComparison.Ordinal);
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>写入最小 registry，并可选写入一份新鲜 heartbeat。</summary>
    /// <param name="projectRoot">项目根。</param>
    /// <param name="withHeartbeat">是否写入 heartbeat（决定 engine 是否在线）。</param>
    private static void WriteEngineRegistry(string projectRoot, bool withHeartbeat)
    {
        string engineRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor");
        Directory.CreateDirectory(engineRoot);
        File.WriteAllText(
            Path.Combine(engineRoot, "engine.json"),
            "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"engine\":\"Unity\",\"sessionId\":\"sess-page\",\"generation\":3}");
        if (!withHeartbeat)
        {
            return;
        }

        Directory.CreateDirectory(Path.Combine(engineRoot, "status"));
        File.WriteAllText(
            Path.Combine(engineRoot, "status", "heartbeat.json"),
            "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"sessionId\":\"sess-page\",\"generation\":3,\"mode\":\"EditMode\",\"sequence\":9,\"createdAtUtc\":\""
                + DateTimeOffset.UtcNow.ToString("O") + "\"}");
    }

    /// <summary>写入一份真实的 Engine state 快照信封（含能力矩阵与入口目录摘要）。</summary>
    /// <param name="projectRoot">项目根。</param>
    private static void WriteEngineSnapshot(string projectRoot)
    {
        string payload = "{" +
            "\"engineKind\":\"Unity\",\"engineVersion\":\"2022.3.16f1\",\"mode\":\"EditMode\",\"activeTarget\":\"editor\"," +
            "\"isPlaying\":false,\"isCompiling\":false,\"isBusy\":false," +
            "\"sessionId\":\"sess-page\",\"generation\":3,\"sessionIdentityAvailable\":true," +
            "\"settingsState\":\"Enabled\",\"settingsReason\":\"\"," +
            "\"hostTargets\":\"editor|play\"," +
            "\"capabilities\":[" +
            "{\"action\":\"domain_state\",\"kind\":\"ReadOnly\",\"targets\":\"editor|play|runtime\",\"isDiagnostic\":true,\"isCancellation\":false,\"exemptFromExecutionSwitch\":true,\"isTargetAgnostic\":true,\"targetAvailability\":[{\"target\":\"editor\",\"availability\":\"Enabled\"},{\"target\":\"play\",\"availability\":\"Enabled\"}]}," +
            "{\"action\":\"scene_query\",\"kind\":\"ReadOnly\",\"targets\":\"editor|play\",\"isDiagnostic\":false,\"isCancellation\":false,\"exemptFromExecutionSwitch\":false,\"isTargetAgnostic\":false,\"targetAvailability\":[{\"target\":\"editor\",\"availability\":\"Enabled\"},{\"target\":\"play\",\"availability\":\"DisabledBySettings\"}]}" +
            "]," +
            "\"objectCatalogAvailable\":true,\"objectCount\":2,\"truncatedObjects\":true," +
            "\"objects\":[" +
            "{\"type\":\"Game.StatsModel\",\"architecture\":\"Game.Architecture\",\"assembly\":\"Game\"}," +
            "{\"type\":\"Game.StatsSystem\",\"architecture\":\"Game.Architecture\",\"assembly\":\"Game\"}" +
            "]" +
            "}";

        string snapshotRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor", "snapshots", "RoslynKit");
        Directory.CreateDirectory(snapshotRoot);
        string envelope = JsonSerializer.Serialize(new
        {
            protocolVersion = 2,
            engineId = "unity-editor",
            kit = "RoslynKit",
            name = "state",
            generation = 3,
            sequence = 9,
            writtenAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            payloadJson = payload
        });
        File.WriteAllText(Path.Combine(snapshotRoot, "state.json"), envelope);
    }

    private static string CreateProjectRoot()
    {
        string projectRoot = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-engine-page-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectRoot);
        return projectRoot;
    }

    private static void DeleteProjectRoot(string projectRoot)
    {
        Directory.Delete(projectRoot, recursive: true);
    }

    /// <summary>把段落拼接成便于断言的文本。</summary>
    private static string Project(WorkbenchDashboardState state)
    {
        return string.Join("\n", InvokeCreateEngineSections(state).Select(static section => section.Label + ": " + section.Value));
    }

    /// <summary>通过反射调用 internal 投影器，沿用本工程既有的内部 API 测试方式。</summary>
    private static IReadOnlyList<WorkbenchDisplaySection> InvokeCreateEngineSections(WorkbenchDashboardState state)
    {
        MethodInfo method = typeof(WorkbenchPageSectionProjector)
            .GetMethod("CreateEngineSections", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("CreateEngineSections was not found.");
        return (IReadOnlyList<WorkbenchDisplaySection>)method.Invoke(null, new object[] { state })!;
    }
}
