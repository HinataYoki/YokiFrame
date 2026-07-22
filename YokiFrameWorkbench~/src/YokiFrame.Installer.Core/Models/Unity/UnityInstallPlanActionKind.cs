namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 表示 Unity 安装计划中的来源互斥和提交动作。
/// </summary>
public enum UnityInstallPlanActionKind
{
    /// <summary>
    /// 通过文件级投影事务安装或更新 embedded 包。
    /// </summary>
    InstallEmbeddedPackage,

    /// <summary>
    /// 在 Git 模式提交前安全移除已有 embedded 包。
    /// </summary>
    RemoveEmbeddedPackage,

    /// <summary>
    /// 将 embedded package 的本地 file 依赖写入 Unity manifest。
    /// </summary>
    SetEmbeddedDependency,

    /// <summary>
    /// 结构化设置 manifest 中的 YokiFrame Git 依赖。
    /// </summary>
    SetGitDependency
}
