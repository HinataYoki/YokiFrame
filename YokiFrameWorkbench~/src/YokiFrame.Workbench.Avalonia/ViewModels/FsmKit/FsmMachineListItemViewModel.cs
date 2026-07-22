using YokiFrame.Tooling.Application.Models.FsmKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels.FsmKit;

/// <summary>
/// 表示左侧活动状态机列表中的稳定项；以 instanceId 保持对象身份，只增量更新可见字段。
/// </summary>
public sealed class FsmMachineListItemViewModel : ViewModelBase
{
    private string mName;
    private string mMachineState;
    private string mCurrentState;
    private int mCurrentStateId;
    private int mStateCount;

    /// <summary>
    /// 从 Runtime 摘要创建稳定列表项；后续刷新必须复用同一 instanceId 对象。
    /// </summary>
    /// <param name="summary">Application 已解析的状态机摘要。</param>
    internal FsmMachineListItemViewModel(WorkbenchFsmMachineSummary summary)
    {
        InstanceId = summary.InstanceId;
        mName = summary.Name;
        mMachineState = summary.MachineState;
        mCurrentState = summary.CurrentState;
        mCurrentStateId = summary.CurrentStateId;
        mStateCount = summary.StateCount;
    }

    /// <summary>获取当前 session 内稳定且唯一的实例标识。</summary>
    public string InstanceId { get; }

    /// <summary>获取用户可见状态机名称。</summary>
    public string Name { get => mName; private set => SetProperty(ref mName, value); }

    /// <summary>获取状态机生命周期阶段。</summary>
    public string MachineState { get => mMachineState; private set => SetProperty(ref mMachineState, value); }

    /// <summary>获取当前状态名称。</summary>
    public string CurrentState { get => mCurrentState; private set => SetProperty(ref mCurrentState, value); }

    /// <summary>获取当前状态整数标识。</summary>
    public int CurrentStateId { get => mCurrentStateId; private set => SetProperty(ref mCurrentStateId, value); }

    /// <summary>获取当前状态节点数量。</summary>
    public int StateCount { get => mStateCount; private set => SetProperty(ref mStateCount, value); }

    /// <summary>
    /// 合并同一实例的新摘要；字段按需通知，避免 ListBox 重新创建 item container。
    /// </summary>
    /// <param name="summary">instanceId 必须与当前项一致的新摘要。</param>
    internal void Apply(WorkbenchFsmMachineSummary summary)
    {
        if (!string.Equals(InstanceId, summary.InstanceId, StringComparison.Ordinal))
        {
            throw new ArgumentException("FSM summary instanceId does not match the stable list item.", nameof(summary));
        }

        Name = summary.Name;
        MachineState = summary.MachineState;
        CurrentState = summary.CurrentState;
        CurrentStateId = summary.CurrentStateId;
        StateCount = summary.StateCount;
    }

    /// <summary>判断当前项是否匹配列表搜索词。</summary>
    /// <param name="query">已 Trim 的搜索文本。</param>
    /// <returns>名称、当前状态或 instanceId 命中时返回 true。</returns>
    internal bool Matches(string query)
    {
        return query.Length == 0
            || Contains(Name, query)
            || Contains(CurrentState, query)
            || Contains(InstanceId, query);
    }

    /// <summary>执行不区分大小写的包含匹配。</summary>
    private static bool Contains(string value, string query)
    {
        return value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
