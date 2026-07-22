namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述 Unity 安装计划中的一个可审阅动作。
/// </summary>
public sealed class UnityInstallPlanAction
{
    /// <summary>
    /// 创建来源互斥或提交动作。
    /// </summary>
    /// <param name="kind">动作类型。</param>
    /// <param name="targetPath">动作影响的目标路径。</param>
    /// <param name="value">可选的目标值，例如 Git URL。</param>
    /// <param name="reason">生成该动作的原因。</param>
    public UnityInstallPlanAction(
        UnityInstallPlanActionKind kind,
        string targetPath,
        string? value,
        string reason)
    {
        Kind = kind;
        TargetPath = targetPath;
        Value = value;
        Reason = reason;
    }

    /// <summary>
    /// 获取动作类型。
    /// </summary>
    public UnityInstallPlanActionKind Kind { get; }

    /// <summary>
    /// 获取动作影响的目标路径。
    /// </summary>
    public string TargetPath { get; }

    /// <summary>
    /// 获取动作携带的可选目标值。
    /// </summary>
    public string? Value { get; }

    /// <summary>
    /// 获取动作生成原因。
    /// </summary>
    public string Reason { get; }
}
