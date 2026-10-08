using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Workbench 页头唯一刷新入口的聚合刷新契约。
/// </summary>
public sealed class WorkbenchRefreshCommandTests
{
    /// <summary>
    /// 验证页头刷新会同时请求 dashboard，并把过期 Skill 状态收回等待项目状态。
    /// </summary>
    [Fact]
    public void 刷新命令_已有Skill状态_同时刷新Dashboard并回到等待()
    {
        var dashboardRefreshCount = 0;
        var viewModel = new WorkbenchShellViewModel(
            () => dashboardRefreshCount++,
            _ => { },
            _ => Task.CompletedTask);
        viewModel.SkillInstallStatusText = "过期状态";

        viewModel.RefreshCommand.Execute(null);

        Assert.Equal(1, dashboardRefreshCount);
        Assert.Equal("等待项目状态", viewModel.SkillInstallStatusText);
    }
}
