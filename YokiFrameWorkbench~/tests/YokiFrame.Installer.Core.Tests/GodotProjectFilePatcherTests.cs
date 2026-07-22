using System.Xml.Linq;
using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Installer.Core.Tests;

/// <summary>
/// 验证 Installer 对 Godot 主项目 csproj 的唯一 owner patch 行为。
/// </summary>
public sealed class GodotProjectFilePatcherTests
{
    private const string OWNER_LABEL = "YokiFrame";
    private const string PACKAGE_SOURCE_GLOB = "addons/yokiframe/package/YokiFrame/**/*.cs";
    private const string CORE_RUNTIME_PROJECT =
        "addons/yokiframe/package/YokiFrame/Core/Runtime/YokiFrame.csproj";
    private const string CORE_EDITOR_PROJECT =
        "addons/yokiframe/package/YokiFrame/Core/Editor/YokiFrame.Editor.csproj";
    private const string GODOT_ADAPTER_PROJECT =
        "addons/yokiframe/package/YokiFrame/Core/Adapters/Godot/Runtime/YokiFrame.Godot.Runtime.csproj";
    private const string GODOT_EDITOR_ADAPTER_PROJECT =
        "addons/yokiframe/package/YokiFrame/Core/Adapters/Godot/Editor/YokiFrame.Godot.Editor.csproj";
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
    /// 验证缺少 owner group 时会创建唯一 YokiFrame ItemGroup，并完整保留用户拥有的项目节点。
    /// </summary>
    [Fact]
    public void PatchCreatesOwnedGroupAndPreservesUnownedProjectXml()
    {
        const string source = """
            <Project Sdk="Godot.NET.Sdk/4.7.0" CustomAttribute="keep-me">
              <!-- user-owned-comment -->
              <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
                <DefineConstants>$(DefineConstants);USER_SYMBOL</DefineConstants>
              </PropertyGroup>
              <ItemGroup Label="UserOwned">
                <Compile Include="Scripts/**/*.cs" />
                <PackageReference Include="User.Package" Version="1.2.3" />
              </ItemGroup>
              <Target Name="UserTarget" BeforeTargets="Build" />
            </Project>
            """;

        var patched = new GodotProjectFilePatcher().Patch(source);
        var project = XDocument.Parse(patched, LoadOptions.PreserveWhitespace);

        Assert.Equal("Godot.NET.Sdk/4.7.0", (string?)project.Root?.Attribute("Sdk"));
        Assert.Equal("keep-me", (string?)project.Root?.Attribute("CustomAttribute"));
        Assert.Contains(project.DescendantNodes().OfType<XComment>(), comment => comment.Value.Trim() == "user-owned-comment");
        Assert.Equal("Scripts/**/*.cs", ReadSingleAttribute(project, "Compile", "Include", "UserOwned"));
        Assert.Equal("1.2.3", ReadSingleAttribute(project, "PackageReference", "Version", "UserOwned"));
        Assert.Equal("Build", ReadSingleAttribute(project, "Target", "BeforeTargets"));
        AssertOwnedGroup(project);
    }

    /// <summary>
    /// 验证已有唯一 owner group 时只替换其内容，不删除或重写相邻的用户 ItemGroup。
    /// </summary>
    [Fact]
    public void PatchReplacesOnlyTheOwnedItemGroup()
    {
        const string source = """
            <Project Sdk="Godot.NET.Sdk/4.7.0">
              <ItemGroup Label="BeforeOwner">
                <None Include="before.txt" />
              </ItemGroup>
              <ItemGroup Label="YokiFrame">
                <Compile Remove="legacy/**/*.cs" />
                <ProjectReference Include="legacy/YokiFrame.csproj" />
                <None Include="stale-owner-entry.txt" />
              </ItemGroup>
              <ItemGroup Label="AfterOwner">
                <AdditionalFiles Include="after.json" />
              </ItemGroup>
            </Project>
            """;

        var patched = new GodotProjectFilePatcher().Patch(source);
        var project = XDocument.Parse(patched, LoadOptions.PreserveWhitespace);

        Assert.Equal("before.txt", ReadSingleAttribute(project, "None", "Include", "BeforeOwner"));
        Assert.Equal("after.json", ReadSingleAttribute(project, "AdditionalFiles", "Include", "AfterOwner"));
        Assert.DoesNotContain(project.Descendants(), element =>
            string.Equals((string?)element.Attribute("Include"), "stale-owner-entry.txt", StringComparison.Ordinal));
        Assert.Equal(
            ["BeforeOwner", OWNER_LABEL, "AfterOwner"],
            project.Root?.Elements()
                .Where(element => element.Name.LocalName == "ItemGroup")
                .Select(element => (string?)element.Attribute("Label")));
        AssertOwnedGroup(project);
    }

    /// <summary>
    /// 验证旧 Installer 的包内 Compile/ProjectReference 会被移除，同时保留同组用户项目项。
    /// </summary>
    [Fact]
    public void PatchRemovesOnlyLegacyYokiFramePackageItems()
    {
        const string source = """
            <Project Sdk="Godot.NET.Sdk/4.7.0">
              <ItemGroup Label="LegacyMixed">
                <Compile Include="addons\yokiframe\package\YokiFrame\Core\Runtime\**\*.cs" />
                <Compile Remove="addons/yokiframe/package/YokiFrame/Core/Adapters/Unity/**/*.cs" />
                <ProjectReference Include="addons/yokiframe/package/YokiFrame/legacy.csproj" />
                <Compile Include="Scripts/**/*.cs" />
                <ProjectReference Include="Libraries/UserProject.csproj" />
                <None Include="addons/yokiframe/package/YokiFrame/user-note.txt" />
              </ItemGroup>
            </Project>
            """;

        var patched = new GodotProjectFilePatcher().Patch(source);
        var project = XDocument.Parse(patched);
        var legacyGroup = Assert.Single(FindItemGroups(project, "LegacyMixed"));

        Assert.Contains(legacyGroup.Elements(), static element =>
            string.Equals((string?)element.Attribute("Include"), "Scripts/**/*.cs", StringComparison.Ordinal));
        Assert.Contains(legacyGroup.Elements(), static element =>
            string.Equals((string?)element.Attribute("Include"), "Libraries/UserProject.csproj", StringComparison.Ordinal));
        Assert.Contains(legacyGroup.Elements(), static element =>
            string.Equals(
                (string?)element.Attribute("Include"),
                "addons/yokiframe/package/YokiFrame/user-note.txt",
                StringComparison.Ordinal));
        Assert.DoesNotContain(legacyGroup.Elements(), static element =>
            element.Name.LocalName is "Compile" or "ProjectReference"
            && element.Attributes().Any(static attribute =>
                attribute.Value.Replace('\\', '/').StartsWith(
                    "addons/yokiframe/package/YokiFrame/",
                    StringComparison.OrdinalIgnoreCase)));
        AssertOwnedGroup(project);
    }

    /// <summary>
    /// 验证同一内容重复执行 patch 不会继续改写文本或累加 owner group。
    /// </summary>
    [Fact]
    public void PatchIsTextuallyIdempotent()
    {
        const string source = """
            <Project Sdk="Godot.NET.Sdk/4.7.0">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """;
        var patcher = new GodotProjectFilePatcher();

        var firstPatch = patcher.Patch(source);
        var secondPatch = patcher.Patch(firstPatch);

        Assert.Equal(firstPatch, secondPatch);
        AssertOwnedGroup(XDocument.Parse(secondPatch));
    }

    /// <summary>
    /// 验证无法解析或根节点不是 MSBuild Project 的输入会被诊断拒绝，而不是生成部分项目文件。
    /// </summary>
    /// <param name="source">无效项目 XML。</param>
    [Theory]
    [InlineData("<NotProject />")]
    [InlineData("<Project><ItemGroup></Project>")]
    public void PatchRejectsInvalidMsbuildProjectWithDiagnostic(string source)
    {
        var error = Assert.Throws<InvalidDataException>(() => new GodotProjectFilePatcher().Patch(source));

        Assert.Contains("Godot", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Project", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证多个 YokiFrame owner group 会被明确拒绝，避免 Installer 猜测并覆盖不明确的所有权。
    /// </summary>
    [Fact]
    public void PatchRejectsMultipleOwnedGroupsWithDiagnostic()
    {
        const string source = """
            <Project Sdk="Godot.NET.Sdk/4.7.0">
              <ItemGroup Label="YokiFrame" />
              <ItemGroup Label="UserOwned" />
              <ItemGroup Label="YokiFrame" />
            </Project>
            """;

        var error = Assert.Throws<InvalidDataException>(() => new GodotProjectFilePatcher().Patch(source));

        Assert.Contains(OWNER_LABEL, error.Message, StringComparison.Ordinal);
        Assert.Contains("2", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证唯一 owner group 只包含包源码排除项、宿主 Adapter，以及受 Tools 条件约束的工具引用。
    /// </summary>
    /// <param name="project">待检查的项目 XML。</param>
    private static void AssertOwnedGroup(XDocument project)
    {
        var ownerGroup = Assert.Single(FindItemGroups(project, OWNER_LABEL));
        Assert.DoesNotContain(ownerGroup.DescendantsAndSelf(), static element =>
            element.Attributes().Any(static attribute =>
                attribute.Value.Contains("UIKit", StringComparison.OrdinalIgnoreCase)));
        Assert.Collection(
            ownerGroup.Elements(),
            compile =>
            {
                Assert.Equal("Compile", compile.Name.LocalName);
                Assert.Equal(PACKAGE_SOURCE_GLOB, (string?)compile.Attribute("Remove"));
                Assert.DoesNotContain(
                    compile.Attributes(),
                    attribute => attribute.Name.LocalName != "Remove");
            },
            coreRuntimeReference =>
            {
                Assert.Equal(CORE_RUNTIME_PROJECT, (string?)coreRuntimeReference.Attribute("Include"));
                Assert.Equal(
                    "YokiFrameToolsBuild=$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))",
                    Assert.Single(coreRuntimeReference.Elements()).Value);
            },
            coreEditorReference =>
            {
                Assert.Equal(CORE_EDITOR_PROJECT, (string?)coreEditorReference.Attribute("Include"));
                Assert.Equal(
                    "$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))",
                    (string?)coreEditorReference.Attribute("Condition"));
                Assert.Equal("YokiFrameToolsBuild=True", Assert.Single(coreEditorReference.Elements()).Value);
            },
            runtimeReference =>
            {
                Assert.Equal("ProjectReference", runtimeReference.Name.LocalName);
                Assert.Equal(GODOT_ADAPTER_PROJECT, (string?)runtimeReference.Attribute("Include"));
                Assert.DoesNotContain(
                    runtimeReference.Attributes(),
                    attribute => attribute.Name.LocalName != "Include");
                var additionalProperties = Assert.Single(runtimeReference.Elements());
                Assert.Equal("AdditionalProperties", additionalProperties.Name.LocalName);
                Assert.Equal(
                    "GodotProjectDir=$(MSBuildProjectDirectory);YokiFrameToolsBuild="
                    + "$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))",
                    additionalProperties.Value);
            },
            actionKitReference =>
            {
                Assert.Equal("ProjectReference", actionKitReference.Name.LocalName);
                Assert.Equal(ACTION_KIT_PROJECT, (string?)actionKitReference.Attribute("Include"));
                Assert.DoesNotContain(
                    actionKitReference.Attributes(),
                    attribute => attribute.Name.LocalName != "Include");
                var additionalProperties = Assert.Single(actionKitReference.Elements());
                Assert.Equal("AdditionalProperties", additionalProperties.Name.LocalName);
                Assert.Equal(
                    "YokiFrameToolsBuild=$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))",
                    additionalProperties.Value);
            },
            audioKitReference =>
            {
                Assert.Equal(AUDIO_KIT_PROJECT, (string?)audioKitReference.Attribute("Include"));
                Assert.Equal(
                    "YokiFrameToolsBuild=$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))",
                    Assert.Single(audioKitReference.Elements()).Value);
            },
            audioKitAdapterReference =>
            {
                Assert.Equal(AUDIO_KIT_ADAPTER_PROJECT, (string?)audioKitAdapterReference.Attribute("Include"));
                Assert.Equal(
                    "GodotProjectDir=$(MSBuildProjectDirectory);YokiFrameToolsBuild="
                    + "$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))",
                    Assert.Single(audioKitAdapterReference.Elements()).Value);
            },
            saveKitReference =>
            {
                Assert.Equal(SAVE_KIT_PROJECT, (string?)saveKitReference.Attribute("Include"));
                Assert.Equal(
                    "YokiFrameToolsBuild=$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))",
                    Assert.Single(saveKitReference.Elements()).Value);
            },
            saveKitAdapterReference =>
            {
                Assert.Equal(SAVE_KIT_ADAPTER_PROJECT, (string?)saveKitAdapterReference.Attribute("Include"));
                Assert.Equal(
                    "GodotProjectDir=$(MSBuildProjectDirectory);YokiFrameToolsBuild="
                    + "$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))",
                    Assert.Single(saveKitAdapterReference.Elements()).Value);
            },
            spatialKitReference =>
            {
                Assert.Equal(SPATIAL_KIT_PROJECT, (string?)spatialKitReference.Attribute("Include"));
                Assert.Equal(
                    "YokiFrameToolsBuild=$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))",
                    Assert.Single(spatialKitReference.Elements()).Value);
            },
            editorReference =>
            {
                Assert.Equal("ProjectReference", editorReference.Name.LocalName);
                Assert.Equal(GODOT_EDITOR_ADAPTER_PROJECT, (string?)editorReference.Attribute("Include"));
                Assert.Equal(
                    "$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))",
                    (string?)editorReference.Attribute("Condition"));
                Assert.DoesNotContain(editorReference.Attributes(), attribute =>
                    attribute.Name.LocalName is not ("Include" or "Condition"));
                var additionalProperties = Assert.Single(editorReference.Elements());
                Assert.Equal("AdditionalProperties", additionalProperties.Name.LocalName);
                Assert.Equal(
                    "GodotProjectDir=$(MSBuildProjectDirectory);YokiFrameToolsBuild=True",
                    additionalProperties.Value);
            },
            actionKitEditorReference =>
            {
                Assert.Equal("ProjectReference", actionKitEditorReference.Name.LocalName);
                Assert.Equal(ACTION_KIT_EDITOR_PROJECT, (string?)actionKitEditorReference.Attribute("Include"));
                Assert.Equal(
                    "$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))",
                    (string?)actionKitEditorReference.Attribute("Condition"));
                Assert.DoesNotContain(actionKitEditorReference.Attributes(), attribute =>
                    attribute.Name.LocalName is not ("Include" or "Condition"));
                var additionalProperties = Assert.Single(actionKitEditorReference.Elements());
                Assert.Equal("AdditionalProperties", additionalProperties.Name.LocalName);
                Assert.Equal("YokiFrameToolsBuild=True", additionalProperties.Value);
            },
            audioKitEditorReference =>
            {
                Assert.Equal("ProjectReference", audioKitEditorReference.Name.LocalName);
                Assert.Equal(AUDIO_KIT_EDITOR_PROJECT, (string?)audioKitEditorReference.Attribute("Include"));
                Assert.Equal(
                    "$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))",
                    (string?)audioKitEditorReference.Attribute("Condition"));
                Assert.Equal("YokiFrameToolsBuild=True", Assert.Single(audioKitEditorReference.Elements()).Value);
            },
            saveKitEditorReference =>
            {
                Assert.Equal("ProjectReference", saveKitEditorReference.Name.LocalName);
                Assert.Equal(SAVE_KIT_EDITOR_PROJECT, (string?)saveKitEditorReference.Attribute("Include"));
                Assert.Equal(
                    "$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))",
                    (string?)saveKitEditorReference.Attribute("Condition"));
                Assert.Equal("YokiFrameToolsBuild=True", Assert.Single(saveKitEditorReference.Elements()).Value);
            },
            spatialKitEditorReference =>
            {
                Assert.Equal("ProjectReference", spatialKitEditorReference.Name.LocalName);
                Assert.Equal(SPATIAL_KIT_EDITOR_PROJECT, (string?)spatialKitEditorReference.Attribute("Include"));
                Assert.Equal(
                    "$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))",
                    (string?)spatialKitEditorReference.Attribute("Condition"));
                Assert.Equal("YokiFrameToolsBuild=True", Assert.Single(spatialKitEditorReference.Elements()).Value);
            });
    }

    /// <summary>
    /// 按 Label 查找项目顶层 ItemGroup，确保测试只检查 Installer 的显式所有权边界。
    /// </summary>
    /// <param name="project">待查询的项目 XML。</param>
    /// <param name="label">ItemGroup 所有权标签。</param>
    /// <returns>匹配的顶层 ItemGroup。</returns>
    private static IEnumerable<XElement> FindItemGroups(XDocument project, string label)
    {
        return project.Root?.Elements()
            .Where(element => element.Name.LocalName == "ItemGroup")
            .Where(element => string.Equals((string?)element.Attribute("Label"), label, StringComparison.Ordinal))
            ?? Enumerable.Empty<XElement>();
    }

    /// <summary>
    /// 从指定 owner 的唯一顶层元素读取属性，减少保留性断言中的 XML 查询噪音。
    /// </summary>
    /// <param name="project">待查询的项目 XML。</param>
    /// <param name="elementName">目标元素名。</param>
    /// <param name="attributeName">目标属性名。</param>
    /// <param name="ownerLabel">可选的 ItemGroup Label；为空时直接查询项目顶层元素。</param>
    /// <returns>目标属性值。</returns>
    private static string? ReadSingleAttribute(
        XDocument project,
        string elementName,
        string attributeName,
        string? ownerLabel = null)
    {
        var elements = ownerLabel == null
            ? project.Root?.Elements().Where(element => element.Name.LocalName == elementName)
            : FindItemGroups(project, ownerLabel)
                .SelectMany(group => group.Elements().Where(element => element.Name.LocalName == elementName));
        var element = Assert.Single(elements ?? Enumerable.Empty<XElement>());
        return (string?)element.Attribute(attributeName);
    }
}
