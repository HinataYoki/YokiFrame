using System.Text;
using System.Text.Json;
using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 为测试 Client 统一复刻 Shared Memory 游标顺序，避免各 fake 对新帧定义产生漂移。
/// </summary>
internal static class TelemetryFrameCursorTestHelper
{
    /// <summary>
    /// 按 session/generation 内单调 sequence 过滤完整读取结果；失败结果保留给应用层处理。
    /// </summary>
    /// <param name="frame">fake 完整读取产生的帧结果。</param>
    /// <param name="afterSequence">调用方最后接受的帧序号。</param>
    /// <returns>候选帧晚于游标或读取失败时返回原结果，否则返回空。</returns>
    internal static SharedMemoryTelemetryFrameReadResult? Filter(
        SharedMemoryTelemetryFrameReadResult frame,
        long afterSequence)
    {
        var header = frame.Header;
        if (!frame.IsAccepted || header == null)
        {
            return frame;
        }

        return header.Sequence > afterSequence ? frame : null;
    }
}

/// <summary>
/// 把测试里的文件副作用收成字节、JSON 字段和 Godot 键值，避免用原文子串证明行为。
/// </summary>
internal static class ToolingFileObservation
{
    /// <summary>按 UTF-8 字节解析 JSON 文件。</summary>
    /// <param name="path">待观察文件。</param>
    /// <returns>调用方负责释放的文档。</returns>
    internal static JsonDocument OpenJson(string path)
    {
        return JsonDocument.Parse(File.ReadAllBytes(path));
    }

    /// <summary>按 UTF-8 读取生成文本；不使用会按当前编码猜测的文本 API。</summary>
    /// <param name="path">待观察文件。</param>
    /// <returns>无 BOM 的 UTF-8 文本。</returns>
    internal static string ReadUtf8(string path)
    {
        return Encoding.UTF8.GetString(File.ReadAllBytes(path));
    }

    /// <summary>断言文件字节与预期 UTF-8 文本完全一致。</summary>
    /// <param name="path">待观察文件。</param>
    /// <param name="expected">预期文本，按无 BOM UTF-8 比较。</param>
    internal static void AssertUtf8(string path, string expected)
    {
        Assert.Equal(Encoding.UTF8.GetBytes(expected), File.ReadAllBytes(path));
    }

    /// <summary>断言生成文本里存在两侧都不是标识符字符的声明片段。</summary>
    /// <param name="source">生成结果。</param>
    /// <param name="fragment">必须出现的声明。</param>
    internal static void AssertDeclares(string source, string fragment)
    {
        if (FindBounded(source, fragment) >= 0)
        {
            return;
        }

        Assert.Fail("生成结果缺少声明: " + fragment);
    }

    /// <summary>断言生成文本不包含指定字符序列，负向契约不放宽成 token 边界。</summary>
    /// <param name="source">生成结果。</param>
    /// <param name="fragment">禁止出现的字符序列。</param>
    internal static void AssertOmits(string source, string fragment)
    {
        Assert.True(source.IndexOf(fragment, StringComparison.Ordinal) < 0, "生成结果仍包含: " + fragment);
    }

    /// <summary>断言 JSON 的属性名和字符串值都不包含指定字符序列。</summary>
    /// <param name="element">已解析文档。</param>
    /// <param name="fragment">禁止出现的键或文本。</param>
    internal static void AssertJsonOmits(JsonElement element, string fragment)
    {
        Assert.False(JsonContains(element, fragment), "JSON 仍包含: " + fragment);
    }

    /// <summary>读取 Godot 配置；section 名保留方括号，文件头键归入空 section。</summary>
    /// <param name="path">project.godot 路径。</param>
    /// <returns>section 到键值的映射，键值只按第一个等号拆分。</returns>
    internal static Dictionary<string, Dictionary<string, string>> ReadGodot(string path)
    {
        string text = ReadUtf8(path).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        Dictionary<string, Dictionary<string, string>> sections = new(StringComparer.Ordinal);
        Dictionary<string, string>? current = null;
        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == ';')
            {
                continue;
            }

            if (line[0] == '[' && line[^1] == ']')
            {
                current = new Dictionary<string, string>(StringComparer.Ordinal);
                sections[line] = current;
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            if (current == null)
            {
                if (!sections.TryGetValue(string.Empty, out Dictionary<string, string>? header))
                {
                    header = new Dictionary<string, string>(StringComparer.Ordinal);
                    sections[string.Empty] = header;
                }

                current = header;
            }

            current[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        return sections;
    }

    /// <summary>在任意 section 中查找 Godot 键。</summary>
    /// <param name="sections">已解析的 project.godot。</param>
    /// <param name="key">owner/key。</param>
    /// <param name="value">找到的原始值。</param>
    /// <returns>至少有一个 section 含该键时返回 true。</returns>
    internal static bool TryGetGodotValue(
        IReadOnlyDictionary<string, Dictionary<string, string>> sections,
        string key,
        out string value)
    {
        foreach (Dictionary<string, string> section in sections.Values)
        {
            if (section.TryGetValue(key, out string? found))
            {
                value = found;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    /// <summary>断言 Godot 键和值都不包含指定字符序列。</summary>
    /// <param name="sections">已解析的 project.godot。</param>
    /// <param name="fragment">禁止出现的路径或键片段。</param>
    internal static void AssertGodotOmits(
        IReadOnlyDictionary<string, Dictionary<string, string>> sections,
        string fragment)
    {
        foreach (KeyValuePair<string, Dictionary<string, string>> section in sections)
        {
            Assert.True(section.Key.IndexOf(fragment, StringComparison.Ordinal) < 0);
            foreach (KeyValuePair<string, string> setting in section.Value)
            {
                Assert.True(setting.Key.IndexOf(fragment, StringComparison.Ordinal) < 0);
                Assert.True(setting.Value.IndexOf(fragment, StringComparison.Ordinal) < 0);
            }
        }
    }

    /// <summary>查找两侧都已断开标识符的片段。</summary>
    /// <param name="source">被搜索文本。</param>
    /// <param name="fragment">目标片段。</param>
    /// <returns>命中下标；不存在时返回 -1。</returns>
    private static int FindBounded(string source, string fragment)
    {
        int index = 0;
        while (index <= source.Length - fragment.Length)
        {
            int found = source.IndexOf(fragment, index, StringComparison.Ordinal);
            if (found < 0)
            {
                return -1;
            }

            int end = found + fragment.Length;
            bool leftBound = found == 0 || !IsIdentifierChar(source[found - 1]);
            bool rightBound = end >= source.Length || !IsIdentifierChar(source[end]);
            if (leftBound && rightBound)
            {
                return found;
            }

            index = found + 1;
        }

        return -1;
    }

    /// <summary>递归检查属性名和字符串值。</summary>
    /// <param name="element">当前 JSON 节点。</param>
    /// <param name="fragment">目标字符序列。</param>
    /// <returns>语义内容包含该序列时返回 true。</returns>
    private static bool JsonContains(JsonElement element, string fragment)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.Name.IndexOf(fragment, StringComparison.Ordinal) >= 0
                        || JsonContains(property.Value, fragment))
                    {
                        return true;
                    }
                }

                return false;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (JsonContains(item, fragment))
                    {
                        return true;
                    }
                }

                return false;
            case JsonValueKind.String:
                return (element.GetString() ?? string.Empty).IndexOf(fragment, StringComparison.Ordinal) >= 0;
            default:
                return false;
        }
    }

    /// <summary>判断字符是否会把相邻片段粘成同一个标识符。</summary>
    /// <param name="value">边界字符。</param>
    /// <returns>字母、数字或下划线时返回 true。</returns>
    private static bool IsIdentifierChar(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_';
    }
}
