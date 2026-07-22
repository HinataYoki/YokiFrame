using YokiFrame.Installer.Core.Models;
using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 调用 Installer.Core 版本门控并返回 Application 自有目标只读模型。
/// </summary>
public sealed class InstallerTargetDetectionService
{
    private readonly TargetProjectDetector mDetector = new();

    /// <summary>
    /// 检测 Unity、Godot 或未知目标目录。
    /// </summary>
    /// <param name="projectRoot">待检测项目根。</param>
    /// <returns>不泄漏 Core DTO 的目标只读模型。</returns>
    public InstallerTargetInfo Detect(string projectRoot)
    {
        var target = mDetector.Detect(projectRoot);
        return new InstallerTargetInfo(
            MapKind(target.Kind),
            target.ProjectRoot,
            target.PackageRoot,
            target.EvidencePaths);
    }

    /// <summary>
    /// 把 Core 项目类型映射为 Application 目标类型。
    /// </summary>
    /// <param name="kind">Core 项目类型。</param>
    /// <returns>Application 目标类型。</returns>
    private static InstallerTargetKind MapKind(InstallerProjectKind kind)
    {
        return kind switch
        {
            InstallerProjectKind.Unknown => InstallerTargetKind.Unknown,
            InstallerProjectKind.Unity => InstallerTargetKind.Unity,
            InstallerProjectKind.Godot => InstallerTargetKind.Godot,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
