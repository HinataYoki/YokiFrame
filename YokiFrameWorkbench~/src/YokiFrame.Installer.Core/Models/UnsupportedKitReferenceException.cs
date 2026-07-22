namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 表示目标项目仍引用当前发布包未提供的旧 Kit，因此安装在零写入计划阶段停止。
/// </summary>
public sealed class UnsupportedKitReferenceException : InvalidOperationException
{
    /// <summary>
    /// 创建未迁移 Kit 引用异常。
    /// </summary>
    /// <param name="conflicts">稳定排序的脚本引用冲突。</param>
    public UnsupportedKitReferenceException(IReadOnlyList<KitReferenceConflict> conflicts)
        : base(CreateMessage(conflicts))
    {
        Conflicts = conflicts.ToArray();
        ConflictPaths = Conflicts.Select(static conflict => conflict.DisplayPath).ToArray();
    }

    /// <summary>获取稳定排序的脚本引用冲突。</summary>
    public IReadOnlyList<KitReferenceConflict> Conflicts { get; }

    /// <summary>获取 CLI 与 UI 可直接展示的冲突位置。</summary>
    public IReadOnlyList<string> ConflictPaths { get; }

    /// <summary>
    /// 创建包含文件、行号、Kit 和旧标识符的用户可读说明。
    /// </summary>
    /// <param name="conflicts">脚本引用冲突。</param>
    /// <returns>安装拒绝说明。</returns>
    private static string CreateMessage(IReadOnlyList<KitReferenceConflict> conflicts)
    {
        var locations = conflicts.Select(static conflict =>
            conflict.DisplayPath + " via " + conflict.Identifier);
        return "Godot project scripts reference YokiFrame Kits that are not available in this release: "
            + string.Join(", ", locations)
            + ". Migrate or remove these references before installing the new package.";
    }
}
