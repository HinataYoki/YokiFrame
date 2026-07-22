namespace YokiFrame.Workbench.Avalonia.ViewModels.EventKit;

/// <summary>把同一源码文件内的多个 EventKit 调用点合并为一个紧凑文件组。</summary>
public sealed class EventKitCodeLocationGroupViewModel
{
    /// <summary>创建保持源码顺序的文件调用点组。</summary>
    /// <param name="filePath">项目内规范化的相对 C# 文件路径。</param>
    /// <param name="locations">属于同一文件且保持扫描顺序的调用位置。</param>
    public EventKitCodeLocationGroupViewModel(
        string filePath,
        IReadOnlyList<EventKitCodeLocationItemViewModel> locations)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        Locations = locations;
    }

    /// <summary>获取项目相对完整路径。</summary>
    public string FilePath { get; }
    /// <summary>获取紧凑文件名。</summary>
    public string FileName { get; }
    /// <summary>获取当前文件内可逐个打开的调用行。</summary>
    public IReadOnlyList<EventKitCodeLocationItemViewModel> Locations { get; }
}
