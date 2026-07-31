using System.Xml.Linq;

namespace YokiFrame.Protocol.Tests.Architecture;

/// <summary>
/// 验证纯 Core 与 Godot 4.7 .NET Adapter 使用可移植、单向依赖的独立项目边界。
/// </summary>
public sealed class GodotProjectBoundaryTests
{
    private const string CORE_PROJECT_RELATIVE_PATH = "Core/Runtime/YokiFrame.csproj";
    private const string CORE_EDITOR_PROJECT_RELATIVE_PATH = "Core/Editor/YokiFrame.Editor.csproj";
    private const string BUILD_PROPS_RELATIVE_PATH = "Directory.Build.props";
    private const string GODOT_PROJECT_RELATIVE_PATH =
        "Core/Adapters/Godot/Runtime/YokiFrame.Godot.Runtime.csproj";
    private const string GODOT_EDITOR_PROJECT_RELATIVE_PATH =
        "Core/Adapters/Godot/Editor/YokiFrame.Godot.Editor.csproj";
    private const string AUDIO_KIT_GODOT_PROJECT_RELATIVE_PATH =
        "Tools/AudioKit/Adapters/Godot/Runtime/YokiFrame.AudioKit.Godot.csproj";
    private const string SAVE_KIT_GODOT_PROJECT_RELATIVE_PATH =
        "Tools/SaveKit/Adapters/Godot/Runtime/YokiFrame.SaveKit.Godot.csproj";
    /// <summary>
    /// 验证未生成项目不携带固定 TableKit Runtime 或 Contracts 程序集。
    /// </summary>
    [Fact]
    public void TableKitDoesNotShipFixedRuntimeAssembly()
    {
        string runtimeRoot = Path.Combine(FindPackageRoot(), "Tools", "TableKit", "Runtime");
        Assert.False(File.Exists(Path.Combine(runtimeRoot, "ITableDataLoader.cs")));
        Assert.False(File.Exists(Path.Combine(runtimeRoot, "YokiFrame.TableKit.Contracts.asmdef")));
        Assert.False(File.Exists(Path.Combine(runtimeRoot, "YokiFrame.TableKit.Contracts.csproj")));
    }

    /// <summary>
    /// 验证纯 Core 项目固定使用 netstandard2.1 和 C# 9，并显式排除宿主、编辑器与测试源码。
    /// </summary>
    [Fact]
    public void CoreProjectDefinesPortableSourceBoundary()
    {
        var project = LoadPackageProject(CORE_PROJECT_RELATIVE_PATH);

        Assert.Equal("Microsoft.NET.Sdk", ReadSdk(project));
        Assert.Equal("netstandard2.1", ReadProperty(project, "TargetFramework"));
        Assert.Equal("9.0", ReadProperty(project, "LangVersion"));
        Assert.Equal("YokiFrame", ReadProperty(project, "AssemblyName"));
        Assert.Equal("false", ReadProperty(project, "EnableDefaultCompileItems"));
        Assert.Equal("true", ReadProperty(project, "TreatWarningsAsErrors"));

        var compile = ReadUnconditionalItem(project, "Compile");
        Assert.Equal("**/*.cs", NormalizePath((string?)compile.Attribute("Include")));
        var exclusions = NormalizePath((string?)compile.Attribute("Exclude"));
        Assert.Contains("**/Tests/**/*.cs", exclusions, StringComparison.Ordinal);
        var toolsConstants = ReadConditionalProperty(project, "DefineConstants", "YokiFrameToolsBuild");
        Assert.Contains("TOOLS", toolsConstants.Split(';'));
        Assert.Single(project.Descendants("Compile"));
    }

    /// <summary>
    /// 验证共享 Editor 项目保持纯 C#，并且只单向引用 Core Runtime。
    /// </summary>
    [Fact]
    public void CoreEditorProjectDefinesPortableToolBoundary()
    {
        var project = LoadPackageProject(CORE_EDITOR_PROJECT_RELATIVE_PATH);

        Assert.Equal("Microsoft.NET.Sdk", ReadSdk(project));
        Assert.Equal("netstandard2.1", ReadProperty(project, "TargetFramework"));
        Assert.Equal("9.0", ReadProperty(project, "LangVersion"));
        Assert.Equal("YokiFrame.Editor", ReadProperty(project, "AssemblyName"));
        Assert.Equal("false", ReadProperty(project, "EnableDefaultCompileItems"));
        Assert.Contains("GODOT", ReadProperty(project, "DefineConstants").Split(';'));
        Assert.Contains("TOOLS", ReadProperty(project, "DefineConstants").Split(';'));
        var reference = Assert.Single(project.Descendants("ProjectReference"));
        Assert.Equal("../Runtime/YokiFrame.csproj", NormalizePath((string?)reference.Attribute("Include")));
        Assert.Equal("YokiFrameToolsBuild=True", Assert.Single(reference.Elements("AdditionalProperties")).Value);
    }

    /// <summary>
    /// 验证 Godot Adapter 使用 4.7 SDK、net8.0 和独立源码集合，并且只单向引用纯 Core 项目。
    /// </summary>
    [Fact]
    public void GodotAdapterProjectReferencesOnlyPortableCore()
    {
        var project = LoadPackageProject(GODOT_PROJECT_RELATIVE_PATH);

        Assert.Equal("Godot.NET.Sdk/4.7.0", ReadSdk(project));
        Assert.Equal("net8.0", ReadProperty(project, "TargetFramework"));
        Assert.Equal("9.0", ReadProperty(project, "LangVersion"));
        Assert.Equal("YokiFrame.Godot.Runtime", ReadProperty(project, "AssemblyName"));
        Assert.Equal("true", ReadProperty(project, "EnableDynamicLoading"));
        Assert.Equal("false", ReadProperty(project, "EnableDefaultCompileItems"));
        Assert.Equal("true", ReadProperty(project, "TreatWarningsAsErrors"));
        Assert.Contains("GODOT", ReadProperty(project, "DefineConstants").Split(';'));
        var godotProjectDir = Assert.Single(project.Descendants("GodotProjectDir"));
        Assert.Equal("$(MSBuildProjectDirectory)", godotProjectDir.Value);
        Assert.Equal("'$(GodotProjectDir)' == ''", (string?)godotProjectDir.Attribute("Condition"));
        Assert.Equal(
            "$(YokiFrameBuildRoot)/bin/$(MSBuildProjectName)/$(Configuration)/",
            NormalizePath(ReadProperty(project, "OutputPath")));
        Assert.Equal(
            "$(YokiFrameBuildRoot)/obj/$(MSBuildProjectName)/$(Configuration)/",
            NormalizePath(ReadProperty(project, "IntermediateOutputPath")));

        var compile = ReadUnconditionalItem(project, "Compile");
        Assert.Equal("**/*.cs", NormalizePath((string?)compile.Attribute("Include")));
        var exclusions = NormalizePath((string?)compile.Attribute("Exclude"));
        Assert.Contains("bin/**/*.cs", exclusions, StringComparison.Ordinal);
        Assert.Contains("obj/**/*.cs", exclusions, StringComparison.Ordinal);
        Assert.Contains(".godot/**/*.cs", exclusions, StringComparison.Ordinal);
        Assert.Contains(
            "TOOLS",
            ReadConditionalProperty(project, "DefineConstants", "YokiFrameResolvedToolsBuild").Split(';'));
        var playerDefineTarget = Assert.Single(
            project.Descendants("Target"),
            element => string.Equals(
                (string?)element.Attribute("Name"),
                "RemoveToolsDefineForYokiFramePlayer",
                StringComparison.Ordinal));
        Assert.Equal("CoreCompile", (string?)playerDefineTarget.Attribute("BeforeTargets"));
        Assert.Contains(
            "YokiFrameResolvedToolsBuild",
            (string?)playerDefineTarget.Attribute("Condition"),
            StringComparison.Ordinal);
        Assert.Contains(
            "TOOLS(?=;|$)",
            Assert.Single(playerDefineTarget.Descendants("DefineConstants")).Value,
            StringComparison.Ordinal);
        var references = project.Descendants("ProjectReference").ToArray();
        Assert.Equal(2, references.Length);
        var reference = Assert.Single(references, element => element.Parent?.Attribute("Condition") == null);
        Assert.Equal("../../../Runtime/YokiFrame.csproj", NormalizePath((string?)reference.Attribute("Include")));
        Assert.Equal(
            "YokiFrameToolsBuild=$(YokiFrameResolvedToolsBuild)",
            Assert.Single(reference.Elements("AdditionalProperties")).Value);
        Assert.Equal(
            "GodotProjectDir",
            Assert.Single(reference.Elements("GlobalPropertiesToRemove")).Value);
        var editorReference = Assert.Single(references, element =>
            ((string?)element.Parent?.Attribute("Condition"))?.Contains(
                "YokiFrameResolvedToolsBuild",
                StringComparison.Ordinal) == true);
        Assert.Equal("../../../Editor/YokiFrame.Editor.csproj", NormalizePath((string?)editorReference.Attribute("Include")));
    }

    /// <summary>
    /// 验证 Godot 宿主路径只停留在 Adapter 项目，不能沿项目引用图污染 Core 与 Tool 项目配置。
    /// </summary>
    [Fact]
    public void GodotAdaptersRemoveHostProjectPropertyFromPortableReferences()
    {
        AssertGlobalPropertyRemoval(
            GODOT_PROJECT_RELATIVE_PATH,
            "../../../Runtime/YokiFrame.csproj");
        AssertGlobalPropertyRemoval(
            GODOT_EDITOR_PROJECT_RELATIVE_PATH,
            "../../../Runtime/YokiFrame.csproj",
            "../../../Editor/YokiFrame.Editor.csproj");
        AssertGlobalPropertyForwarded(
            GODOT_EDITOR_PROJECT_RELATIVE_PATH,
            "../Runtime/YokiFrame.Godot.Runtime.csproj");
        AssertGlobalPropertyRemoval(
            AUDIO_KIT_GODOT_PROJECT_RELATIVE_PATH,
            "../../../Runtime/YokiFrame.AudioKit.csproj");
        AssertGlobalPropertyForwarded(
            AUDIO_KIT_GODOT_PROJECT_RELATIVE_PATH,
            "../../../../../Core/Adapters/Godot/Runtime/YokiFrame.Godot.Runtime.csproj");
        AssertGlobalPropertyRemoval(
            SAVE_KIT_GODOT_PROJECT_RELATIVE_PATH,
            "../../../Runtime/YokiFrame.SaveKit.csproj");
        AssertGlobalPropertyForwarded(
            SAVE_KIT_GODOT_PROJECT_RELATIVE_PATH,
            "../../../../../Core/Adapters/Godot/Runtime/YokiFrame.Godot.Runtime.csproj");
    }

    /// <summary>
    /// 验证安装投影中的 Runtime Adapter 能从固定 addons 层级回到目标 Godot 项目根。
    /// </summary>
    [Fact]
    public void GodotRuntimeBuildPropsUseTheInstalledProjectRootDepth()
    {
        var propsPath = Path.Combine(
            FindPackageRoot(),
            "Core",
            "Adapters",
            "Godot",
            "Runtime",
            "Directory.Build.props");
        var props = File.ReadAllText(propsPath);

        Assert.Contains(
            "$(MSBuildThisFileDirectory)../../../../../../../../",
            props,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "$(MSBuildThisFileDirectory)../../../../../../../../../",
            props,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证共享 Directory.Build.props 按 Tools/Runtime profile 和项目名隔离 bin/obj。
    /// </summary>
    [Fact]
    public void PortableBuildPropsIsolateArtifactsUnderSharedBuildRoot()
    {
        var project = LoadPackageProject(BUILD_PROPS_RELATIVE_PATH);

        var profileProperties = project.Descendants("YokiFrameBuildProfile").ToArray();
        Assert.Equal(2, profileProperties.Length);
        Assert.Contains(profileProperties, property => property.Value.Trim() == "tools");
        Assert.Contains(profileProperties, property => property.Value.Trim() == "runtime");
        Assert.Equal(
            "$(YokiFrameBuildRoot)/$(YokiFrameBuildProfile)/bin/$(MSBuildProjectName)/",
            NormalizePath(ReadProperty(project, "BaseOutputPath")));
        Assert.Equal(
            "$(YokiFrameBuildRoot)/obj/$(MSBuildProjectName)/",
            NormalizePath(ReadProperty(project, "BaseIntermediateOutputPath")));
        Assert.Equal(
            "$(YokiFrameBuildRoot)/obj/$(MSBuildProjectName)/$(YokiFrameBuildProfile)/$(Configuration)/",
            NormalizePath(ReadProperty(project, "IntermediateOutputPath")));
    }

    /// <summary>
    /// 验证项目文件不在 SDK props 导入后重写基础输出路径，避免触发 MSB3539 和 NuGet assets 漂移。
    /// </summary>
    /// <param name="relativePath">待检查的包内项目路径。</param>
    [Theory]
    [InlineData(CORE_PROJECT_RELATIVE_PATH)]
    [InlineData(CORE_EDITOR_PROJECT_RELATIVE_PATH)]
    [InlineData(GODOT_PROJECT_RELATIVE_PATH)]
    public void PortableProjectsDoNotOverrideBasePathsAfterSdkImport(string relativePath)
    {
        var project = LoadPackageProject(relativePath);

        Assert.Empty(project.Descendants("BaseOutputPath"));
        Assert.Empty(project.Descendants("BaseIntermediateOutputPath"));
    }

    /// <summary>
    /// 读取包内项目文件，并在项目缺失时用明确断言形成可诊断的 RED 结果。
    /// </summary>
    /// <param name="relativePath">相对于 YokiFrame 包根的项目路径。</param>
    /// <returns>已解析的 MSBuild XML。</returns>
    private static XDocument LoadPackageProject(string relativePath)
    {
        var projectPath = Path.Combine(FindPackageRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(projectPath), "缺少独立项目边界: " + projectPath);
        return XDocument.Load(projectPath);
    }

    /// <summary>
    /// 读取 MSBuild Project 根节点的 SDK 声明。
    /// </summary>
    /// <param name="project">待检查的项目 XML。</param>
    /// <returns>SDK 标识。</returns>
    private static string ReadSdk(XDocument project)
    {
        return (string?)project.Root?.Attribute("Sdk") ?? string.Empty;
    }

    /// <summary>
    /// 读取项目中的唯一无条件具名属性，条件覆盖由对应断言单独验证。
    /// </summary>
    /// <param name="project">待检查的项目 XML。</param>
    /// <param name="propertyName">属性元素名。</param>
    /// <returns>去除首尾空白后的属性值。</returns>
    private static string ReadProperty(XDocument project, string propertyName)
    {
        var property = Assert.Single(
            project.Descendants(propertyName),
            element => element.Parent?.Name.LocalName == "PropertyGroup"
                && element.Parent.Parent == project.Root
                && element.Parent.Attribute("Condition") == null);
        return property.Value.Trim();
    }

    /// <summary>读取匹配条件片段的唯一具名属性。</summary>
    /// <param name="project">待检查的项目 XML。</param>
    /// <param name="propertyName">属性元素名。</param>
    /// <param name="conditionFragment">父 PropertyGroup 条件中必须包含的文本。</param>
    /// <returns>去除首尾空白后的属性值。</returns>
    private static string ReadConditionalProperty(
        XDocument project,
        string propertyName,
        string conditionFragment)
    {
        var property = Assert.Single(project.Descendants(propertyName), element =>
            ((string?)element.Parent?.Attribute("Condition"))?.Contains(
                conditionFragment,
                StringComparison.Ordinal) == true);
        return property.Value.Trim();
    }

    /// <summary>读取唯一无条件 MSBuild item。</summary>
    /// <param name="project">待检查的项目 XML。</param>
    /// <param name="itemName">Item 元素名。</param>
    /// <returns>唯一无条件 item。</returns>
    private static XElement ReadUnconditionalItem(XDocument project, string itemName)
    {
        return Assert.Single(
            project.Descendants(itemName),
            element => element.Parent?.Attribute("Condition") == null);
    }

    /// <summary>读取父 ItemGroup 条件匹配指定片段的唯一 item。</summary>
    /// <param name="project">待检查的项目 XML。</param>
    /// <param name="itemName">Item 元素名。</param>
    /// <param name="conditionFragment">父 ItemGroup 条件中必须包含的文本。</param>
    /// <returns>唯一匹配的条件 item。</returns>
    private static XElement ReadConditionalItem(
        XDocument project,
        string itemName,
        string conditionFragment)
    {
        return Assert.Single(project.Descendants(itemName), element =>
            ((string?)element.Parent?.Attribute("Condition"))?.Contains(
                conditionFragment,
                StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// 检查指定 Adapter 的可移植项目引用移除了 GodotProjectDir。
    /// </summary>
    /// <param name="relativePath">Adapter 项目相对路径。</param>
    /// <param name="portableReferences">需要移除宿主属性的项目引用路径。</param>
    private static void AssertGlobalPropertyRemoval(string relativePath, params string[] portableReferences)
    {
        var project = LoadPackageProject(relativePath);
        foreach (var include in portableReferences)
        {
            var reference = Assert.Single(
                project.Descendants("ProjectReference"),
                element => NormalizePath((string?)element.Attribute("Include")) == include);
            Assert.Equal(
                "GodotProjectDir",
                Assert.Single(reference.Elements("GlobalPropertiesToRemove")).Value);
        }
    }

    /// <summary>
    /// 检查指定 Adapter 到另一个 Godot Adapter 的引用没有误删宿主项目属性。
    /// </summary>
    /// <param name="relativePath">Adapter 项目相对路径。</param>
    /// <param name="godotReference">需要继续接收宿主属性的 Godot 项目引用路径。</param>
    private static void AssertGlobalPropertyForwarded(string relativePath, string godotReference)
    {
        var project = LoadPackageProject(relativePath);
        var reference = Assert.Single(
            project.Descendants("ProjectReference"),
            element => NormalizePath((string?)element.Attribute("Include")) == godotReference);
        Assert.Empty(reference.Elements("GlobalPropertiesToRemove"));
    }

    /// <summary>
    /// 统一项目路径分隔符，保证测试在 Windows、macOS 与 Linux 上使用同一断言。
    /// </summary>
    /// <param name="path">项目文件中的相对路径。</param>
    /// <returns>使用正斜杠的路径文本。</returns>
    private static string NormalizePath(string? path)
    {
        return (path ?? string.Empty).Replace('\\', '/');
    }

    /// <summary>
    /// 从测试输出目录向上定位 YokiFrame 包根，支持仓库开发构建与独立包测试。
    /// </summary>
    /// <returns>YokiFrame 包根绝对路径。</returns>
    private static string FindPackageRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "Assets", "YokiFrame");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("无法定位 YokiFrame 包根。");
    }
}
