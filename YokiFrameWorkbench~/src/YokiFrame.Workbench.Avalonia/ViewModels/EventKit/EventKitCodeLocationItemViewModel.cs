using System.Windows.Input;
using YokiFrame.Tooling.Application.Models.EventKit.Scan;

namespace YokiFrame.Workbench.Avalonia.ViewModels.EventKit;

/// <summary>把一个源码位置包装为带异步打开命令的只读 UI 项。</summary>
public sealed class EventKitCodeLocationItemViewModel
{
    private readonly Func<WorkbenchEventKitCodeLocation, Task>? mOpenLocationAsync;

    /// <summary>创建源码位置项；无宿主打开边界时命令保持禁用。</summary>
    public EventKitCodeLocationItemViewModel(
        WorkbenchEventKitCodeLocation location,
        Func<WorkbenchEventKitCodeLocation, Task>? openLocationAsync)
    {
        Location = location;
        mOpenLocationAsync = openLocationAsync;
        OpenCommand = new AsyncRelayCommand(OpenAsync, () => mOpenLocationAsync != null);
    }

    /// <summary>获取 Application 验证过的项目相对位置。</summary>
    public WorkbenchEventKitCodeLocation Location { get; }
    /// <summary>获取紧凑文件名。</summary>
    public string FileName => Location.FileName;
    /// <summary>获取项目相对完整路径。</summary>
    public string FilePath => Location.FilePath;
    /// <summary>获取源码行号。</summary>
    public int Line => Location.Line;
    /// <summary>获取显示文本。</summary>
    public string Display => Location.Display;
    /// <summary>获取屏幕阅读器使用的完整打开位置说明。</summary>
    public string OpenAutomationName => "打开 " + FilePath + ":" + Line;
    /// <summary>获取通过宿主代码编辑器打开位置的命令。</summary>
    public ICommand OpenCommand { get; }

    /// <summary>调用注入的平台边界打开当前代码位置。</summary>
    private async Task OpenAsync()
    {
        if (mOpenLocationAsync != null)
        {
            await mOpenLocationAsync(Location);
        }
    }
}
