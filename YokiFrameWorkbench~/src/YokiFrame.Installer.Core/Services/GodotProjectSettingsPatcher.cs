using System.Text;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 按 project.godot section/key 边界维护 Installer 独占的插件、autoload 与 YokiFrame 配置。
/// </summary>
public sealed class GodotProjectSettingsPatcher
{
    private const string PLUGIN_CONFIG_PATH = "res://addons/yokiframe/plugin.cfg";
    private const string PACKAGE_ROOT = "res://addons/yokiframe/package/YokiFrame";
    private const string AUTOLOAD_NAME = "YokiFrameGodotBootstrap";
    private const string AUTOLOAD_PATH = "*res://addons/yokiframe/YokiFrameGodotBootstrap.cs";

    /// <summary>
    /// 结构化更新 project.godot 的三个 owner 边界，并保持其它 section、键和值原样存在。
    /// </summary>
    /// <param name="projectSettings">完整 project.godot 文本。</param>
    /// <returns>使用 LF 换行且重复 patch 文本幂等的项目设置。</returns>
    public string Patch(string projectSettings)
    {
        return Patch(projectSettings, enablePlugin: true);
    }

    /// <summary>
    /// 按 project.godot owner 边界维护配置，并按选项登记或移除 YokiFrame editor plugin。
    /// </summary>
    /// <param name="projectSettings">完整 project.godot 文本。</param>
    /// <param name="enablePlugin">是否在 editor_plugins/enabled 中登记 YokiFrame。</param>
    /// <returns>使用 LF 换行且重复 patch 文本幂等的项目设置。</returns>
    public string Patch(string projectSettings, bool enablePlugin)
    {
        var sections = ParseSections(projectSettings);
        PatchEditorPlugins(sections, enablePlugin);
        PatchAutoload(sections);
        PatchOwnedSection(sections);
        return RenderSections(sections);
    }

    /// <summary>
    /// 更新 editor_plugins/enabled 的 PackedStringArray，保留其它插件顺序并拒绝重复 owner。
    /// </summary>
    /// <param name="sections">已解析 section 列表。</param>
    /// <param name="enablePlugin">是否登记 YokiFrame editor plugin；关闭时移除既有 owner。</param>
    private static void PatchEditorPlugins(List<SectionBlock> sections, bool enablePlugin)
    {
        var section = GetOptionalSingleSection(sections, "editor_plugins");
        if (section == null)
        {
            if (!enablePlugin)
            {
                return;
            }

            section = AddSection(sections, "editor_plugins");
        }

        var keyIndices = FindKeyIndices(section, "enabled");
        if (keyIndices.Count > 1)
        {
            throw new InvalidDataException("Godot editor_plugins/enabled appears " + keyIndices.Count + " times.");
        }

        List<string> pluginPaths = keyIndices.Count == 0
            ? new List<string>()
            : ParsePackedStringArray(ReadValue(section.Lines[keyIndices[0]], "editor_plugins", "enabled"));
        var ownerCount = pluginPaths.Count(static path => string.Equals(path, PLUGIN_CONFIG_PATH, StringComparison.Ordinal));
        if (ownerCount > 1)
        {
            throw new InvalidDataException(
                "Godot editor_plugins contains " + ownerCount + " entries for " + PLUGIN_CONFIG_PATH + ".");
        }

        if (enablePlugin && ownerCount == 0)
        {
            pluginPaths.Add(PLUGIN_CONFIG_PATH);
        }
        else if (!enablePlugin && ownerCount == 1)
        {
            pluginPaths.RemoveAll(static path => string.Equals(path, PLUGIN_CONFIG_PATH, StringComparison.Ordinal));
        }

        if (!enablePlugin && keyIndices.Count == 0)
        {
            return;
        }

        var value = "PackedStringArray(" + string.Join(", ", pluginPaths.Select(static path => "\"" + EscapeQuotedValue(path) + "\"")) + ")";
        SetSingleKey(section, "enabled", value, keyIndices);
    }

    /// <summary>
    /// 更新唯一 YokiFrame autoload 行，保留用户其它 autoload 的原始顺序。
    /// </summary>
    /// <param name="sections">已解析 section 列表。</param>
    private static void PatchAutoload(List<SectionBlock> sections)
    {
        var section = GetOrAddSingleSection(sections, "autoload");
        var keyIndices = FindKeyIndices(section, AUTOLOAD_NAME);
        if (keyIndices.Count > 1)
        {
            throw new InvalidDataException(
                "Godot autoload contains " + keyIndices.Count + " entries for " + AUTOLOAD_NAME + ".");
        }

        SetSingleKey(section, AUTOLOAD_NAME, "\"" + AUTOLOAD_PATH + "\"", keyIndices);
    }

    /// <summary>
    /// 替换 Installer 完整拥有的 [yokiframe] section，删除遗留 owner 键但不影响其它 section。
    /// </summary>
    /// <param name="sections">已解析 section 列表。</param>
    private static void PatchOwnedSection(List<SectionBlock> sections)
    {
        var matches = sections.Where(static section => string.Equals(section.Name, "yokiframe", StringComparison.Ordinal)).ToList();
        if (matches.Count > 1)
        {
            throw new InvalidDataException("Godot project contains " + matches.Count + " [yokiframe] owner sections.");
        }

        var section = matches.Count == 1 ? matches[0] : AddSection(sections, "yokiframe");
        section.Lines.Clear();
        section.Lines.Add("package_root=\"" + PACKAGE_ROOT + "\"");
    }

    /// <summary>
    /// 将 project.godot 拆为前导文本和具名 section，保留每个非 owner 原始行。
    /// </summary>
    /// <param name="content">完整项目设置。</param>
    /// <returns>按原顺序排列的 section blocks。</returns>
    private static List<SectionBlock> ParseSections(string content)
    {
        List<SectionBlock> sections = new() { new SectionBlock(null, string.Empty) };
        var current = sections[0];
        foreach (var rawLine in NormalizeLineEndings(content).Split('\n'))
        {
            var trimmed = rawLine.Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                var name = trimmed[1..^1];
                if (name.Length == 0)
                {
                    throw new InvalidDataException("Godot project contains an empty section name.");
                }

                current = new SectionBlock(name, rawLine);
                sections.Add(current);
                continue;
            }

            current.Lines.Add(rawLine);
        }

        return sections;
    }

    /// <summary>
    /// 获取唯一 section；缺失时在文件末尾创建，重复时拒绝不明确所有权。
    /// </summary>
    /// <param name="sections">section 列表。</param>
    /// <param name="name">目标 section 名称。</param>
    /// <returns>唯一 section。</returns>
    private static SectionBlock GetOrAddSingleSection(List<SectionBlock> sections, string name)
    {
        var matches = sections.Where(section => string.Equals(section.Name, name, StringComparison.Ordinal)).ToList();
        if (matches.Count > 1)
        {
            throw new InvalidDataException("Godot project contains " + matches.Count + " [" + name + "] sections.");
        }

        return matches.Count == 1 ? matches[0] : AddSection(sections, name);
    }

    /// <summary>
    /// 读取可选唯一 section；缺失时返回空，重复时拒绝不明确所有权。
    /// </summary>
    /// <param name="sections">section 列表。</param>
    /// <param name="name">目标 section 名称。</param>
    /// <returns>唯一 section，缺失时返回 null。</returns>
    private static SectionBlock? GetOptionalSingleSection(List<SectionBlock> sections, string name)
    {
        var matches = sections.Where(section => string.Equals(section.Name, name, StringComparison.Ordinal)).ToList();
        if (matches.Count > 1)
        {
            throw new InvalidDataException("Godot project contains " + matches.Count + " [" + name + "] sections.");
        }

        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>
    /// 在 section 列表末尾创建具名 block。
    /// </summary>
    /// <param name="sections">section 列表。</param>
    /// <param name="name">新 section 名称。</param>
    /// <returns>新 section。</returns>
    private static SectionBlock AddSection(List<SectionBlock> sections, string name)
    {
        SectionBlock section = new(name, "[" + name + "]");
        sections.Add(section);
        return section;
    }

    /// <summary>
    /// 查找 section 内指定键的全部有效配置行，忽略注释和相似文本。
    /// </summary>
    /// <param name="section">目标 section。</param>
    /// <param name="key">精确配置键。</param>
    /// <returns>匹配行索引。</returns>
    private static List<int> FindKeyIndices(SectionBlock section, string key)
    {
        List<int> indices = new();
        for (var index = 0; index < section.Lines.Count; index++)
        {
            var line = section.Lines[index].Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex > 0 && string.Equals(line[..separatorIndex].Trim(), key, StringComparison.Ordinal))
            {
                indices.Add(index);
            }
        }

        return indices;
    }

    /// <summary>
    /// 替换唯一键行或追加新键，并在追加前移除 section 尾部空白行以保持稳定格式。
    /// </summary>
    /// <param name="section">目标 section。</param>
    /// <param name="key">配置键。</param>
    /// <param name="value">右侧原始值。</param>
    /// <param name="indices">现有键行索引。</param>
    private static void SetSingleKey(SectionBlock section, string key, string value, IReadOnlyList<int> indices)
    {
        var line = key + "=" + value;
        if (indices.Count == 1)
        {
            section.Lines[indices[0]] = line;
            return;
        }

        TrimTrailingBlankLines(section.Lines);
        section.Lines.Add(line);
    }

    /// <summary>
    /// 从配置行读取等号右侧原始值，并在缺少分隔符时给出 owner 诊断。
    /// </summary>
    /// <param name="line">完整配置行。</param>
    /// <param name="section">section 名称。</param>
    /// <param name="key">配置键。</param>
    /// <returns>去除首尾空白后的右值。</returns>
    private static string ReadValue(string line, string section, string key)
    {
        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
        {
            throw new InvalidDataException("Godot " + section + "/" + key + " is invalid.");
        }

        return line[(separatorIndex + 1)..].Trim();
    }

    /// <summary>
    /// 解析 Godot PackedStringArray 字面量，拒绝字符串或其它无法安全维护的形态。
    /// </summary>
    /// <param name="value">配置右值。</param>
    /// <returns>按原顺序解析的字符串项。</returns>
    private static List<string> ParsePackedStringArray(string value)
    {
        const string prefix = "PackedStringArray(";
        if (!value.StartsWith(prefix, StringComparison.Ordinal) || !value.EndsWith(')'))
        {
            throw new InvalidDataException("Godot editor_plugins/enabled must use PackedStringArray(...).");
        }

        var content = value[prefix.Length..^1];
        List<string> values = new();
        var index = 0;
        while (TryReadPackedString(content, ref index, out var item))
        {
            values.Add(item);
        }

        return values;
    }

    /// <summary>
    /// 从 PackedStringArray 内容读取一个带引号字符串，并验证项间只使用逗号分隔。
    /// </summary>
    /// <param name="content">括号内部文本。</param>
    /// <param name="index">当前解析位置。</param>
    /// <param name="value">读取出的字符串。</param>
    /// <returns>还有字符串项时返回 true。</returns>
    private static bool TryReadPackedString(string content, ref int index, out string value)
    {
        SkipWhitespace(content, ref index);
        if (index >= content.Length)
        {
            value = string.Empty;
            return false;
        }

        if (content[index] != '"')
        {
            throw new InvalidDataException("Godot editor_plugins/enabled PackedStringArray contains an unquoted value.");
        }

        value = ReadQuotedValue(content, ref index);
        SkipWhitespace(content, ref index);
        if (index < content.Length && content[index++] != ',')
        {
            throw new InvalidDataException("Godot editor_plugins/enabled PackedStringArray has an invalid separator.");
        }

        return true;
    }

    /// <summary>
    /// 读取支持反斜杠转义的双引号字符串。
    /// </summary>
    /// <param name="content">待解析文本。</param>
    /// <param name="index">起始引号位置，返回时指向引号之后。</param>
    /// <returns>解码后的字符串。</returns>
    private static string ReadQuotedValue(string content, ref int index)
    {
        StringBuilder builder = new();
        index++;
        while (index < content.Length)
        {
            var current = content[index++];
            if (current == '"')
            {
                return builder.ToString();
            }

            if (current == '\\' && index < content.Length)
            {
                current = content[index++];
            }

            builder.Append(current);
        }

        throw new InvalidDataException("Godot editor_plugins/enabled contains an unterminated string.");
    }

    /// <summary>
    /// 跳过 PackedStringArray 中不具语义的空白字符。
    /// </summary>
    /// <param name="content">待解析文本。</param>
    /// <param name="index">当前位置。</param>
    private static void SkipWhitespace(string content, ref int index)
    {
        while (index < content.Length && char.IsWhiteSpace(content[index]))
        {
            index++;
        }
    }

    /// <summary>
    /// 转义重新写入 Godot 双引号字符串的反斜杠和引号。
    /// </summary>
    /// <param name="value">原始字符串。</param>
    /// <returns>Godot 字符串内容。</returns>
    private static string EscapeQuotedValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    /// <summary>
    /// 渲染 section blocks，保留已有行并为新增 section 使用稳定单空行分隔。
    /// </summary>
    /// <param name="sections">section blocks。</param>
    /// <returns>使用 LF 且末尾单换行的 project.godot。</returns>
    private static string RenderSections(IReadOnlyList<SectionBlock> sections)
    {
        List<string> lines = new();
        foreach (var section in sections)
        {
            if (section.Name != null)
            {
                if (lines.Count > 0 && lines[^1].Length != 0)
                {
                    lines.Add(string.Empty);
                }

                lines.Add(section.Header);
            }

            lines.AddRange(section.Lines);
        }

        TrimTrailingBlankLines(lines);
        return string.Join('\n', lines) + "\n";
    }

    /// <summary>
    /// 移除尾部空白行，避免重复 patch 持续累加 section 间距。
    /// </summary>
    /// <param name="lines">待整理行列表。</param>
    private static void TrimTrailingBlankLines(List<string> lines)
    {
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }
    }

    /// <summary>
    /// 统一输入换行符，保证跨平台 patch 输出稳定。
    /// </summary>
    /// <param name="content">原始文本。</param>
    /// <returns>仅使用 LF 的文本。</returns>
    private static string NormalizeLineEndings(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    /// <summary>
    /// 保存单个 project.godot section 的原始 header 与内容行。
    /// </summary>
    private sealed class SectionBlock
    {
        /// <summary>
        /// 创建 section block；name 为 null 时表示文件前导文本。
        /// </summary>
        /// <param name="name">section 名称。</param>
        /// <param name="header">原始 section header。</param>
        public SectionBlock(string? name, string header)
        {
            Name = name;
            Header = header;
        }

        /// <summary>
        /// 获取 section 名称；前导文本为 null。
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// 获取原始 section header。
        /// </summary>
        public string Header { get; }

        /// <summary>
        /// 获取 section 内容行。
        /// </summary>
        public List<string> Lines { get; } = new();
    }
}
