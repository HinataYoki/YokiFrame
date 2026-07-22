namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 描述 Application 可识别的 Installer 目标宿主。
/// </summary>
public enum InstallerTargetKind
{
    /// <summary>
    /// 当前目录不是受支持项目。
    /// </summary>
    Unknown,

    /// <summary>
    /// Unity 2022.3 或更高版本项目。
    /// </summary>
    Unity,

    /// <summary>
    /// Godot .NET 项目。
    /// </summary>
    Godot
}

/// <summary>
/// 描述自动检测后供 UI 或 CLI 显示的目标项目只读模型。
/// </summary>
public sealed class InstallerTargetInfo
{
    /// <summary>
    /// 创建目标项目只读模型。
    /// </summary>
    /// <param name="kind">目标宿主类型。</param>
    /// <param name="projectRoot">规范化项目根。</param>
    /// <param name="packageTarget">YokiFrame 包目标根；未知项目为空。</param>
    /// <param name="evidencePaths">证明检测结果的路径。</param>
    public InstallerTargetInfo(
        InstallerTargetKind kind,
        string projectRoot,
        string packageTarget,
        IReadOnlyList<string> evidencePaths)
    {
        Kind = kind;
        ProjectRoot = projectRoot;
        PackageTarget = packageTarget;
        EvidencePaths = evidencePaths.ToArray();
    }

    /// <summary>
    /// 获取目标宿主类型。
    /// </summary>
    public InstallerTargetKind Kind { get; }

    /// <summary>
    /// 获取规范化项目根。
    /// </summary>
    public string ProjectRoot { get; }

    /// <summary>
    /// 获取 YokiFrame 包目标根；未知项目为空。
    /// </summary>
    public string PackageTarget { get; }

    /// <summary>
    /// 获取证明检测结果的路径快照。
    /// </summary>
    public IReadOnlyList<string> EvidencePaths { get; }

    /// <summary>
    /// 获取当前目录是否是受支持项目。
    /// </summary>
    public bool IsRecognized => Kind != InstallerTargetKind.Unknown;
}
