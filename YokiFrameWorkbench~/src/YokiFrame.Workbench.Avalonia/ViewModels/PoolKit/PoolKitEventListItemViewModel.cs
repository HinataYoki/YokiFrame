using YokiFrame.Tooling.Application.Models.PoolKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels.PoolKit;

/// <summary>提供 PoolKit 事件流行的稳定身份与本地化展示文本。</summary>
public sealed class PoolKitEventListItemViewModel : ViewModelBase
{
    /// <summary>创建事件流行。</summary>
    public PoolKitEventListItemViewModel(WorkbenchPoolKitEvent item, int occurrence)
    {
        Item = item;
        Identity = item.Timestamp + "\u001f" + item.EventType + "\u001f" + item.PoolId + "\u001f" + item.PoolName
            + "\u001f" + item.ObjectName + "\u001f" + occurrence;
    }

    private WorkbenchPoolKitEvent Item { get; }
    /// <summary>获取帧内稳定事件身份。</summary>
    public string Identity { get; }
    /// <summary>获取事件类型。</summary>
    public string EventType => Item.EventType;
    /// <summary>获取本地化事件类型。</summary>
    public string EventTypeText => Item.EventType switch
    {
        "Spawn" => GetString("String.PoolKit.EventSpawn", "借出"),
        "Return" => GetString("String.PoolKit.EventReturn", "归还"),
        "Forced" => GetString("String.PoolKit.EventForced", "强制归还"),
        _ => Item.EventType
    };
    /// <summary>获取是否为借出事件。</summary>
    public bool IsSpawn => string.Equals(EventType, "Spawn", StringComparison.Ordinal);
    /// <summary>获取是否为正常归还事件。</summary>
    public bool IsReturn => string.Equals(EventType, "Return", StringComparison.Ordinal);
    /// <summary>获取是否为强制归还事件。</summary>
    public bool IsForced => string.Equals(EventType, "Forced", StringComparison.Ordinal);
    /// <summary>获取对象池名称。</summary>
    public string PoolName => Item.PoolName;
    /// <summary>获取对象池稳定标识。</summary>
    public string PoolId => Item.PoolId;
    /// <summary>获取对象显示名。</summary>
    public string ObjectName => Item.ObjectName;
    /// <summary>获取相对诊断时间文本。</summary>
    public string TimeText => Item.Timestamp.ToString("F2") + "s";
    /// <summary>获取源码位置文本。</summary>
    public string SourceText => Item.HasSourceLocation
        ? Path.GetFileName(Item.SourceFile) + ":" + Item.SourceLine
        : GetString("String.PoolKit.NoSourceLocation", "未记录定位");

    /// <summary>按当前语言刷新事件行的展示文本；行身份不变，仅重投影绑定属性。</summary>
    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(EventTypeText));
        OnPropertyChanged(nameof(SourceText));
    }

    /// <summary>从当前语言资源读取 PoolKit 文案，保留测试与无资源环境的中文兜底。</summary>
    private static string GetString(string key, string fallback)
    {
        return WorkbenchI18nService.Instance.GetString(key, fallback);
    }
}
