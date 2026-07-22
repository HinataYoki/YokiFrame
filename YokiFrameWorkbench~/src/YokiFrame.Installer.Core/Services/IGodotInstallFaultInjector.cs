namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 为 Godot 整体安装测试提供内部检查点 seam；生产构造函数使用无操作实现。
/// </summary>
internal interface IGodotInstallFaultInjector
{
    /// <summary>
    /// 在外层 owner 文件越过稳定提交边界后通知测试观察者。
    /// </summary>
    /// <param name="checkpoint">刚完成的外层文件提交检查点。</param>
    void OnCheckpoint(GodotInstallCheckpoint checkpoint);
}
