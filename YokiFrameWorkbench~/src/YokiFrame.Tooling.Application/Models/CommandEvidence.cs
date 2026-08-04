namespace YokiFrame.Tooling.Application.Models;

/// <summary>
/// 描述命令结果对应的证据持久性。
/// </summary>
public enum CommandEvidenceKind
{
    None,
    FileBacked,
    Ephemeral
}

/// <summary>
/// 描述一次命令结果的 transport-specific 证据。
/// </summary>
public sealed class CommandEvidence
{
    /// <summary>
    /// 创建命令证据。
    /// </summary>
    /// <param name="kind">证据持久性。</param>
    /// <param name="commandPath">FileBridge command 路径。</param>
    /// <param name="responsePath">FileBridge response 路径。</param>
    /// <param name="diagnostic">面向调用方的诊断说明。</param>
    public CommandEvidence(
        CommandEvidenceKind kind,
        string commandPath,
        string responsePath,
        string diagnostic)
    {
        Kind = kind;
        CommandPath = commandPath ?? string.Empty;
        ResponsePath = responsePath ?? string.Empty;
        Diagnostic = diagnostic ?? string.Empty;
    }

    /// <summary>获取证据持久性。</summary>
    public CommandEvidenceKind Kind { get; }

    /// <summary>获取 FileBridge command 路径；非 FileBridge 结果为空。</summary>
    public string CommandPath { get; }

    /// <summary>获取 FileBridge response 路径；非 FileBridge 结果为空。</summary>
    public string ResponsePath { get; }

    /// <summary>获取 transport-specific 诊断说明。</summary>
    public string Diagnostic { get; }

    /// <summary>
    /// 创建 FileBridge 文件证据。
    /// </summary>
    public static CommandEvidence FileBacked(string commandPath, string responsePath)
    {
        return new CommandEvidence(
            CommandEvidenceKind.FileBacked,
            commandPath,
            responsePath,
            "FileBridge command and terminal response are available as durable evidence.");
    }

    /// <summary>
    /// 创建 FastChannel 临时证据。
    /// </summary>
    public static CommandEvidence Ephemeral(string diagnostic)
    {
        return new CommandEvidence(CommandEvidenceKind.Ephemeral, string.Empty, string.Empty, diagnostic);
    }

    /// <summary>
    /// 创建无证据结果。
    /// </summary>
    public static CommandEvidence Empty(string diagnostic)
    {
        return new CommandEvidence(CommandEvidenceKind.None, string.Empty, string.Empty, diagnostic);
    }
}
