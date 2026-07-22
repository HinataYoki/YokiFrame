namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 表示 Godot 外层 owner 文件或包提交失败，并公开整体回滚和诊断结果。
/// </summary>
public sealed class GodotInstallException : IOException
{
    /// <summary>
    /// 创建 Godot 整体安装失败异常。
    /// </summary>
    /// <param name="message">失败说明。</param>
    /// <param name="diagnosticEvidencePath">持久化诊断 JSON 路径。</param>
    /// <param name="rollbackSucceeded">是否已经恢复全部外层 owner 文件。</param>
    /// <param name="innerException">原始安装失败。</param>
    public GodotInstallException(
        string message,
        string diagnosticEvidencePath,
        bool rollbackSucceeded,
        Exception innerException)
        : base(message, innerException)
    {
        DiagnosticEvidencePath = diagnosticEvidencePath;
        RollbackSucceeded = rollbackSucceeded;
    }

    /// <summary>
    /// 获取持久化诊断 JSON 路径。
    /// </summary>
    public string DiagnosticEvidencePath { get; }

    /// <summary>
    /// 获取是否已经恢复全部外层 owner 文件。
    /// </summary>
    public bool RollbackSucceeded { get; }
}
