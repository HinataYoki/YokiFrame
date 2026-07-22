namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述一个由 Installer 规划并拥有的 Godot UID sidecar。
/// </summary>
public sealed class GodotUidSidecar
{
    /// <summary>
    /// 创建 UID sidecar 计划项。
    /// </summary>
    /// <param name="relativePath">sidecar 相对于所属包根的正斜杠路径。</param>
    /// <param name="content">待提交的完整 UID 文本。</param>
    public GodotUidSidecar(string relativePath, string content)
    {
        RelativePath = relativePath;
        Content = content;
    }

    /// <summary>获取 sidecar 的包相对路径。</summary>
    public string RelativePath { get; }

    /// <summary>获取待提交的完整 UID 文本。</summary>
    public string Content { get; }
}
