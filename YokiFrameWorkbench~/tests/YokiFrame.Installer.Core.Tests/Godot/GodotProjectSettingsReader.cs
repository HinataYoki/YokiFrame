namespace YokiFrame.Installer.Core.Tests.Godot;

/// <summary>
/// 按 section 行边界读取 project.godot，供安装结果断言字段而不是全文子串。
/// </summary>
internal static class GodotProjectSettingsReader
{
    /// <summary>
    /// 读取指定 section 中唯一键的原始右侧值。
    /// </summary>
    /// <param name="content">完整 project.godot 文本。</param>
    /// <param name="sectionName">目标 section 名称。</param>
    /// <param name="key">目标配置键。</param>
    /// <returns>等号右侧的原始值。</returns>
    internal static string ReadSingleValue(string content, string sectionName, string key)
    {
        return Assert.Single(ReadValues(content, sectionName, key));
    }

    /// <summary>
    /// 返回指定 section 的非空内容行，用于确认 owner 项没有额外残留。
    /// </summary>
    /// <param name="content">完整 project.godot 文本。</param>
    /// <param name="sectionName">目标 section 名称。</param>
    /// <returns>去掉空行后的 section 内容。</returns>
    internal static IReadOnlyList<string> ReadNonEmptyLines(string content, string sectionName)
    {
        return Assert.Single(ReadSections(content, sectionName))
            .Where(static line => line.Length > 0)
            .ToArray();
    }

    /// <summary>
    /// 按 section 行边界收集指定键的全部值。
    /// </summary>
    /// <param name="content">完整 project.godot 文本。</param>
    /// <param name="sectionName">目标 section 名称。</param>
    /// <param name="key">目标配置键。</param>
    /// <returns>按原顺序收集的配置值。</returns>
    private static IReadOnlyList<string> ReadValues(string content, string sectionName, string key)
    {
        List<string> values = new();
        foreach (var line in Assert.Single(ReadSections(content, sectionName)))
        {
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0 || !string.Equals(line[..separatorIndex].Trim(), key, StringComparison.Ordinal))
            {
                continue;
            }

            values.Add(line[(separatorIndex + 1)..].Trim());
        }

        return values;
    }

    /// <summary>
    /// 将 project.godot 拆成同名 section 的行列表。
    /// </summary>
    /// <param name="content">完整 project.godot 文本。</param>
    /// <param name="sectionName">目标 section 名称。</param>
    /// <returns>同名 section 的全部内容行。</returns>
    private static IReadOnlyList<IReadOnlyList<string>> ReadSections(string content, string sectionName)
    {
        List<IReadOnlyList<string>> sections = new();
        List<string>? currentLines = null;
        foreach (var rawLine in content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                if (currentLines != null)
                {
                    sections.Add(currentLines);
                }

                currentLines = string.Equals(line[1..^1], sectionName, StringComparison.Ordinal) ? new List<string>() : null;
                continue;
            }

            currentLines?.Add(line);
        }

        if (currentLines != null)
        {
            sections.Add(currentLines);
        }

        return sections;
    }
}
