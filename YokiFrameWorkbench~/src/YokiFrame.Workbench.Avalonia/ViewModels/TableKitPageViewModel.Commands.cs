using System.Windows.Input;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>声明 TableKit 页面命令，执行逻辑留在对应职责分部。</summary>
public sealed partial class TableKitPageViewModel
{
    /// <summary>读取 Luban 配置并显示临时 JSON 预览。</summary>
    public AsyncRelayCommand ValidateCommand { get; }
    /// <summary>执行 Luban 正式生成。</summary>
    public AsyncRelayCommand GenerateCommand { get; }
    /// <summary>重新读取 target 列表和环境状态。</summary>
    public ICommand RefreshConfigCommand { get; }
    /// <summary>保存 Workbench-only 配置。</summary>
    public ICommand SaveCommand { get; }
    /// <summary>还原当前项目默认配置。</summary>
    public ICommand ResetCommand { get; }
    /// <summary>添加额外导出目标。</summary>
    public ICommand AddExtraOutputCommand { get; }
    /// <summary>复制控制台日志。</summary>
    public AsyncRelayCommand CopyConsoleCommand { get; }
    /// <summary>清空控制台日志。</summary>
    public ICommand ClearConsoleCommand { get; }
    /// <summary>选择 Luban 工作目录。</summary>
    public AsyncRelayCommand BrowseLubanWorkDirCommand { get; }
    /// <summary>选择实际 Luban.dll 文件。</summary>
    public AsyncRelayCommand BrowseLubanExecutableCommand { get; }
    /// <summary>选择可选 Luban.Agent 文件。</summary>
    public AsyncRelayCommand BrowseLubanAgentCommand { get; }
    /// <summary>选择可选 Luban.Mcp 文件。</summary>
    public AsyncRelayCommand BrowseLubanMcpCommand { get; }
    /// <summary>选择可选 Luban Skill 目录。</summary>
    public AsyncRelayCommand BrowseLubanSkillsCommand { get; }
    /// <summary>选择数据输出目录。</summary>
    public AsyncRelayCommand BrowseOutputDataCommand { get; }
    /// <summary>选择代码输出目录。</summary>
    public AsyncRelayCommand BrowseOutputCodeCommand { get; }
    /// <summary>选择编辑器数据目录。</summary>
    public AsyncRelayCommand BrowseEditorDataCommand { get; }
    /// <summary>在系统文件管理器中打开配置表目录。</summary>
    public AsyncRelayCommand OpenConfigDirectoryCommand { get; }

}
