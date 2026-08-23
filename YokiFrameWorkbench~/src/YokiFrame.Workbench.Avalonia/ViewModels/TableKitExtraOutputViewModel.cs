using System.Windows.Input;
using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>TableKit 额外输出目标的可编辑行。</summary>
public sealed class TableKitExtraOutputViewModel : ViewModelBase
{
    private readonly Action<TableKitExtraOutputViewModel> mRemove;
    private readonly IInstallerFolderPicker? mFolderPicker;
    private readonly string mProjectRoot;
    private string mTargetName;
    private string mCodeTarget;
    private string mDataTarget;
    private string mOutputDataDir;
    private string mOutputCodeDir;

    /// <summary>创建一个额外输出目标行。</summary>
    /// <param name="model">初始模型。</param>
    /// <param name="remove">移除回调。</param>
    public TableKitExtraOutputViewModel(
        TableKitExtraOutput model,
        Action<TableKitExtraOutputViewModel> remove,
        IReadOnlyList<string>? targetOptions = null,
        IReadOnlyList<string>? codeTargetOptions = null,
        IReadOnlyList<string>? dataTargetOptions = null,
        string? projectRoot = null,
        IInstallerFolderPicker? folderPicker = null)
    {
        mProjectRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(projectRoot) ? Directory.GetCurrentDirectory() : projectRoot);
        mTargetName = string.IsNullOrWhiteSpace(model.TargetName) ? "server" : model.TargetName;
        mCodeTarget = string.IsNullOrWhiteSpace(model.CodeTarget) ? "java-json" : model.CodeTarget;
        mDataTarget = string.IsNullOrWhiteSpace(model.DataTarget) ? "json" : model.DataTarget;
        mOutputDataDir = TableKitPathUtilities.ToRelative(mProjectRoot, model.OutputDataDir);
        mOutputCodeDir = TableKitPathUtilities.ToRelative(mProjectRoot, model.OutputCodeDir);
        mRemove = remove ?? throw new ArgumentNullException(nameof(remove));
        mFolderPicker = folderPicker;
        TargetOptions = targetOptions ?? Array.Empty<string>();
        CodeTargetOptions = codeTargetOptions ?? Array.Empty<string>();
        DataTargetOptions = dataTargetOptions ?? Array.Empty<string>();
        RemoveCommand = new RelayCommand(() => mRemove(this));
        BrowseDataCommand = new AsyncRelayCommand(BrowseDataAsync);
        BrowseCodeCommand = new AsyncRelayCommand(BrowseCodeAsync);
    }

    /// <summary>获取或设置额外输出 target。</summary>
    public string TargetName { get => mTargetName; set => SetProperty(ref mTargetName, value); }
    /// <summary>获取或设置额外代码 target。</summary>
    public string CodeTarget { get => mCodeTarget; set => SetProperty(ref mCodeTarget, value); }
    /// <summary>获取或设置额外数据 target。</summary>
    public string DataTarget { get => mDataTarget; set => SetProperty(ref mDataTarget, value); }
    /// <summary>获取或设置额外数据目录。</summary>
    public string OutputDataDir { get => mOutputDataDir; set => SetProperty(ref mOutputDataDir, value); }
    /// <summary>获取或设置额外代码目录。</summary>
    public string OutputCodeDir { get => mOutputCodeDir; set => SetProperty(ref mOutputCodeDir, value); }
    /// <summary>移除当前额外输出目标。</summary>
    public ICommand RemoveCommand { get; }
    /// <summary>选择额外数据输出目录。</summary>
    public AsyncRelayCommand BrowseDataCommand { get; }
    /// <summary>选择额外代码输出目录。</summary>
    public AsyncRelayCommand BrowseCodeCommand { get; }
    /// <summary>可选的 target 名称集合。</summary>
    public IReadOnlyList<string> TargetOptions { get; }
    /// <summary>可选的 code target 集合。</summary>
    public IReadOnlyList<string> CodeTargetOptions { get; }
    /// <summary>可选的 data target 集合。</summary>
    public IReadOnlyList<string> DataTargetOptions { get; }

    /// <summary>转换为 Application 层模型。</summary>
    /// <returns>额外输出目标。</returns>
    public TableKitExtraOutput ToModel() => new()
    {
        TargetName = TargetName,
        CodeTarget = CodeTarget,
        DataTarget = DataTarget,
        OutputDataDir = OutputDataDir,
        OutputCodeDir = OutputCodeDir
    };

    /// <summary>通过跨平台目录选择器更新额外数据输出目录。</summary>
    private async Task BrowseDataAsync() => OutputDataDir = await BrowseAsync(
        WorkbenchI18nService.Instance.GetString("String.TableKit.PickExtraDataDirTitle", "选择额外数据输出目录"), OutputDataDir);

    /// <summary>通过跨平台目录选择器更新额外代码输出目录。</summary>
    private async Task BrowseCodeAsync() => OutputCodeDir = await BrowseAsync(
        WorkbenchI18nService.Instance.GetString("String.TableKit.PickExtraCodeDirTitle", "选择额外代码输出目录"), OutputCodeDir);

    /// <summary>从当前字段路径打开目录选择器，并保持项目相对显示。</summary>
    /// <param name="title">原生选择器标题。</param>
    /// <param name="currentPath">字段当前路径。</param>
    /// <returns>用户确认后的相对路径；取消时保留原值。</returns>
    private async Task<string> BrowseAsync(string title, string currentPath)
    {
        if (mFolderPicker == null) return currentPath;
        string suggested = TableKitPathUtilities.FindPickerStartDirectory(mProjectRoot, currentPath, false);
        string? selected = await mFolderPicker.PickFolderAsync(title, suggestedPath: suggested);
        return string.IsNullOrWhiteSpace(selected)
            ? currentPath
            : TableKitPathUtilities.ToRelative(mProjectRoot, selected);
    }
}
