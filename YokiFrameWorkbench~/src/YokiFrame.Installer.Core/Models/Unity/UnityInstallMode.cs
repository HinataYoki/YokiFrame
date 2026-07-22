namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 表示 Unity 项目使用的 YokiFrame 安装来源。
/// </summary>
public enum UnityInstallMode
{
    /// <summary>
    /// 把受控文件投影安装为 UPM embedded package。
    /// </summary>
    Embedded,

    /// <summary>
    /// 只在 Packages/manifest.json 中登记 Git URL 依赖。
    /// </summary>
    GitUrl
}
