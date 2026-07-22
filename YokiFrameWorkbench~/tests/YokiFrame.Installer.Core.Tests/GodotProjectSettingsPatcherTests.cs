using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Installer.Core.Tests;

/// <summary>
/// 验证 Installer 对 project.godot 中 YokiFrame 插件、autoload 与配置 section 的唯一 owner patch 行为。
/// </summary>
public sealed class GodotProjectSettingsPatcherTests
{
    private const string PLUGIN_CONFIG_PATH = "res://addons/yokiframe/plugin.cfg";
    private const string PACKAGE_ROOT = "res://addons/yokiframe/package/YokiFrame";
    private const string AUTOLOAD_NAME = "YokiFrameGodotBootstrap";
    private const string AUTOLOAD_PATH = "*res://addons/yokiframe/YokiFrameGodotBootstrap.cs";

    /// <summary>
    /// 验证缺少 owner 项时会补齐插件、autoload 和 [yokiframe]，同时逐行保留其它 section、注释和相似文本。
    /// </summary>
    [Fact]
    public void PatchAddsOwnedEntriesAndPreservesUnownedProjectSettings()
    {
        const string source = """
            ; user-owned header
            config_version=5

            [application]
            config/name="Fixture"
            ; owner-like text must remain untouched: res://addons/yokiframe/plugin.cfg
            run/metadata="autoload/YokiFrameGodotBootstrap=[yokiframe]"

            [editor_plugins]
            enabled=PackedStringArray("res://addons/other/plugin.cfg")

            [autoload]
            UserService="*res://autoloads/user_service.cs"

            [rendering]
            renderer/rendering_method="gl_compatibility"
            """;

        var patched = new GodotProjectSettingsPatcher().Patch(source);

        Assert.Contains("; user-owned header", patched, StringComparison.Ordinal);
        Assert.Contains(
            "; owner-like text must remain untouched: res://addons/yokiframe/plugin.cfg",
            patched,
            StringComparison.Ordinal);
        Assert.Equal("\"autoload/YokiFrameGodotBootstrap=[yokiframe]\"", ReadSingleValue(patched, "application", "run/metadata"));
        Assert.Equal(
            "PackedStringArray(\"res://addons/other/plugin.cfg\", \"" + PLUGIN_CONFIG_PATH + "\")",
            ReadSingleValue(patched, "editor_plugins", "enabled"));
        Assert.Equal("\"*res://autoloads/user_service.cs\"", ReadSingleValue(patched, "autoload", "UserService"));
        Assert.Equal("\"" + AUTOLOAD_PATH + "\"", ReadSingleValue(patched, "autoload", AUTOLOAD_NAME));
        Assert.Equal("\"" + PACKAGE_ROOT + "\"", ReadSingleValue(patched, "yokiframe", "package_root"));
        Assert.Equal("\"gl_compatibility\"", ReadSingleValue(patched, "rendering", "renderer/rendering_method"));
    }

    /// <summary>
    /// 验证已有单份 owner 项只会更新为当前约定，不会改写同 section 中其它插件或 autoload。
    /// </summary>
    [Fact]
    public void PatchReplacesOnlyExistingOwnedEntries()
    {
        const string source = """
            config_version=5

            [editor_plugins]
            enabled=PackedStringArray("res://addons/first/plugin.cfg", "res://addons/yokiframe/plugin.cfg", "res://addons/last/plugin.cfg")

            [autoload]
            First="*res://autoloads/first.cs"
            YokiFrameGodotBootstrap="*res://legacy/GodotBootstrap.cs"
            Last="*res://autoloads/last.cs"

            [yokiframe]
            package_root="res://legacy/YokiFrame"
            legacy_owner_value=true
            """;

        var patched = new GodotProjectSettingsPatcher().Patch(source);

        Assert.Equal(
            "PackedStringArray(\"res://addons/first/plugin.cfg\", \"" + PLUGIN_CONFIG_PATH + "\", \"res://addons/last/plugin.cfg\")",
            ReadSingleValue(patched, "editor_plugins", "enabled"));
        Assert.Equal("\"*res://autoloads/first.cs\"", ReadSingleValue(patched, "autoload", "First"));
        Assert.Equal("\"" + AUTOLOAD_PATH + "\"", ReadSingleValue(patched, "autoload", AUTOLOAD_NAME));
        Assert.Equal("\"*res://autoloads/last.cs\"", ReadSingleValue(patched, "autoload", "Last"));
        Assert.Equal("\"" + PACKAGE_ROOT + "\"", ReadSingleValue(patched, "yokiframe", "package_root"));
        Assert.DoesNotContain("legacy_owner_value", patched, StringComparison.Ordinal);
        Assert.DoesNotContain("res://legacy", patched, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证禁用插件登记时只移除 YokiFrame plugin owner，并继续维护 autoload 与 package_root。
    /// </summary>
    [Fact]
    public void PatchWithPluginDisabledMaintainsRuntimeOwnersWithoutRegistration()
    {
        const string source = """
            config_version=5

            [editor_plugins]
            enabled=PackedStringArray("res://addons/first/plugin.cfg", "res://addons/yokiframe/plugin.cfg")

            [autoload]
            UserService="*res://autoloads/user_service.cs"
            """;

        var patched = new GodotProjectSettingsPatcher().Patch(source, enablePlugin: false);

        Assert.Equal(
            "PackedStringArray(\"res://addons/first/plugin.cfg\")",
            ReadSingleValue(patched, "editor_plugins", "enabled"));
        Assert.Equal("\"" + AUTOLOAD_PATH + "\"", ReadSingleValue(patched, "autoload", AUTOLOAD_NAME));
        Assert.Equal("\"" + PACKAGE_ROOT + "\"", ReadSingleValue(patched, "yokiframe", "package_root"));
        Assert.DoesNotContain(PLUGIN_CONFIG_PATH, patched, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证相同 project.godot 重复 patch 不会累加 owner 项或产生无意义文本变化。
    /// </summary>
    [Fact]
    public void PatchIsTextuallyIdempotent()
    {
        const string source = """
            config_version=5

            [application]
            config/name="Fixture"
            """;
        var patcher = new GodotProjectSettingsPatcher();

        var firstPatch = patcher.Patch(source);
        var secondPatch = patcher.Patch(firstPatch);

        Assert.Equal(firstPatch, secondPatch);
        Assert.Equal(1, CountValueOccurrences(firstPatch, PLUGIN_CONFIG_PATH));
        Assert.Single(ReadValues(firstPatch, "autoload", AUTOLOAD_NAME));
        Assert.Single(ReadSections(firstPatch, "yokiframe"));
    }

    /// <summary>
    /// 验证重复 YokiFrame 插件 owner 会被诊断拒绝，而不是静默去重后掩盖配置漂移。
    /// </summary>
    [Fact]
    public void PatchRejectsDuplicatePluginOwnerWithDiagnostic()
    {
        const string source = """
            [editor_plugins]
            enabled=PackedStringArray("res://addons/yokiframe/plugin.cfg", "res://addons/other/plugin.cfg", "res://addons/yokiframe/plugin.cfg")
            """;

        var error = Assert.Throws<InvalidDataException>(() => new GodotProjectSettingsPatcher().Patch(source));

        Assert.Contains("editor_plugins", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(PLUGIN_CONFIG_PATH, error.Message, StringComparison.Ordinal);
        Assert.Contains("2", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证重复 autoload owner 会被诊断拒绝，避免 Installer 猜测应替换哪一行。
    /// </summary>
    [Fact]
    public void PatchRejectsDuplicateAutoloadOwnerWithDiagnostic()
    {
        const string source = """
            [autoload]
            YokiFrameGodotBootstrap="*res://legacy/first.cs"
            UserService="*res://autoloads/user_service.cs"
            YokiFrameGodotBootstrap="*res://legacy/second.cs"
            """;

        var error = Assert.Throws<InvalidDataException>(() => new GodotProjectSettingsPatcher().Patch(source));

        Assert.Contains("autoload", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(AUTOLOAD_NAME, error.Message, StringComparison.Ordinal);
        Assert.Contains("2", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证重复 [yokiframe] owner section 会被诊断拒绝，避免跨 section 合并不明确的所有权。
    /// </summary>
    [Fact]
    public void PatchRejectsDuplicateOwnedSectionsWithDiagnostic()
    {
        const string source = """
            [yokiframe]
            package_root="res://legacy/first"

            [application]
            config/name="Fixture"

            [yokiframe]
            package_root="res://legacy/second"
            """;

        var error = Assert.Throws<InvalidDataException>(() => new GodotProjectSettingsPatcher().Patch(source));

        Assert.Contains("yokiframe", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证无法安全解析的 editor_plugins owner 结构会被拒绝，而不是用全局字符串替换破坏用户配置。
    /// </summary>
    [Fact]
    public void PatchRejectsUnsupportedPluginListShapeWithDiagnostic()
    {
        const string source = """
            [editor_plugins]
            enabled="res://addons/yokiframe/plugin.cfg"
            """;

        var error = Assert.Throws<InvalidDataException>(() => new GodotProjectSettingsPatcher().Patch(source));

        Assert.Contains("editor_plugins", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("enabled", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PackedStringArray", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 读取指定 section 和 key 的唯一值，确保重复配置不会在断言阶段被忽略。
    /// </summary>
    /// <param name="content">完整 project.godot 文本。</param>
    /// <param name="sectionName">目标 section 名称。</param>
    /// <param name="key">目标配置键。</param>
    /// <returns>目标键右侧的原始值。</returns>
    private static string ReadSingleValue(string content, string sectionName, string key)
    {
        return Assert.Single(ReadValues(content, sectionName, key));
    }

    /// <summary>
    /// 按 section 行边界读取指定键的所有值，测试重复 owner 与保留性。
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
    /// 将 project.godot 拆为 section 行列表，避免测试通过脆弱的全文替换理解配置所有权。
    /// </summary>
    /// <param name="content">完整 project.godot 文本。</param>
    /// <param name="sectionName">目标 section 名称。</param>
    /// <returns>同名 section 的全部内容行。</returns>
    private static IReadOnlyList<IReadOnlyList<string>> ReadSections(string content, string sectionName)
    {
        List<IReadOnlyList<string>> sections = new();
        List<string>? currentLines = null;
        foreach (var rawLine in NormalizeLineEndings(content).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
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

    /// <summary>
    /// 统计固定 owner 文本出现次数，用于验证幂等 patch 不会累加插件路径。
    /// </summary>
    /// <param name="content">待检查文本。</param>
    /// <param name="value">目标文本。</param>
    /// <returns>非重叠出现次数。</returns>
    private static int CountValueOccurrences(string content, string value)
    {
        var count = 0;
        var searchIndex = 0;
        while ((searchIndex = content.IndexOf(value, searchIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            searchIndex += value.Length;
        }

        return count;
    }

    /// <summary>
    /// 统一换行符，保证测试的行模型不受执行平台影响。
    /// </summary>
    /// <param name="content">待规范化文本。</param>
    /// <returns>仅使用 LF 的文本。</returns>
    private static string NormalizeLineEndings(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }
}
