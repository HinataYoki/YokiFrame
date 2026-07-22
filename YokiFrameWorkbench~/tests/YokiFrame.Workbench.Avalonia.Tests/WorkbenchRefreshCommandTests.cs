using System.Reflection;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Workbench 页头唯一刷新入口的聚合刷新契约。
/// </summary>
public sealed class WorkbenchRefreshCommandTests
{
    /// <summary>
    /// 验证页头刷新会同时请求 dashboard 并重新读取当前项目的 Skill 状态。
    /// </summary>
    [Fact]
    public void GlobalRefreshAlsoRefreshesSkillStatus()
    {
        var dashboardRefreshCount = 0;
        var viewModel = new WorkbenchShellViewModel(
            () => dashboardRefreshCount++,
            _ => { },
            _ => Task.CompletedTask);
        var statusField = typeof(WorkbenchShellViewModel).GetField(
            "mSkillInstallStatusText",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(statusField);
        statusField.SetValue(viewModel, "过期状态");

        viewModel.RefreshCommand.Execute(null);

        Assert.Equal(1, dashboardRefreshCount);
        Assert.Equal("等待项目状态", viewModel.SkillInstallStatusText);
    }
}
