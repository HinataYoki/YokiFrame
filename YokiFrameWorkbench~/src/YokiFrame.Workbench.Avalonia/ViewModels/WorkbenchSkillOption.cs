using System.Windows.Input;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 描述 Skill 安装面板顶部可点击的包内 Skill 入口。
/// </summary>
public sealed class WorkbenchSkillOption
{
    /// <summary>
    /// 创建 Skill 入口卡片。
    /// </summary>
    /// <param name="name">Skill 目录名。</param>
    /// <param name="label">面向用户的短标题。</param>
    /// <param name="description">辅助说明，通常显示 Skill 目录名。</param>
    /// <param name="isSelected">当前是否为选中 Skill。</param>
    /// <param name="selectCommand">点击卡片后选择该 Skill 的命令。</param>
    public WorkbenchSkillOption(string name, string label, string description, bool isSelected, ICommand selectCommand)
    {
        Name = name;
        Label = label;
        Description = description;
        IsSelected = isSelected;
        SelectCommand = selectCommand;
    }

    /// <summary>
    /// 获取 Skill 目录名。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取面向用户的短标题。
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// 获取辅助说明。
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 获取当前卡片是否为选中态。
    /// </summary>
    public bool IsSelected { get; }

    /// <summary>
    /// 获取点击卡片后选择该 Skill 的命令。
    /// </summary>
    public ICommand SelectCommand { get; }
}
