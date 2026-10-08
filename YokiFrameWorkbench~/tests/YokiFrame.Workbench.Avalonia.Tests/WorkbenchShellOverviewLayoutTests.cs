using System.Text;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖框架总览页可观察的日志、Skill 安装和状态卡片契约。
/// </summary>
public sealed partial class WorkbenchShellOverviewLayoutTests
{
    /// <summary>
    /// 验证复制日志包含已发送命令，并且清空命令会移除全部运行日志。
    /// </summary>
    [Fact]
    public void CreateLogClipboardText_已记录发送命令_复制文本包含命令且清空后无日志()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);

        viewModel.ShowCommandInFlight("System", "ping");
        var text = viewModel.CreateLogClipboardText();
        Assert.True(viewModel.ClearLogCommand.CanExecute(null));
        viewModel.ClearLogCommand.Execute(null);

        Assert.Contains("System/ping", text, StringComparison.Ordinal);
        Assert.Empty(viewModel.LogLines);
    }

    /// <summary>
    /// 验证 ViewModel 为命令发送、响应和错误写入不同类型的运行日志。
    /// </summary>
    [Fact]
    public void ShowCommandResult_发送接收和错误_日志行类型与消息匹配()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);

        viewModel.ShowCommandInFlight("System", "ping");
        viewModel.ShowCommandResult(new WorkbenchCommandState("System", "ping", true, "Ok", "{\"pong\":true}", string.Empty));
        viewModel.ShowTransientError("bridge timeout");

        Assert.Equal(WorkbenchLogLineKind.Outbound, viewModel.LogLines[^3].Kind);
        Assert.Contains("命令发送", viewModel.LogLines[^3].Message, StringComparison.Ordinal);
        Assert.Equal(WorkbenchLogLineKind.Inbound, viewModel.LogLines[^2].Kind);
        Assert.Contains("命令接收", viewModel.LogLines[^2].Message, StringComparison.Ordinal);
        Assert.Equal(WorkbenchLogLineKind.Error, viewModel.LogLines[^1].Kind);
        Assert.Contains("错误", viewModel.LogLines[^1].Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证自定义目录安装命令会把当前选中的 Skill 写入项目内目标目录。
    /// </summary>
    [Fact]
    public void InstallCustomSkillCommand_自定义目录_写入Skill文件()
    {
        var projectRoot = CreateProjectWithPackagedSkill("yokiframe");
        try
        {
            var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);
            viewModel.UpdateDashboard(CreateDashboardState(projectRoot));
            viewModel.CustomSkillPath = "custom/skills";

            Assert.True(viewModel.InstallCustomSkillCommand.CanExecute(null));
            viewModel.InstallCustomSkillCommand.Execute(null);

            Assert.True(File.Exists(Path.Combine(projectRoot, "custom", "skills", "yokiframe", "SKILL.md")));
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    /// <summary>
    /// 验证已安装目标把按钮文案切成更新，并且再次安装会整目录替换旧文档。
    /// </summary>
    [Fact]
    public void InstallCommand_已安装目标_替换目录并移除过期文件()
    {
        var projectRoot = CreateProjectWithPackagedSkill("yokiframe");
        var targetRoot = Path.Combine(projectRoot, ".codex", "skills", "yokiframe");
        try
        {
            Directory.CreateDirectory(targetRoot);
            File.WriteAllText(Path.Combine(targetRoot, "SKILL.md"), "old");
            File.WriteAllText(Path.Combine(targetRoot, "stale.md"), "stale");
            File.WriteAllText(
                Path.Combine(projectRoot, "Assets", "YokiFrame", "Core", "Editor", "Skills", "yokiframe", "SKILL.md"),
                "new-content");
            var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);

            viewModel.UpdateDashboard(CreateDashboardState(projectRoot));
            var codex = Assert.Single(viewModel.SkillTargets, target => target.Id == "codex");
            Assert.True(codex.IsInstalled);
            Assert.True(codex.ActionText == "更新" || codex.ActionText == "Update");
            Assert.True(codex.InstallCommand.CanExecute(null));
            codex.InstallCommand.Execute(null);

            var installed = File.ReadAllBytes(Path.Combine(targetRoot, "SKILL.md"));
            Assert.Equal(Encoding.UTF8.GetBytes("new-content"), installed);
            Assert.False(File.Exists(Path.Combine(targetRoot, "stale.md")));
            var updated = Assert.Single(viewModel.SkillTargets, target => target.Id == "codex");
            Assert.True(updated.ActionText == "更新" || updated.ActionText == "Update");
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    /// <summary>
    /// 验证 ViewModel 能把首批 snapshot 投影为总览卡片，供实时数据区域直接展示。
    /// </summary>
    [Fact]
    public void UpdateDashboard_有成功和失败快照_投影对应总览卡片()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);

        viewModel.UpdateDashboard(CreateDashboardState());

        AssertMetricCard(viewModel.SnapshotCards, "EventKit", "snapshot", "available");
        AssertMetricCard(viewModel.SnapshotCards, "LogKit", "telemetry", "missing: stale");
    }

    /// <summary>
    /// 验证桥接状态卡片只保留框架总览首屏真正需要的紧凑诊断信息。
    /// </summary>
    [Fact]
    public void FrameworkOverviewProjectsTauriLikeEngineStatusCards()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);

        viewModel.UpdateDashboard(CreateDashboardState());

        Assert.InRange(viewModel.EngineCards.Count, 1, 4);
        Assert.Contains(viewModel.EngineCards, card => card.Title == "心跳" || card.Title == "Heartbeat");
        Assert.Contains(viewModel.EngineCards, card => card.Title == "命令" || card.Title == "Command");
        Assert.Contains(viewModel.EngineCards, card => card.Title == "事件" || card.Title == "Events");
        Assert.Contains(viewModel.EngineCards, card => card.Title == "背压" || card.Title == "Pressure");
    }
}
