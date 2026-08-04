namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 描述不同宿主安装成功后统一返回给 UI 或 CLI 的结果。
/// </summary>
public sealed class InstallerExecutionResult
{
    /// <summary>
    /// 创建统一安装执行结果。
    /// </summary>
    /// <param name="targetPath">主要提交目标，例如包根或 Unity manifest。</param>
    /// <param name="changed">执行是否改变目标项目。</param>
    /// <param name="replacedExistingPackage">是否替换或移除了既有包来源。</param>
    /// <param name="evidencePaths">成功证据路径。</param>
    /// <param name="committedNeedsVerification">提交已完成但宿主 post-verify 尚未成功时为 true。</param>
    /// <param name="verificationError">post-verify 失败说明。</param>
    public InstallerExecutionResult(
        string targetPath,
        bool changed,
        bool replacedExistingPackage,
        IReadOnlyList<string>? evidencePaths = null,
        bool committedNeedsVerification = false,
        string verificationError = "")
    {
        TargetPath = targetPath;
        Changed = changed;
        ReplacedExistingPackage = replacedExistingPackage;
        EvidencePaths = evidencePaths?.ToArray() ?? Array.Empty<string>();
        CommittedNeedsVerification = committedNeedsVerification;
        VerificationError = verificationError ?? string.Empty;
    }

    /// <summary>
    /// 获取主要提交目标。
    /// </summary>
    public string TargetPath { get; }

    /// <summary>
    /// 获取执行是否改变目标项目。
    /// </summary>
    public bool Changed { get; }

    /// <summary>
    /// 获取是否替换或移除了既有包来源。
    /// </summary>
    public bool ReplacedExistingPackage { get; }

    /// <summary>
    /// 获取成功证据路径快照。
    /// </summary>
    public IReadOnlyList<string> EvidencePaths { get; }

    /// <summary>获取提交后仍需宿主验证的标记。</summary>
    public bool CommittedNeedsVerification { get; }

    /// <summary>获取提交后验证失败说明。</summary>
    public string VerificationError { get; }
}
