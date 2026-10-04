using System.Text.Json;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 生成 Godot .NET 外层薄 C# EditorPlugin 与 Runtime bootstrap，不复制 Adapter 业务逻辑。
/// </summary>
public sealed class GodotPluginEntryPointBuilder
{
    /// <summary>
    /// 生成 Godot plugin.cfg。版本只读取源包 package.json，避免与 Unity 包版本各写一份。
    /// </summary>
    /// <param name="sourcePackageRoot">包含 package.json 的 YokiFrame 包根。</param>
    /// <returns>使用 LF 换行的 plugin.cfg。</returns>
    public string BuildPluginConfig(string sourcePackageRoot)
    {
        return "[plugin]\n"
            + "name=\"YokiFrame\"\n"
            + "description=\"YokiFrame integration for Godot .NET.\"\n"
            + "author=\"YokiFrame\"\n"
            + "version=\"" + ReadPackageVersion(sourcePackageRoot) + "\"\n"
            + "script=\"YokiFrameGodotEditorPlugin.cs\"\n";
    }

    /// <summary>
    /// 读取 package.json 的 version。字段缺失、空白或 JSON 损坏时直接失败，不回退到写死版本。
    /// </summary>
    /// <param name="sourcePackageRoot">YokiFrame 包根。</param>
    /// <returns>去除首尾空白后的版本。</returns>
    private static string ReadPackageVersion(string sourcePackageRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePackageRoot);
        var packagePath = Path.Combine(Path.GetFullPath(sourcePackageRoot), "package.json");
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("YokiFrame package.json 不存在。", packagePath);
        }

        try
        {
            using var stream = File.OpenRead(packagePath);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("version", out var version)
                || version.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(version.GetString()))
            {
                throw new InvalidDataException("YokiFrame package.json 缺少非空字符串 version: " + packagePath);
            }

            return version.GetString()!.Trim();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("YokiFrame package.json JSON 无效: " + packagePath, exception);
        }
    }

    /// <summary>
    /// 生成编入 Godot 主项目的薄 C# EditorPlugin，使生命周期与菜单逻辑留在独立 Editor Adapter。
    /// </summary>
    /// <returns>使用 LF 换行的 YokiFrameGodotEditorPlugin.cs。</returns>
    public string BuildEditorBootstrapScript()
    {
        return "#if TOOLS\n"
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
    }

    /// <summary>
    /// 生成编入 Godot 主项目的薄 C# bootstrap，使脚本注册留在宿主程序集而逻辑继续由 Adapter 承担。
    /// </summary>
    /// <returns>使用 LF 换行的 YokiFrameGodotBootstrap.cs。</returns>
    public string BuildRuntimeBootstrapScript()
    {
        return "using YokiFrame;\n"
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
    }
}
