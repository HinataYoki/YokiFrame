using System.Text.Json;
using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Installer.Core.Tests;

/// <summary>
/// 验证 Installer 生成的 Godot .NET 外层插件入口保持合法、稳定且不承载运行时修复逻辑。
/// </summary>
public sealed class GodotPluginEntryPointBuilderTests
{
    /// <summary>
    /// 验证 plugin.cfg 只声明通用 Godot .NET 元数据，并从外层目录加载薄 C# EditorPlugin。
    /// </summary>
    [Fact]
    public void BuildPluginConfigUsesGodotDotnetMetadataAndLocalScript()
    {
        var packageRoot = FindRepositoryPackageRoot();
        var config = new GodotPluginEntryPointBuilder().BuildPluginConfig(packageRoot);
        var plugin = ReadSection(config, "plugin");

        Assert.Equal("YokiFrame", plugin["name"]);
        Assert.Equal("YokiFrame integration for Godot .NET.", plugin["description"]);
        Assert.Equal("YokiFrame", plugin["author"]);
        Assert.Equal(ReadPackageVersion(packageRoot), plugin["version"]);
        Assert.Equal("YokiFrameGodotEditorPlugin.cs", plugin["script"]);
        Assert.Equal(5, plugin.Count);
    }

    /// <summary>
    /// 验证宿主项目 EditorPlugin 脚本只继承独立 Editor Adapter，不复制菜单或 FileBridge 业务。
    /// </summary>
    [Fact]
    public void BuildEditorBootstrapCreatesThinHostProjectPlugin()
    {
        var script = NormalizeLineEndings(
            new GodotPluginEntryPointBuilder().BuildEditorBootstrapScript()).TrimEnd();

        Assert.StartsWith("#if TOOLS\n", script, StringComparison.Ordinal);
        Assert.EndsWith("#endif", script, StringComparison.Ordinal);
        Assert.Contains("using Godot;", script, StringComparison.Ordinal);
        Assert.Contains("using YokiFrame;", script, StringComparison.Ordinal);
        Assert.Contains("[Tool]", script, StringComparison.Ordinal);
        Assert.Contains(
            "public partial class YokiFrameGodotEditorPlugin : GodotEditorPlugin",
            script,
            StringComparison.Ordinal);
        Assert.Contains("public override void _EnterTree()", script, StringComparison.Ordinal);
        Assert.Contains("base._EnterTree();", script, StringComparison.Ordinal);
        Assert.Contains("ActionKitEditorInstaller.EnsureInstalled();", script, StringComparison.Ordinal);
        Assert.Contains("AudioKitEditorInstaller.EnsureInstalled();", script, StringComparison.Ordinal);
        Assert.Contains("SaveKitEditorInstaller.EnsureInstalled();", script, StringComparison.Ordinal);
        Assert.Contains("SpatialKitEditorInstaller.EnsureInstalled();", script, StringComparison.Ordinal);
        Assert.True(
            script.IndexOf("base._EnterTree();", StringComparison.Ordinal)
            < script.IndexOf("ActionKitEditorInstaller.EnsureInstalled();", StringComparison.Ordinal),
            "Tool Editor 能力只能在 Core Godot Editor Host 完成启动后安装。");
    }

    /// <summary>
    /// 验证宿主项目脚本只继承独立 Adapter 的 bootstrap，不复制 FileBridge 或生命周期业务。
    /// </summary>
    [Fact]
    public void BuildRuntimeBootstrapCreatesThinHostProjectScript()
    {
        var script = NormalizeLineEndings(
            new GodotPluginEntryPointBuilder().BuildRuntimeBootstrapScript()).TrimEnd();

        Assert.Contains("using YokiFrame;", script, StringComparison.Ordinal);
        Assert.Contains(
            "public partial class YokiFrameGodotBootstrap : GodotBootstrap",
            script,
            StringComparison.Ordinal);
        Assert.Contains("_ = typeof(GodotAudioKitRuntimeInstaller);", script, StringComparison.Ordinal);
        Assert.Contains("_ = typeof(GodotSaveKitRuntimeInstaller);", script, StringComparison.Ordinal);
    }

    /// <summary>按测试项目固定布局定位仓库包根，避免把版本号再写进测试。</summary>
    /// <returns>包含 package.json 的 YokiFrame 包根。</returns>
    private static string FindRepositoryPackageRoot()
    {
        var packageRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        var packagePath = Path.Combine(packageRoot, "package.json");
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("未找到 YokiFrame package.json。", packagePath);
        }

        return packageRoot;
    }

    /// <summary>读取 package.json version，作为 plugin.cfg 的唯一期望值。</summary>
    /// <param name="packageRoot">YokiFrame 包根。</param>
    /// <returns>package.json 中的版本。</returns>
    private static string ReadPackageVersion(string packageRoot)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(packageRoot, "package.json")));
        return document.RootElement.GetProperty("version").GetString()!;
    }

    /// <summary>
    /// 按 Godot ConfigFile 的 section/key 行模型读取指定 section，供测试验证合法元数据而不依赖行序。
    /// </summary>
    /// <param name="content">待解析的配置文本。</param>
    /// <param name="sectionName">目标 section 名称。</param>
    /// <returns>目标 section 内去除字符串引号后的键值。</returns>
    private static IReadOnlyDictionary<string, string> ReadSection(string content, string sectionName)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        var currentSection = string.Empty;

        foreach (var rawLine in NormalizeLineEndings(content).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                currentSection = line[1..^1];
                continue;
            }

            if (!string.Equals(currentSection, sectionName, StringComparison.Ordinal) || line.Length == 0 || line[0] == ';')
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            Assert.True(separatorIndex > 0, "Godot plugin metadata must use key=value lines.");
            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"');
            Assert.True(values.TryAdd(key, value), "Godot plugin metadata must not contain duplicate keys: " + key);
        }

        Assert.NotEmpty(values);
        return values;
    }

    /// <summary>
    /// 统一换行符，避免平台差异掩盖插件入口的实际文本契约。
    /// </summary>
    /// <param name="content">待规范化文本。</param>
    /// <returns>仅使用 LF 的文本。</returns>
    private static string NormalizeLineEndings(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }
}
