namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 标识 Unity 来源切换中可验证和故障注入的提交边界。
/// </summary>
internal enum UnityInstallCheckpoint
{
    /// <summary>
    /// YokiFrame embedded package 的本地 file 依赖已原子写入正式 manifest，尚未执行提交后重读验证。
    /// </summary>
    EmbeddedDependencyPersisted,

    /// <summary>
    /// YokiFrame Git 依赖已原子写入正式 manifest，尚未执行提交后重读验证。
    /// </summary>
    GitDependencyPersisted
}
