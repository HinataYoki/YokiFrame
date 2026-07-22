using System.Windows.Input;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 描述 Workbench Skill 安装面板中的单个 AI 目标。
/// </summary>
public sealed class WorkbenchSkillTarget
{
    /// <summary>
    /// 创建 Skill 目标卡片。
    /// </summary>
    /// <param name="id">目标标识。</param>
    /// <param name="label">显示名。</param>
    /// <param name="relativePath">相对项目根的安装目录。</param>
    /// <param name="statusText">安装状态文本。</param>
    /// <param name="isInstalled">当前选中 Skill 是否已安装。</param>
    /// <param name="installCommand">安装命令。</param>
    /// <param name="uninstallCommand">卸载命令。</param>
    public WorkbenchSkillTarget(
        string id,
        string label,
        string relativePath,
        string statusText,
        bool isInstalled,
        ICommand installCommand,
        ICommand uninstallCommand)
    {
        Id = id;
        Label = label;
        RelativePath = relativePath;
        StatusText = statusText;
        IsInstalled = isInstalled;
        InstallCommand = installCommand;
        UninstallCommand = uninstallCommand;
    }

    /// <summary>
    /// 获取目标标识。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 获取显示名。
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// 获取相对项目根的安装目录。
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// 获取安装状态文本。
    /// </summary>
    public string StatusText { get; }

    /// <summary>
    /// 获取当前选中 Skill 是否已安装。
    /// </summary>
    public bool IsInstalled { get; }

    /// <summary>
    /// 获取安装命令。
    /// </summary>
    public ICommand InstallCommand { get; }

    /// <summary>
    /// 获取卸载命令。
    /// </summary>
    public ICommand UninstallCommand { get; }
}
