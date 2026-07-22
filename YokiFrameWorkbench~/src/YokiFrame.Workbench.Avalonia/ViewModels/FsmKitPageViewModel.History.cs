using System.Collections.ObjectModel;
using YokiFrame.Tooling.Application.Models.FsmKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>维护右侧转换历史的稳定集合和有界窗口增量同步。</summary>
public sealed partial class FsmKitPageViewModel
{
    private readonly ObservableCollection<WorkbenchFsmTransition> mTransitions = new();

    /// <summary>增量同步 Runtime 有界历史，避免每个详情帧替换 ItemsSource。</summary>
    /// <param name="history">按时间排列的最新转换窗口。</param>
    private void SynchronizeTransitionHistory(IReadOnlyList<WorkbenchFsmTransition> history)
    {
        var overlap = FindTransitionOverlap(history);
        if (overlap == 0 && mTransitions.Count == history.Count)
        {
            ReplaceChangedTransitions(history);
            return;
        }

        var removeCount = mTransitions.Count - overlap;
        for (var index = 0; index < removeCount; index++)
        {
            mTransitions.RemoveAt(0);
        }

        for (var index = overlap; index < history.Count; index++)
        {
            mTransitions.Add(history[index]);
        }
    }

    /// <summary>查找旧窗口后缀与新窗口前缀的最大重叠，支持容量滚动时只移除最旧项。</summary>
    /// <param name="history">最新转换窗口。</param>
    /// <returns>可以直接复用的连续转换数量。</returns>
    private int FindTransitionOverlap(IReadOnlyList<WorkbenchFsmTransition> history)
    {
        var maximum = Math.Min(mTransitions.Count, history.Count);
        for (var count = maximum; count > 0; count--)
        {
            if (MatchesTransitionWindow(history, count))
            {
                return count;
            }
        }

        return 0;
    }

    /// <summary>判断旧历史后缀是否与新历史指定长度的前缀完全一致。</summary>
    private bool MatchesTransitionWindow(
        IReadOnlyList<WorkbenchFsmTransition> history,
        int count)
    {
        var oldStart = mTransitions.Count - count;
        for (var index = 0; index < count; index++)
        {
            if (!AreSameTransition(mTransitions[oldStart + index], history[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>在无法形成滚动窗口时按索引替换真实变化项，仍保持集合对象身份。</summary>
    private void ReplaceChangedTransitions(IReadOnlyList<WorkbenchFsmTransition> history)
    {
        for (var index = 0; index < history.Count; index++)
        {
            if (!AreSameTransition(mTransitions[index], history[index]))
            {
                mTransitions[index] = history[index];
            }
        }
    }

    /// <summary>比较转换业务值，避免等价新 DTO 触发容器重绑。</summary>
    private static bool AreSameTransition(
        WorkbenchFsmTransition current,
        WorkbenchFsmTransition candidate)
    {
        return string.Equals(current.From, candidate.From, StringComparison.Ordinal)
            && string.Equals(current.To, candidate.To, StringComparison.Ordinal)
            && string.Equals(current.Time, candidate.Time, StringComparison.Ordinal);
    }

    /// <summary>清空当前历史但保留集合实例，供选择、宿主或页面状态重置使用。</summary>
    private void ClearTransitionHistory()
    {
        mTransitions.Clear();
    }
}
