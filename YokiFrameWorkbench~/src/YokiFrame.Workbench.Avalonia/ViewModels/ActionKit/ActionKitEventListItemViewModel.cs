using YokiFrame.Tooling.Application.Models.ActionKit;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.ViewModels.ActionKit;

/// <summary>提供可按终态稳定键复用的 ActionKit 事件行。</summary>
public sealed class ActionKitEventListItemViewModel : ViewModelBase
{
    private string mActionType = string.Empty;
    private string mErrorMessage = string.Empty;

    /// <summary>创建一个终态事件行。</summary>
    /// <param name="item">Application 强类型事件。</param>
    public ActionKitEventListItemViewModel(WorkbenchActionKitEvent item)
    {
        ActionId = item.ActionId;
        Outcome = item.Outcome;
        Frame = item.Frame;
        Apply(item);
    }

    /// <summary>获取根 Action ID。</summary>
    public string ActionId { get; }

    /// <summary>获取根 Action 类型。</summary>
    public string ActionType { get => mActionType; private set => SetProperty(ref mActionType, value); }

    /// <summary>获取原始终态名称。</summary>
    public string Outcome { get; }

    /// <summary>获取终态发生帧。</summary>
    public long Frame { get; }

    /// <summary>获取故障摘要。</summary>
    public string ErrorMessage
    {
        get => mErrorMessage;
        private set
        {
            if (SetProperty(ref mErrorMessage, value)) OnPropertyChanged(nameof(HasError));
        }
    }

    /// <summary>获取本地化终态名称。</summary>
    public string OutcomeText => Outcome switch
    {
        "Completed" => "完成",
        "Cancelled" => "取消",
        "Faulted" => "故障",
        _ => Outcome
    };

    /// <summary>获取帧号展示文本。</summary>
    public string FrameText => "Frame " + Frame;

    /// <summary>获取 ID 展示文本。</summary>
    public string ActionIdText => "#" + ActionId;

    /// <summary>获取是否包含故障详情。</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>判断新事件是否与当前行表示同一终态。</summary>
    /// <param name="item">待匹配 Application 事件。</param>
    /// <returns>Action ID、Outcome 与 Frame 全部一致时返回 true。</returns>
    internal bool Matches(WorkbenchActionKitEvent item)
    {
        return Frame == item.Frame
            && string.Equals(ActionId, item.ActionId, StringComparison.Ordinal)
            && string.Equals(Outcome, item.Outcome, StringComparison.Ordinal);
    }

    /// <summary>更新同一终态的可变展示字段。</summary>
    /// <param name="item">同一稳定键的最新事件。</param>
    internal void Apply(WorkbenchActionKitEvent item)
    {
        if (!Matches(item))
            throw new InvalidOperationException("ActionKit event identity cannot change during an in-place update.");
        ActionType = item.ActionType;
        ErrorMessage = item.ErrorMessage;
    }
}
