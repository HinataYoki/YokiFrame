namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 生成 Godot .NET 外层薄 C# EditorPlugin 与 Runtime bootstrap，不复制 Adapter 业务逻辑。
/// </summary>
public sealed class GodotPluginEntryPointBuilder
{
    /// <summary>
    /// 生成 Godot plugin.cfg 固定元数据。
    /// </summary>
    /// <returns>使用 LF 换行的 plugin.cfg。</returns>
    public string BuildPluginConfig()
    {
        return "[plugin]\n"
            + "name=\"YokiFrame\"\n"
            + "description=\"YokiFrame integration for Godot .NET.\"\n"
            + "author=\"YokiFrame\"\n"
            + "version=\"2.0.0\"\n"
            + "script=\"YokiFrameGodotEditorPlugin.cs\"\n";
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
            + "    /// 先启动 Core Godot Editor Host，再安装当前项目选择的 Tool 编辑器能力。\n"
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
            + "    /// <summary>确保需要宿主组合的 Godot Adapter 被加载并只注册惰性默认工厂。</summary>\n"
            + "    static YokiFrameGodotBootstrap()\n"
            + "    {\n"
            + "        GodotAudioKitRuntimeInstaller.EnsureInstalled();\n"
            + "        GodotSaveKitRuntimeInstaller.EnsureInstalled();\n"
            + "    }\n"
            + "}\n";
    }
}
