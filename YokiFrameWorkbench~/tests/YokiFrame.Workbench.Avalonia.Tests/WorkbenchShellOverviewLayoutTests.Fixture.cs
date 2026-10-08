using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 提供框架总览行为测试使用的 dashboard 状态和卡片断言。
/// </summary>
public sealed partial class WorkbenchShellOverviewLayoutTests
{
    /// <summary>
    /// 创建包含 snapshot 成功和失败状态的 dashboard，验证总览区能同时展示两类结果。
    /// </summary>
    /// <returns>测试用 dashboard 状态。</returns>
    private static WorkbenchDashboardState CreateDashboardState()
    {
        return CreateDashboardState("F:/Project");
    }

    /// <summary>
    /// 创建包含 snapshot 成功和失败状态的 dashboard，验证总览区能同时展示两类结果。
    /// </summary>
    /// <param name="projectRoot">测试用项目根目录。</param>
    /// <returns>测试用 dashboard 状态。</returns>
    private static WorkbenchDashboardState CreateDashboardState(string projectRoot)
    {
        FileBridgeStatus status = new(
            "unity-editor",
            projectRoot + "/.yokiframe/engines/unity-editor",
            projectRoot + "/.yokiframe/engines/unity-editor/commands",
            projectRoot + "/.yokiframe/engines/unity-editor/results");
        WorkbenchBridgeHealth health = new(
            WorkbenchBridgeConnectionState.Online,
            "FileBridge is online for unity-editor.",
            "No action needed.",
            new[] { status.EngineRoot },
            1,
            15,
            "test-session",
            7,
            "EditMode",
            3);
        WorkbenchSnapshotState[] snapshots =
        {
            new("EventKit", "workbench", projectRoot + "/event.json", "snapshot", true, "{\"listeners\":2}", string.Empty),
            new("LogKit", "workbench", projectRoot + "/log.json", "telemetry", false, string.Empty, "stale")
        };

        return new WorkbenchDashboardState(
            projectRoot,
            DateTimeOffset.UtcNow,
            Array.Empty<EngineRegistryEntry>(),
            "unity-editor",
            status,
            health,
            null,
            snapshots,
            "{}",
            Array.Empty<string>());
    }

    /// <summary>
    /// 断言指定标题的卡片包含预期主值和详情。
    /// </summary>
    /// <param name="cards">卡片集合。</param>
    /// <param name="title">卡片标题。</param>
    /// <param name="value">预期主值。</param>
    /// <param name="detail">预期详情。</param>
    private static void AssertMetricCard(
        IReadOnlyList<WorkbenchMetricCard> cards,
        string title,
        string value,
        string detail)
    {
        var card = Assert.Single(cards, item => item.Title == title);
        Assert.Equal(value, card.Value);
        Assert.Equal(detail, card.Detail);
    }

    /// <summary>
    /// 创建包含一个包内 Skill 的最小项目目录。
    /// </summary>
    /// <param name="skillName">Skill 名称。</param>
    /// <returns>测试项目根目录。</returns>
    private static string CreateProjectWithPackagedSkill(string skillName)
    {
        var root = Path.Combine(Path.GetTempPath(), "yokiframe-workbench-skill-tests", Guid.NewGuid().ToString("N"));
        var skillRoot = Path.Combine(root, "Assets", "YokiFrame", "Core", "Editor", "Skills", skillName);
        Directory.CreateDirectory(skillRoot);
        File.WriteAllText(Path.Combine(skillRoot, "SKILL.md"), "---\nname: " + skillName + "\ndescription: test\n---\n");
        return root;
    }
}
