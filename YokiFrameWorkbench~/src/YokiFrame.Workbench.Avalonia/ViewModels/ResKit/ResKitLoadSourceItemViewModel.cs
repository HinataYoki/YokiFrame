using System.Windows.Input;
using YokiFrame.Tooling.Application.Models.ResKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels.ResKit;

/// <summary>包装一条 lease 来源，并提供受控源码定位命令。</summary>
public sealed class ResKitLoadSourceItemViewModel
{
    private readonly Func<WorkbenchResKitLoadSource, Task>? mOpenLocationAsync;

    /// <summary>创建来源项；没有有效源码位置或宿主边界时定位命令保持禁用。</summary>
    public ResKitLoadSourceItemViewModel(
        WorkbenchResKitLoadSource source,
        Func<WorkbenchResKitLoadSource, Task>? openLocationAsync)
    {
        Source = source;
        mOpenLocationAsync = openLocationAsync;
        OpenCommand = new AsyncRelayCommand(OpenAsync, CanOpen);
    }

    /// <summary>获取原始来源模型。</summary>
    public WorkbenchResKitLoadSource Source { get; }
    /// <summary>获取来源展示名。</summary>
    public string Display => Source.Display;
    /// <summary>获取源码文件路径。</summary>
    public string FilePath => Source.FilePath;
    /// <summary>获取源码行号。</summary>
    public int Line => Source.Line;
    /// <summary>获取该来源合并的引用数。</summary>
    public int RefCount => Source.RefCount;
    /// <summary>获取来源是否匿名。</summary>
    public bool IsAnonymous => Source.IsAnonymous;
    /// <summary>获取来源是否由位置跟踪采集。</summary>
    public bool IsTracked => Source.IsTracked;
    /// <summary>获取源码定位命令。</summary>
    public ICommand OpenCommand { get; }
    /// <summary>获取屏幕阅读器使用的完整定位说明。</summary>
    public string OpenAutomationName => "打开 " + FilePath + ":" + Line;

    /// <summary>判断当前来源是否可请求宿主打开。</summary>
    private bool CanOpen() => Source.HasSourceLocation && mOpenLocationAsync != null;

    /// <summary>调用注入边界打开当前来源。</summary>
    private async Task OpenAsync()
    {
        if (mOpenLocationAsync != null) await mOpenLocationAsync(Source);
    }
}
