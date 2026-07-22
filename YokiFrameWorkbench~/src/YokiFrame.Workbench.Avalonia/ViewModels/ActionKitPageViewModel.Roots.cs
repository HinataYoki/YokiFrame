using YokiFrame.Tooling.Application.Models.ActionKit;
using YokiFrame.Workbench.Avalonia.ViewModels.ActionKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 ActionKit 根动作集合及终态选择保留逻辑。</summary>
public sealed partial class ActionKitPageViewModel
{
    /// <summary>同步活动根，并暂留刚进入终态的当前选择以避免列表清空选择。</summary>
    /// <param name="roots">Runtime 本轮仍处于活动状态的根动作。</param>
    /// <param name="retainedSelectedRoot">已确认进入终态、需要保留最后树快照的当前选择。</param>
    private void SynchronizeRoots(
        IReadOnlyList<WorkbenchActionKitRoot> roots,
        ActionKitRootViewModel? retainedSelectedRoot)
    {
        for (var targetIndex = 0; targetIndex < roots.Count; targetIndex++)
        {
            WorkbenchActionKitRoot root = roots[targetIndex];
            int existingIndex = FindRootIndex(root.ActionId, targetIndex);
            ActionKitRootViewModel viewModel;
            if (existingIndex < 0)
            {
                viewModel = new ActionKitRootViewModel(root);
                Roots.Insert(targetIndex, viewModel);
            }
            else
            {
                if (existingIndex != targetIndex) Roots.Move(existingIndex, targetIndex);
                viewModel = Roots[targetIndex];
                viewModel.Apply(root);
            }
        }

        for (var index = Roots.Count - 1; index >= roots.Count; index--)
        {
            if (!ReferenceEquals(Roots[index], retainedSelectedRoot)) Roots.RemoveAt(index);
        }
    }

    /// <summary>仅在选中根已经离开活动集合时，查找它最新的终态证据。</summary>
    /// <param name="roots">本轮活动根。</param>
    /// <param name="events">最新优先的有界终态事件。</param>
    /// <param name="actionId">当前选中根的稳定 ID。</param>
    /// <returns>匹配终态；根仍活动、没有选择或事件已被裁剪时返回 null。</returns>
    private static WorkbenchActionKitEvent? FindTerminalEventForMissingRoot(
        IReadOnlyList<WorkbenchActionKitRoot> roots,
        IReadOnlyList<WorkbenchActionKitEvent> events,
        string actionId)
    {
        if (string.IsNullOrEmpty(actionId)) return null;
        for (var index = 0; index < roots.Count; index++)
        {
            if (string.Equals(roots[index].ActionId, actionId, StringComparison.Ordinal)) return null;
        }

        for (var index = 0; index < events.Count; index++)
        {
            if (string.Equals(events[index].ActionId, actionId, StringComparison.Ordinal)) return events[index];
        }

        return null;
    }
}
