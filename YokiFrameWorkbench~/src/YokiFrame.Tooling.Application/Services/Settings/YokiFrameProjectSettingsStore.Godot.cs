using System.Text;
using System.Text.Json;

namespace YokiFrame.Tooling.Application.Services.Settings;

public sealed partial class YokiFrameProjectSettingsStore
{
    private const string GODOT_RUNTIME_SECTION = "[yokiframe/runtime]";

    /// <summary>读取 Godot project.godot 的 YokiFrame Runtime section。</summary>
    internal static YokiFrameProjectSettingsBackendDocument LoadGodotBackendDocument(
        YokiFrameProjectSettingsTarget target,
        string path)
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
            ParseGodotSettings(text),
            ComputeFingerprint(bytes));
    }

    /// <summary>从 `[yokiframe/runtime]` 中解析 owner/key 标量，保留最后条目生效顺序。</summary>
    private static List<YokiFrameProjectSetting> ParseGodotSettings(string content)
    {
        string[] lines = NormalizeGodotLines(content);
        int header = FindGodotSection(lines);
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

    /// <summary>按 owner patch 更新 YokiFrame section，同时保持其它 Godot section 和原始行。</summary>
    internal static string SerializeGodotBackendDocument(
        string content,
        IReadOnlyList<YokiFrameProjectSettingsPatch> patches)
    {
        List<string> lines = NormalizeGodotLines(content).ToList();
        EnsureGodotSection(lines);
        foreach (YokiFrameProjectSettingsPatch patch in patches)
        {
            int header = FindGodotSection(lines);
            int end = FindGodotSectionEnd(lines, header);
            for (var index = end - 1; index > header; index--)
            {
                if (TryReadGodotPath(lines[index], out string owner, out string key)
                    && patch.Owns(owner, key))
                {
                    lines.RemoveAt(index);
                }
            }

            end = FindGodotSectionEnd(lines, header);
            foreach (YokiFrameProjectSettingValue value in patch.Values)
            {
                lines.Insert(end++, patch.Owner + "/" + value.Key + "=" + JsonSerializer.Serialize(value.Value ?? string.Empty));
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
                return JsonSerializer.Deserialize<string>(rawValue) ?? string.Empty;
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Godot YokiFrame string setting is invalid.", exception);
            }
        }

        return rawValue;
    }

    /// <summary>确保列表包含唯一目标 section；缺失时在文件末尾追加。</summary>
    private static void EnsureGodotSection(List<string> lines)
    {
        if (FindGodotSection(lines) >= 0) return;
        if (lines.Count > 0 && lines[^1].Length != 0) lines.Add(string.Empty);
        lines.Add(GODOT_RUNTIME_SECTION);
    }

    /// <summary>查找 YokiFrame Runtime section header。</summary>
    private static int FindGodotSection(IReadOnlyList<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (string.Equals(lines[index].Trim(), GODOT_RUNTIME_SECTION, StringComparison.Ordinal)) return index;
        }

        return -1;
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
