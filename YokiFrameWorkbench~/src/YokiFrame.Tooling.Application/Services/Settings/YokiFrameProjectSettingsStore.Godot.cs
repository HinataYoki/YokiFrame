using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YokiFrame.Tooling.Application.Services.Settings;

public sealed partial class YokiFrameProjectSettingsStore
{
    internal const string GODOT_RUNTIME_SECTION = "[yokiframe/runtime]";
    internal const string GODOT_EDITOR_SECTION = "[yokiframe/editor]";

    /// <summary>读取 Godot project.godot 的指定 YokiFrame section。</summary>
    internal static YokiFrameProjectSettingsBackendDocument LoadGodotBackendDocument(
        YokiFrameProjectSettingsTarget target,
        string path,
        string section)
    {
        if (!File.Exists(path))
        {
            return new YokiFrameProjectSettingsBackendDocument(
                target,
                path,
                false,
                string.Empty,
                new List<YokiFrameProjectSetting>());
        }

        byte[] bytes = ReadBoundedFile(path);
        string text = Encoding.UTF8.GetString(bytes);
        return new YokiFrameProjectSettingsBackendDocument(
            target,
            path,
            true,
            text,
            ParseGodotSettings(text, section),
            ComputeFingerprint(bytes));
    }

    /// <summary>从指定 section 中解析 owner/key 标量，保留最后条目生效顺序。</summary>
    private static List<YokiFrameProjectSetting> ParseGodotSettings(string content, string section)
    {
        string[] lines = NormalizeGodotLines(content);
        int header = FindGodotSection(lines, section);
        List<YokiFrameProjectSetting> settings = new();
        if (header < 0) return settings;
        int end = FindGodotSectionEnd(lines, header);
        for (int index = header + 1; index < end; index++)
        {
            if (TryParseGodotSetting(lines[index], out YokiFrameProjectSetting? setting))
            {
                settings.Add(setting!);
            }
        }

        return settings;
    }

    /// <summary>按 owner patch 更新指定 section，同时保持其它 Godot section 和原始行。</summary>
    internal static string SerializeGodotBackendDocument(
        string content,
        string section,
        IReadOnlyList<YokiFrameProjectSettingsPatch> patches)
    {
        List<string> lines = NormalizeGodotLines(content).ToList();
        EnsureGodotSection(lines, section);
        foreach (YokiFrameProjectSettingsPatch patch in patches)
        {
            int header = FindGodotSection(lines, section);
            int end = FindGodotSectionEnd(lines, header);
            // 只删除本 patch 拥有的键。其它 Kit 的草稿必须留在原位置，避免无改动保存重排 project.godot。
            for (var index = end - 1; index > header; index--)
            {
                if (TryReadGodotPath(lines[index], out string owner, out string key)
                    && patch.Owns(owner, key)
                    && !patch.Values.Any(value => string.Equals(value.Key, key, StringComparison.Ordinal)))
                {
                    lines.RemoveAt(index);
                }
            }

            end = FindGodotSectionEnd(lines, header);
            foreach (YokiFrameProjectSettingValue value in patch.Values)
            {
                string serialized = patch.Owner + "/" + value.Key + "="
                    + JsonSerializer.Serialize(value.Value ?? string.Empty, GodotSettingsJsonContext.Default.String);
                int existing = FindOwnedGodotLine(lines, header, end, patch.Owner, value.Key);
                // Godot 保存后会把引号改成 \u0022。语义相同就保留原行，避免开关 Workbench 改写 project.godot。
                if (existing >= 0 && GodotSettingValuesEqual(lines[existing], serialized))
                {
                    continue;
                }

                if (existing >= 0) lines.RemoveAt(existing);
                end = FindGodotSectionEnd(lines, header);
                lines.Insert(end, serialized);
            }
        }

        return string.Join('\n', lines).TrimEnd() + "\n";
    }

    /// <summary>解析一个 Godot Runtime 标量；注释、空行和无 owner 路径的行由调用方保留但不投影。</summary>
    private static bool TryParseGodotSetting(
        string line,
        out YokiFrameProjectSetting? setting)
    {
        setting = null;
        if (!TryReadGodotPath(line, out string owner, out string key)) return false;
        int separator = line.IndexOf('=');
        string rawValue = line[(separator + 1)..].Trim();
        string value = ParseGodotValue(rawValue);
        try
        {
            ValidateIdentifier(owner, "owner");
            ValidateIdentifier(key, "key");
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Godot YokiFrame setting has an unsafe identifier.", exception);
        }

        setting = new YokiFrameProjectSetting(owner, key, value);
        return true;
    }

    /// <summary>在当前 section 中查找指定 owner/key 的原始行。</summary>
    private static int FindOwnedGodotLine(
        IReadOnlyList<string> lines,
        int header,
        int end,
        string owner,
        string key)
    {
        for (int index = header + 1; index < end; index++)
        {
            if (TryReadGodotPath(lines[index], out string currentOwner, out string currentKey)
                && string.Equals(currentOwner, owner, StringComparison.Ordinal)
                && string.Equals(currentKey, key, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>比较两条 Godot 设置的解析值。Godot 的 \u0022 与 Workbench 的直引号视为相同。</summary>
    private static bool GodotSettingValuesEqual(string currentLine, string serializedLine)
    {
        if (string.Equals(currentLine, serializedLine, StringComparison.Ordinal)) return true;
        return TryReadGodotScalar(currentLine, out string current)
            && TryReadGodotScalar(serializedLine, out string serialized)
            && string.Equals(current, serialized, StringComparison.Ordinal);
    }

    /// <summary>按 owner/key 的第一个斜杠切分后读取右值，避免 JSON 内容中的等号干扰。</summary>
    private static bool TryReadGodotScalar(string line, out string value)
    {
        value = string.Empty;
        if (!TryReadGodotPath(line, out _, out _)) return false;
        string trimmed = line.Trim();
        int separator = trimmed.IndexOf('=');
        if (separator < 0) return false;
        value = ParseGodotValue(trimmed[(separator + 1)..].Trim());
        return true;
    }

    /// <summary>读取 Godot `owner/key=value` 左值，不解释普通项目配置。</summary>
    private static bool TryReadGodotPath(string line, out string owner, out string key)
    {
        owner = string.Empty;
        key = string.Empty;
        string trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed[0] is ';' or '#') return false;
        int separator = trimmed.IndexOf('=');
        if (separator <= 0) return false;
        string path = trimmed[..separator].Trim();
        int slash = path.IndexOf('/');
        if (slash <= 0 || slash == path.Length - 1) return false;
        owner = path[..slash];
        key = path[(slash + 1)..];
        return true;
    }

    /// <summary>解析 Godot 标量；字符串使用 JSON 兼容转义，其它标量保留文本表示。</summary>
    private static string ParseGodotValue(string rawValue)
    {
        if (rawValue.Length >= 2 && rawValue[0] == '"' && rawValue[^1] == '"')
        {
            try
            {
                return JsonSerializer.Deserialize(rawValue, GodotSettingsJsonContext.Default.String) ?? string.Empty;
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Godot YokiFrame string setting is invalid.", exception);
            }
        }

        return rawValue;
    }

    /// <summary>确保列表包含唯一目标 section；缺失时在文件末尾追加。</summary>
    private static void EnsureGodotSection(List<string> lines, string section)
    {
        if (FindGodotSection(lines, section) >= 0) return;
        if (lines.Count > 0 && lines[^1].Length != 0) lines.Add(string.Empty);
        lines.Add(section);
    }

    /// <summary>查找指定 section header。</summary>
    private static int FindGodotSection(IReadOnlyList<string> lines, string section)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (string.Equals(lines[index].Trim(), section, StringComparison.Ordinal)) return index;
        }

        return -1;
    }

    /// <summary>读取指定 section 中的一个字符串值；section 或键缺失时返回 false。</summary>
    internal static bool TryReadGodotValue(
        string content,
        string section,
        string owner,
        string key,
        out string value)
    {
        value = string.Empty;
        foreach (YokiFrameProjectSetting setting in ParseGodotSettings(content, section))
        {
            if (string.Equals(setting.Owner, owner, StringComparison.Ordinal)
                && string.Equals(setting.Key, key, StringComparison.Ordinal))
            {
                value = setting.Value;
                return true;
            }
        }

        return false;
    }

    /// <summary>查找当前 section 后的下一个 section 或文件末尾。</summary>
    private static int FindGodotSectionEnd(IReadOnlyList<string> lines, int header)
    {
        for (int index = header + 1; index < lines.Count; index++)
        {
            string line = lines[index].Trim();
            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal)) return index;
        }

        return lines.Count;
    }

    /// <summary>把平台换行统一为 LF，并避免 Split 产生无意义的尾部空行。</summary>
    private static string[] NormalizeGodotLines(string content)
    {
        string normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return normalized.TrimEnd('\n').Length == 0 ? Array.Empty<string>() : normalized.TrimEnd('\n').Split('\n');
    }
}

/// <summary>为 Godot Runtime 字符串设置提供 Native AOT 可用的 JSON 元数据。</summary>
[JsonSourceGenerationOptions]
[JsonSerializable(typeof(string))]
internal sealed partial class GodotSettingsJsonContext : JsonSerializerContext
{
}
