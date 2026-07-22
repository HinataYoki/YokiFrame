using Avalonia.Controls;
using Avalonia.Threading;
using YokiFrame.Tooling.Application.Models.ActionKit;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.ViewModels.ActionKit;
using YokiFrame.Workbench.Avalonia.Views.Pages;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>覆盖 ActionKit 根动作进入终态后的 Avalonia 选择保留行为。</summary>
public sealed partial class ActionKitPageViewModelTests
{
    /// <summary>验证选中根完成并离开活动集合后，列表仍保留选中项和最后动作树。</summary>
    [Fact]
    public async Task CompletedSelectedRootRemainsSelectedInList()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            WorkbenchActionKitState initialState = CreateState(1L, 0);
            ActionKitPageViewModel viewModel = new();
            viewModel.ApplyPeriodicState(initialState);
            ActionKitPageView view = new() { DataContext = viewModel };
            Window window = new() { Width = 1420, Height = 820, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                ListBox roots = view.FindControl<ListBox>("ActionRoots")!;
                roots.SelectedItem = viewModel.Roots[1];
                viewModel.SelectedNode = viewModel.Roots[1].Children[0];
                ActionKitRootViewModel selectedRoot = viewModel.Roots[1];
                ActionKitNodeViewModel selectedNode = Assert.IsType<ActionKitNodeViewModel>(viewModel.SelectedNode);

                viewModel.ApplyPeriodicState(CreateCompletedState(initialState));
                Dispatcher.UIThread.RunJobs();

                Assert.Same(selectedRoot, roots.SelectedItem);
                Assert.Same(selectedRoot, viewModel.SelectedRoot);
                Assert.Same(selectedNode, viewModel.SelectedNode);
                Assert.Equal("Finished", selectedRoot.Status);
                Assert.Contains(selectedRoot, viewModel.FilteredRoots);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>创建第二个根已完成、且终态历史已发布的新版本状态。</summary>
    /// <param name="initialState">包含待完成根及其最后完整动作树的初始状态。</param>
    /// <returns>只保留第一个活动根，并为原第二个根提供完成事件的状态。</returns>
    private static WorkbenchActionKitState CreateCompletedState(WorkbenchActionKitState initialState)
    {
        WorkbenchActionKitRoot completedRoot = initialState.Roots[1];
        WorkbenchActionKitEvent terminalEvent = new(
            completedRoot.ActionId,
            completedRoot.Type,
            "Completed",
            241L,
            string.Empty);
        return CreateState(
            2L,
            0,
            rootsOverride: new[] { initialState.Roots[0] },
            eventsOverride: new[] { terminalEvent });
    }
}
