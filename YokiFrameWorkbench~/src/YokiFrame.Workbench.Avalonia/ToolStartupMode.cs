namespace YokiFrame.Workbench.Avalonia;

/// <summary>
/// 表示单一 Avalonia 工具应用的启动界面模式。
/// </summary>
public enum ToolStartupMode
{
    /// <summary>
    /// 用户直接打开工具时显示安装计划界面。
    /// </summary>
    Installer,

    /// <summary>
    /// 引擎侧携带项目初始化信息启动时显示 Workbench 界面。
    /// </summary>
    Workbench
}
