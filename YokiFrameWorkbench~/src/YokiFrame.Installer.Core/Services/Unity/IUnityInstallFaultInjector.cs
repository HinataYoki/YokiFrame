namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 为 Unity 来源切换测试提供内部检查点 seam；生产构造函数使用无操作实现。
/// </summary>
internal interface IUnityInstallFaultInjector
{
    /// <summary>
    /// 在 Unity 安装越过稳定提交边界后通知测试观察者。
    /// </summary>
    /// <param name="checkpoint">刚完成的 Unity 安装检查点。</param>
    void OnCheckpoint(UnityInstallCheckpoint checkpoint);
}
