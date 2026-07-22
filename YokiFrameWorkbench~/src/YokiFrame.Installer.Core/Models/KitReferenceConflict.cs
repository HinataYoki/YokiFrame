namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述用户 C# 脚本对当前发布包未提供 Kit API 的一次真实代码引用。
/// </summary>
public sealed class KitReferenceConflict
{
    /// <summary>
    /// 创建 Kit 引用冲突。
    /// </summary>
    /// <param name="kitName">缺失 Kit 名称。</param>
    /// <param name="identifier">脚本中命中的旧 API 标识符。</param>
    /// <param name="projectRelativePath">目标项目内脚本相对路径。</param>
    /// <param name="lineNumber">一基行号。</param>
    public KitReferenceConflict(
        string kitName,
        string identifier,
        string projectRelativePath,
        int lineNumber)
    {
        KitName = kitName;
        Identifier = identifier;
        ProjectRelativePath = projectRelativePath;
        LineNumber = lineNumber;
    }

    /// <summary>获取缺失 Kit 名称。</summary>
    public string KitName { get; }

    /// <summary>获取脚本中命中的旧 API 标识符。</summary>
    public string Identifier { get; }

    /// <summary>获取目标项目内脚本相对路径。</summary>
    public string ProjectRelativePath { get; }

    /// <summary>获取一基行号。</summary>
    public int LineNumber { get; }

    /// <summary>
    /// 获取 CLI 与 UI 可直接展示的稳定冲突位置。
    /// </summary>
    public string DisplayPath => ProjectRelativePath + ":" + LineNumber + " [" + KitName + "]";
}
