namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 为事务测试提供内部检查点 seam；生产构造函数始终使用无操作实现。
/// </summary>
internal interface IPackageInstallTransactionFaultInjector
{
    /// <summary>
    /// 在事务越过稳定检查点后通知观察者，测试可在此注入故障。
    /// </summary>
    /// <param name="checkpoint">当前稳定检查点。</param>
    void OnCheckpoint(PackageInstallTransactionCheckpoint checkpoint);
}
