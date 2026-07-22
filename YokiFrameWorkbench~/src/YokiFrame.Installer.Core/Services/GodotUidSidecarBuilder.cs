using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 为单个 Godot 脚本资源保留合法既有 UID，或生成可修复的确定性 sidecar 内容。
/// </summary>
public sealed class GodotUidSidecarBuilder
{
    private readonly GodotUidGenerator mGenerator = new();

    /// <summary>
    /// 创建单个 UID sidecar 计划；本方法只读现有文件，不执行写入。
    /// </summary>
    /// <param name="relativePath">sidecar 相对于所属包根的路径。</param>
    /// <param name="resourcePath">对应脚本的 Godot res:// 路径。</param>
    /// <param name="existingSidecarPath">目标位置当前 sidecar 的完整路径。</param>
    /// <returns>保留合法原文或使用确定性值修复的 sidecar。</returns>
    public GodotUidSidecar Build(
        string relativePath,
        string resourcePath,
        string existingSidecarPath)
    {
        if (File.Exists(existingSidecarPath))
        {
            var existingContent = File.ReadAllText(existingSidecarPath);
            if (mGenerator.IsValid(existingContent))
            {
                return new GodotUidSidecar(relativePath, existingContent);
            }
        }

        return new GodotUidSidecar(relativePath, mGenerator.Generate(resourcePath) + "\n");
    }
}
