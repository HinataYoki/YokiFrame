namespace YokiFrame.Tooling.Application.Services.Settings;

/// <summary>
/// 判断当前项目根属于 Unity 还是 Godot，供配置后端选择宿主布局。
/// 只看项目根已有标记，不创建目录，也不把 Godot 项目伪装成 Unity 目录结构。
/// </summary>
internal static class YokiFrameProjectHostLayout
{
    /// <summary>
    /// 判断项目根是否是 Godot 项目。存在 project.godot 时优先认定为 Godot，即使旁边误留了 Unity 标记。
    /// </summary>
    /// <param name="projectRoot">已规范化的项目根；空值按非 Godot 处理。</param>
    /// <returns>项目根包含 project.godot 时返回 true。</returns>
    internal static bool IsGodotProject(string? projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot)) return false;
        return File.Exists(Path.Combine(projectRoot, "project.godot"));
    }
}
