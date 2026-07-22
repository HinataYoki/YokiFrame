namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述安装器对目标项目的识别结果。
/// </summary>
public sealed class InstallerProjectInfo
{
    /// <summary>
    /// 创建目标项目信息。
    /// </summary>
    /// <param name="kind">项目类型。</param>
    /// <param name="projectRoot">目标项目根目录。</param>
    /// <param name="packageRoot">YokiFrame 包目标根目录。</param>
    /// <param name="evidencePaths">用于证明识别结果的路径。</param>
    public InstallerProjectInfo(
        InstallerProjectKind kind,
        string projectRoot,
        string packageRoot,
        IReadOnlyList<string> evidencePaths)
    {
        Kind = kind;
        ProjectRoot = projectRoot;
        PackageRoot = packageRoot;
        EvidencePaths = evidencePaths;
    }

    /// <summary>
    /// 获取项目类型。
    /// </summary>
    public InstallerProjectKind Kind { get; }

    /// <summary>
    /// 获取目标项目根目录。
    /// </summary>
    public string ProjectRoot { get; }

    /// <summary>
    /// 获取 YokiFrame 包目标根目录。
    /// </summary>
    public string PackageRoot { get; }

    /// <summary>
    /// 获取用于证明识别结果的路径。
    /// </summary>
    public IReadOnlyList<string> EvidencePaths { get; }
}
