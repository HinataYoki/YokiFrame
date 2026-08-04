using Avalonia.Controls;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// 负责组装 Workbench 和 Installer 的应用级依赖，避免 Application 生命周期类直接承担构造细节。
/// </summary>
public sealed class WorkbenchCompositionRoot
{
    /// <summary>
    /// 根据启动模式创建唯一主窗口及其 Application 服务图。
    /// </summary>
    /// <param name="options">已经解析并校验的工具启动选项。</param>
    /// <param name="activationCoordinator">Workbench 单实例激活协调器；Installer 模式不使用。</param>
    /// <returns>已完成依赖组装的 Avalonia 主窗口。</returns>
    public Window CreateMainWindow(
        ToolStartupOptions options,
        WorkbenchActivationCoordinator? activationCoordinator)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Mode == ToolStartupMode.Workbench
            ? new WorkbenchWindow(
                new WorkbenchDashboardService(options.ProjectRoot),
                options,
                activationCoordinator)
            : new InstallerWindow(options);
    }
}
