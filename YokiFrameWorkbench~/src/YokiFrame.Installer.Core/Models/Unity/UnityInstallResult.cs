namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述一次 Unity 安装执行成功后的稳定结果。
/// </summary>
public sealed class UnityInstallResult
{
    /// <summary>
    /// 创建 Unity 安装结果。
    /// </summary>
    /// <param name="plan">实际执行的安装计划。</param>
    /// <param name="packageTransaction">embedded 包事务结果；Git 模式为空。</param>
    /// <param name="manifestChanged">Packages/manifest.json 是否发生内容变化。</param>
    public UnityInstallResult(
        UnityInstallPlan plan,
        PackageInstallTransactionResult? packageTransaction,
        bool manifestChanged)
    {
        Plan = plan;
        PackageTransaction = packageTransaction;
        ManifestChanged = manifestChanged;
    }

    /// <summary>
    /// 获取实际执行的安装计划。
    /// </summary>
    public UnityInstallPlan Plan { get; }

    /// <summary>
    /// 获取 embedded 包事务结果；Git 模式返回空。
    /// </summary>
    public PackageInstallTransactionResult? PackageTransaction { get; }

    /// <summary>
    /// 获取 Packages/manifest.json 是否发生内容变化。
    /// </summary>
    public bool ManifestChanged { get; }
}
