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
    /// 验证宿主项目 EditorPlugin 脚本与生成器约定的薄入口逐字一致，不能再夹带菜单或 FileBridge 业务。
    /// </summary>
    [Fact]
    public void BuildEditorBootstrapCreatesThinHostProjectPlugin()
    {
        Assert.Equal(EDITOR_BOOTSTRAP, new GodotPluginEntryPointBuilder().BuildEditorBootstrapScript());
    }

    /// <summary>
    /// 验证宿主项目运行时脚本与生成器约定的薄 bootstrap 逐字一致。
    /// </summary>
    [Fact]
    public void BuildRuntimeBootstrapCreatesThinHostProjectScript()
    {
        Assert.Equal(RUNTIME_BOOTSTRAP, new GodotPluginEntryPointBuilder().BuildRuntimeBootstrapScript());
    }

    private const string EDITOR_BOOTSTRAP =
        "#if TOOLS\n"
        + "using Godot;\n"
        + "using YokiFrame;\n\n"
        + "/// <summary>\n"
        + "/// 将 Godot EditorPlugin 资源桥接到独立 YokiFrame Editor Adapter。\n"
        + "/// </summary>\n"
        + "[Tool]\n"
        + "public partial class YokiFrameGodotEditorPlugin : GodotEditorPlugin\n"
        + "{\n"
        + "    /// <summary>\n"
        + "    /// 先启动 Core Godot Editor Host，再注册 Tool Editor 能力。\n"
        + "    /// </summary>\n"
        + "    public override void _EnterTree()\n"
        + "    {\n"
        + "        base._EnterTree();\n"
        + "        ActionKitEditorInstaller.EnsureInstalled();\n"
        + "        AudioKitEditorInstaller.EnsureInstalled();\n"
        + "        SaveKitEditorInstaller.EnsureInstalled();\n"
        + "        SpatialKitEditorInstaller.EnsureInstalled();\n"
        + "    }\n"
        + "}\n"
        + "#endif\n";

    private const string RUNTIME_BOOTSTRAP =
        "using YokiFrame;\n"
        + "using YokiFrame.Godot;\n\n"
        + "/// <summary>\n"
        + "/// 将 Godot 主项目脚本注册桥接到独立 YokiFrame Adapter bootstrap。\n"
        + "/// </summary>\n"
        + "public partial class YokiFrameGodotBootstrap : GodotBootstrap\n"
        + "{\n"
        + "    /// <summary>只触发独立 Tool Adapter 程序集加载。工厂注册由各自的 ModuleInitializer 完成。</summary>\n"
        + "    static YokiFrameGodotBootstrap()\n"
        + "    {\n"
        + "        _ = typeof(GodotAudioKitRuntimeInstaller);\n"
        + "        _ = typeof(GodotSaveKitRuntimeInstaller);\n"
        + "    }\n"
        + "}\n";

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
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(packageRoot, "package.json")));
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
