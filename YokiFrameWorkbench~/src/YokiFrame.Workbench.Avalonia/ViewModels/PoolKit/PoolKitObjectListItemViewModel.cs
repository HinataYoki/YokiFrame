using System.Windows.Input;
using YokiFrame.Tooling.Application.Models.PoolKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels.PoolKit;

/// <summary>包装 PoolKit 对象明细，并提供受控源码定位命令。</summary>
public sealed class PoolKitObjectListItemViewModel
{
    private readonly Func<WorkbenchPoolKitObject, Task>? mOpenLocationAsync;

    /// <summary>创建对象明细；缺少有效源码位置或宿主回调时定位命令保持禁用。</summary>
    /// <param name="item">Runtime 投影出的有界对象明细。</param>
    /// <param name="openLocationAsync">通过宿主代码编辑器打开借出位置的可选回调。</param>
    public PoolKitObjectListItemViewModel(
        WorkbenchPoolKitObject item,
        Func<WorkbenchPoolKitObject, Task>? openLocationAsync)
    {
        Item = item;
        mOpenLocationAsync = openLocationAsync;
        OpenCommand = new AsyncRelayCommand(OpenAsync, CanOpen);
    }

    /// <summary>获取原始对象明细。</summary>
    public WorkbenchPoolKitObject Item { get; }
    /// <summary>获取对象显示名。</summary>
    public string ObjectName => Item.ObjectName;
    /// <summary>获取借出时刻。</summary>
    public double SpawnTime => Item.SpawnTime;
    /// <summary>获取源码文件路径。</summary>
    public string SourceFile => Item.SourceFile;
    /// <summary>获取源码行号。</summary>
    public int SourceLine => Item.SourceLine;
    /// <summary>获取是否具备有效源码位置。</summary>
    public bool HasSourceLocation => Item.HasSourceLocation;
    /// <summary>获取紧凑源码位置文本，完整路径保留给提示和打开命令。</summary>
    public string SourceDisplay => Path.GetFileName(SourceFile) + ":" + SourceLine;
    /// <summary>获取完整源码位置文本。</summary>
    public string FullSourceText => SourceFile + ":" + SourceLine;
    /// <summary>获取屏幕阅读器使用的完整定位说明。</summary>
    public string OpenAutomationName => "打开 " + FullSourceText;
    /// <summary>获取通过宿主代码编辑器打开位置的命令。</summary>
    public ICommand OpenCommand { get; }

    /// <summary>仅在位置有效且宿主提供打开边界时允许点击。</summary>
    /// <returns>允许发送源码定位请求时返回 true。</returns>
    private bool CanOpen() => HasSourceLocation && mOpenLocationAsync != null;

    /// <summary>调用注入的平台边界打开当前对象的借出位置。</summary>
    /// <returns>宿主完成源码定位请求后的任务。</returns>
    private async Task OpenAsync()
    {
        if (mOpenLocationAsync != null) await mOpenLocationAsync(Item);
    }
}
