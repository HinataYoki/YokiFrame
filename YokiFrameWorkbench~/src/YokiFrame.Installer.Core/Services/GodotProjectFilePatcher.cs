using System.Xml;
using System.Xml.Linq;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 结构化维护 Godot 主项目中由 Installer 独占拥有的 YokiFrame ItemGroup。
/// </summary>
public sealed class GodotProjectFilePatcher
{
    private const string OWNER_LABEL = "YokiFrame";
    private const string PACKAGE_SOURCE_GLOB = "addons/yokiframe/package/YokiFrame/**/*.cs";
    private const string PACKAGE_PATH_PREFIX = "addons/yokiframe/package/YokiFrame/";
    private const string CORE_RUNTIME_PROJECT =
        "addons/yokiframe/package/YokiFrame/Core/Runtime/YokiFrame.csproj";
    private const string CORE_EDITOR_PROJECT =
        "addons/yokiframe/package/YokiFrame/Core/Editor/YokiFrame.Editor.csproj";
    private const string GODOT_ADAPTER_PROJECT =
        "addons/yokiframe/package/YokiFrame/Core/Adapters/Godot/Runtime/YokiFrame.Godot.Runtime.csproj";
    private const string GODOT_EDITOR_ADAPTER_PROJECT =
        "addons/yokiframe/package/YokiFrame/Core/Adapters/Godot/Editor/YokiFrame.Godot.Editor.csproj";
    private const string TOOLS_CONDITION =
        "$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))";
    private const string ACTION_KIT_PROJECT =
        "addons/yokiframe/package/YokiFrame/Tools/ActionKit/Runtime/YokiFrame.ActionKit.csproj";
    private const string ACTION_KIT_EDITOR_PROJECT =
        "addons/yokiframe/package/YokiFrame/Tools/ActionKit/Editor/YokiFrame.ActionKit.Editor.csproj";
    private const string AUDIO_KIT_PROJECT =
        "addons/yokiframe/package/YokiFrame/Tools/AudioKit/Runtime/YokiFrame.AudioKit.csproj";
    private const string AUDIO_KIT_ADAPTER_PROJECT =
        "addons/yokiframe/package/YokiFrame/Tools/AudioKit/Adapters/Godot/Runtime/YokiFrame.AudioKit.Godot.csproj";
    private const string AUDIO_KIT_EDITOR_PROJECT =
        "addons/yokiframe/package/YokiFrame/Tools/AudioKit/Editor/YokiFrame.AudioKit.Editor.csproj";
    private const string SAVE_KIT_PROJECT =
        "addons/yokiframe/package/YokiFrame/Tools/SaveKit/Runtime/YokiFrame.SaveKit.csproj";
    private const string SAVE_KIT_ADAPTER_PROJECT =
        "addons/yokiframe/package/YokiFrame/Tools/SaveKit/Adapters/Godot/Runtime/YokiFrame.SaveKit.Godot.csproj";
    private const string SAVE_KIT_EDITOR_PROJECT =
        "addons/yokiframe/package/YokiFrame/Tools/SaveKit/Editor/YokiFrame.SaveKit.Editor.csproj";
    private const string SPATIAL_KIT_PROJECT =
        "addons/yokiframe/package/YokiFrame/Tools/SpatialKit/Runtime/YokiFrame.SpatialKit.csproj";
    private const string SPATIAL_KIT_EDITOR_PROJECT =
        "addons/yokiframe/package/YokiFrame/Tools/SpatialKit/Editor/YokiFrame.SpatialKit.Editor.csproj";

    /// <summary>
    /// 创建或替换唯一 YokiFrame owner group，并保持其它项目节点、注释和顺序不变。
    /// </summary>
    /// <param name="projectXml">Godot 主 C# 项目的完整 XML。</param>
    /// <returns>可重复 patch 且文本幂等的项目 XML。</returns>
    public string Patch(string projectXml)
    {
        var project = ParseProject(projectXml);
        var root = project.Root!;
        var ownedGroups = FindOwnedGroups(root);
        if (ownedGroups.Count > 1)
        {
            throw new InvalidDataException(
                "Godot Project contains " + ownedGroups.Count + " ItemGroup entries labeled " + OWNER_LABEL + ".");
        }

        RemoveLegacyOwnedItems(root);

        var replacement = CreateOwnedGroup(root.Name.Namespace);
        if (ownedGroups.Count == 1)
        {
            ownedGroups[0].ReplaceWith(replacement);
        }
        else
        {
            root.Add(replacement);
        }

        return project.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// 解析并验证 MSBuild Project 根节点，把 XML 语法错误统一转换为可诊断安装异常。
    /// </summary>
    /// <param name="projectXml">待解析项目 XML。</param>
    /// <returns>已验证的项目文档。</returns>
    private static XDocument ParseProject(string projectXml)
    {
        try
        {
            var project = XDocument.Parse(projectXml, LoadOptions.PreserveWhitespace);
            if (project.Root == null || !string.Equals(project.Root.Name.LocalName, "Project", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Godot C# Project must use an MSBuild Project root element.");
            }

            return project;
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("Godot C# Project XML is invalid.", exception);
        }
    }

    /// <summary>
    /// 查找项目根层由 Installer 所有的 ItemGroup，避免误匹配嵌套 target 内容。
    /// </summary>
    /// <param name="root">MSBuild Project 根节点。</param>
    /// <returns>匹配 owner label 的顶层 ItemGroup。</returns>
    private static List<XElement> FindOwnedGroups(XElement root)
    {
        return root.Elements()
            .Where(static element => string.Equals(element.Name.LocalName, "ItemGroup", StringComparison.Ordinal))
            .Where(static element => string.Equals((string?)element.Attribute("Label"), OWNER_LABEL, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// 删除旧 Installer 遗留的包内 Compile/ProjectReference 项，避免源码与独立 Adapter 被重复编入主程序集。
    /// </summary>
    /// <param name="root">MSBuild Project 根节点。</param>
    private static void RemoveLegacyOwnedItems(XElement root)
    {
        var legacyItems = root.Elements()
            .Where(static element => string.Equals(element.Name.LocalName, "ItemGroup", StringComparison.Ordinal))
            .SelectMany(static group => group.Elements())
            .Where(static element => IsLegacyOwnedItem(element))
            .ToArray();
        for (var index = 0; index < legacyItems.Length; index++)
        {
            legacyItems[index].Remove();
        }
    }

    /// <summary>
    /// 判断 MSBuild item 是否是旧 Installer 指向 YokiFrame 受控包的编译或项目引用项。
    /// </summary>
    /// <param name="element">待检查 ItemGroup 子元素。</param>
    /// <returns>可由当前 owner group 替代时返回 true。</returns>
    private static bool IsLegacyOwnedItem(XElement element)
    {
        if (!string.Equals(element.Name.LocalName, "Compile", StringComparison.Ordinal)
            && !string.Equals(element.Name.LocalName, "ProjectReference", StringComparison.Ordinal))
        {
            return false;
        }

        return IsPackagePath((string?)element.Attribute("Include"))
            || IsPackagePath((string?)element.Attribute("Remove"));
    }

    /// <summary>
    /// 使用统一正斜杠和忽略大小写语义识别受控 Godot package 内路径。
    /// </summary>
    /// <param name="path">MSBuild Include/Remove 路径。</param>
    /// <returns>路径位于固定 YokiFrame package 根时返回 true。</returns>
    private static bool IsPackagePath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && path.Replace('\\', '/').StartsWith(PACKAGE_PATH_PREFIX, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 创建唯一 owner group，排除包源码 glob，并接入 Core 与已发布 Tool 的独立项目。
    /// </summary>
    /// <param name="projectNamespace">宿主项目使用的 XML namespace。</param>
    /// <returns>规范化 YokiFrame ItemGroup。</returns>
    private static XElement CreateOwnedGroup(XNamespace projectNamespace)
    {
        return new XElement(
            projectNamespace + "ItemGroup",
            new XAttribute("Label", OWNER_LABEL),
            new XElement(
                projectNamespace + "Compile",
                new XAttribute("Remove", PACKAGE_SOURCE_GLOB)),
            CreateReference(projectNamespace, CORE_RUNTIME_PROJECT, null,
                "YokiFrameToolsBuild=" + TOOLS_CONDITION),
            CreateReference(projectNamespace, CORE_EDITOR_PROJECT, TOOLS_CONDITION,
                "YokiFrameToolsBuild=True"),
            CreateReference(projectNamespace, GODOT_ADAPTER_PROJECT, null,
                "GodotProjectDir=$(MSBuildProjectDirectory);YokiFrameToolsBuild=" + TOOLS_CONDITION),
            CreateReference(projectNamespace, ACTION_KIT_PROJECT, null,
                "YokiFrameToolsBuild=" + TOOLS_CONDITION),
            CreateReference(projectNamespace, AUDIO_KIT_PROJECT, null,
                "YokiFrameToolsBuild=" + TOOLS_CONDITION),
            CreateReference(projectNamespace, AUDIO_KIT_ADAPTER_PROJECT, null,
                "GodotProjectDir=$(MSBuildProjectDirectory);YokiFrameToolsBuild=" + TOOLS_CONDITION),
            CreateReference(projectNamespace, SAVE_KIT_PROJECT, null,
                "YokiFrameToolsBuild=" + TOOLS_CONDITION),
            CreateReference(projectNamespace, SAVE_KIT_ADAPTER_PROJECT, null,
                "GodotProjectDir=$(MSBuildProjectDirectory);YokiFrameToolsBuild=" + TOOLS_CONDITION),
            CreateReference(projectNamespace, SPATIAL_KIT_PROJECT, null,
                "YokiFrameToolsBuild=" + TOOLS_CONDITION),
            CreateReference(projectNamespace, GODOT_EDITOR_ADAPTER_PROJECT, TOOLS_CONDITION,
                "GodotProjectDir=$(MSBuildProjectDirectory);YokiFrameToolsBuild=True"),
            CreateReference(projectNamespace, ACTION_KIT_EDITOR_PROJECT, TOOLS_CONDITION,
                "YokiFrameToolsBuild=True"),
            CreateReference(projectNamespace, AUDIO_KIT_EDITOR_PROJECT, TOOLS_CONDITION,
                "YokiFrameToolsBuild=True"),
            CreateReference(projectNamespace, SAVE_KIT_EDITOR_PROJECT, TOOLS_CONDITION,
                "YokiFrameToolsBuild=True"),
            CreateReference(projectNamespace, SPATIAL_KIT_EDITOR_PROJECT, TOOLS_CONDITION,
                "YokiFrameToolsBuild=True"));
    }

    /// <summary>创建带可选 Tools 条件和传递属性的规范化项目引用。</summary>
    private static XElement CreateReference(
        XNamespace projectNamespace,
        string include,
        string? condition,
        string additionalProperties)
    {
        var reference = new XElement(
            projectNamespace + "ProjectReference",
            new XAttribute("Include", include));
        if (!string.IsNullOrEmpty(condition)) reference.Add(new XAttribute("Condition", condition));
        if (!string.IsNullOrEmpty(additionalProperties))
            reference.Add(new XElement(projectNamespace + "AdditionalProperties", additionalProperties));
        return reference;
    }
}
