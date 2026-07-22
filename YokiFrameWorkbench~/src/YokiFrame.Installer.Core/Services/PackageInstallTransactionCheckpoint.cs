namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 标识 Installer 包事务中可验证和故障注入的提交边界。
/// </summary>
internal enum PackageInstallTransactionCheckpoint
{
    /// <summary>
    /// 完整投影和 owner manifest 已写入 staging 并通过 hash 复验。
    /// </summary>
    StagingVerified,

    /// <summary>
    /// 已有正式包已经移动到事务备份区。
    /// </summary>
    ExistingPackageBackedUp,

    /// <summary>
    /// staging 包已经移动为正式目标目录。
    /// </summary>
    TargetCommitted
}
