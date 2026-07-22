using YokiFrame.Tooling.Application.Models.ResKit;
using System.Globalization;

namespace YokiFrame.Workbench.Avalonia.ViewModels.ResKit;

/// <summary>包装一条卸载记录，并为列表更新保留稳定身份。</summary>
public sealed class ResKitHistoryListItemViewModel : ViewModelBase
{
    private WorkbenchResKitUnloadRecord mRecord;
    private string mUnloadTimeText = string.Empty;

    /// <summary>创建绑定指定卸载记录的稳定列表项。</summary>
    public ResKitHistoryListItemViewModel(WorkbenchResKitUnloadRecord record)
    {
        mRecord = record;
        UpdateUnloadTimeText();
    }

    /// <summary>获取记录稳定身份。</summary>
    public string Identity => mRecord.Identity;
    /// <summary>获取资源路径。</summary>
    public string Path => mRecord.Path;
    /// <summary>获取资源类型。</summary>
    public string TypeName => mRecord.TypeName;
    /// <summary>获取 Provider 名称。</summary>
    public string ProviderName => mRecord.ProviderName;
    /// <summary>获取 UTC 卸载时间。</summary>
    public string UnloadTimeUtc => mRecord.UnloadTimeUtc;
    /// <summary>获取适合宽表展示的紧凑 UTC 时间。</summary>
    public string UnloadTimeText => mUnloadTimeText;

    /// <summary>应用同身份新帧并通知全部绑定字段。</summary>
    internal void Update(WorkbenchResKitUnloadRecord record)
    {
        mRecord = record;
        UpdateUnloadTimeText();
        OnPropertyChanged(nameof(Path));
        OnPropertyChanged(nameof(TypeName));
        OnPropertyChanged(nameof(ProviderName));
        OnPropertyChanged(nameof(UnloadTimeUtc));
        OnPropertyChanged(nameof(UnloadTimeText));
    }

    /// <summary>在记录变更时格式化 UTC 时间，避免列表绑定重复解析相同协议文本。</summary>
    private void UpdateUnloadTimeText()
    {
        mUnloadTimeText = FormatUnloadTime(mRecord.UnloadTimeUtc);
    }

    /// <summary>将协议 UTC 文本压缩为稳定的月日与毫秒时间，解析失败时保留原值。</summary>
    private static string FormatUnloadTime(string value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time)
            ? time.ToUniversalTime().ToString("MM-dd HH:mm:ss.fff 'UTC'", CultureInfo.InvariantCulture)
            : value;
    }
}
