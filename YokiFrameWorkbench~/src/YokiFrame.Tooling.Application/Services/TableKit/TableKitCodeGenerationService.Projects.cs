using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using YokiFrame.Tooling.Application.Models.TableKit;

namespace YokiFrame.Tooling.Application.Services.TableKit;

internal sealed partial class TableKitCodeGenerationService
{
    private const string GODOT_OWNER_LABEL = "YokiFrame.TableKit";
    private const string GODOT_CORE_RUNTIME_PROJECT =
        "addons/yokiframe/package/YokiFrame/Core/Runtime/YokiFrame.csproj";
    private const string GODOT_TOOLS_CONDITION =
        "$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))";

    /// <summary>生成可选 Unity asmdef，并移除同名 Godot project 防止宿主边界混用。</summary>
    /// <param name="options">包含 asmdef 开关和程序集名的选项。</param>
    /// <param name="contract">包含代码 target 和输出根的契约。</param>
    /// <param name="files">接收生成文件路径。</param>
    private static void GenerateUnityBoundary(
        TableKitOptions options,
        TableKitContract contract,
        List<string> files)
    {
        DeleteFileIfExists(Path.Combine(contract.OutputCodeDirectory, contract.AssemblyName + ".csproj"));
        string asmdefPath = Path.Combine(contract.OutputCodeDirectory, contract.AssemblyName + ".asmdef");
        if (!options.UseAssemblyDefinition)
        {
            DeleteFileIfExists(asmdefPath);
            DeleteFileIfExists(asmdefPath + ".meta");
            return;
        }

        JsonArray references = new("YokiFrame", "Luban.Runtime", "UniTask");
        AddOptionalCodeTargetReference(references, contract.CodeTarget);
        JsonObject document = new()
        {
            ["name"] = contract.AssemblyName,
            ["rootNamespace"] = string.Empty,
            ["references"] = references,
            ["includePlatforms"] = new JsonArray(),
            ["excludePlatforms"] = new JsonArray(),
            ["allowUnsafeCode"] = false,
            ["overrideReferences"] = false,
            ["precompiledReferences"] = new JsonArray(),
            ["autoReferenced"] = true,
            ["defineConstraints"] = new JsonArray(),
            ["versionDefines"] = new JsonArray(),
            ["noEngineReferences"] = false
        };
        WriteAtomically(asmdefPath, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        files.Add(asmdefPath);
    }

    /// <summary>生成 Godot csproj、移除同名 asmdef，并把独立项目接入 Godot 主项目。</summary>
    /// <param name="options">包含项目根与程序集名的选项。</param>
    /// <param name="contract">包含代码 target 和输出根的契约。</param>
    /// <param name="files">接收生成及更新文件路径。</param>
    private static void GenerateGodotBoundary(
        TableKitOptions options,
        TableKitContract contract,
        List<string> files)
    {
        string asmdefPath = Path.Combine(contract.OutputCodeDirectory, contract.AssemblyName + ".asmdef");
        DeleteFileIfExists(asmdefPath);
        DeleteFileIfExists(asmdefPath + ".meta");
        string mainProjectPath = FindGodotMainProject(options.ProjectRoot);
        XDocument mainProject = XDocument.Load(mainProjectPath, LoadOptions.PreserveWhitespace);
        string targetFramework = ReadTargetFramework(mainProject, mainProjectPath);
        string generatedProjectPath = Path.Combine(contract.OutputCodeDirectory, contract.AssemblyName + ".csproj");
        string coreProjectPath = Path.Combine(options.ProjectRoot, GODOT_CORE_RUNTIME_PROJECT);
        string coreProjectReference = ToProjectRelativePath(
            contract.OutputCodeDirectory,
            coreProjectPath);
        WriteAtomically(
            generatedProjectPath,
            BuildGodotProjectSource(contract, targetFramework, coreProjectReference));
        files.Add(generatedProjectPath);
        PatchGodotMainProject(mainProjectPath, mainProject, options.ProjectRoot, contract.OutputCodeDirectory, generatedProjectPath);
        files.Add(mainProjectPath);
    }

    /// <summary>生成纯 C# Godot TableKit 项目，编译门面、加载契约和 Luban 子目录。</summary>
    /// <param name="contract">TableKit 输出与 target 契约。</param>
    /// <param name="targetFramework">Godot 主项目使用的目标框架。</param>
    /// <param name="coreProjectReference">相对生成项目的 YokiFrame Core project 路径。</param>
    /// <returns>独立 MSBuild 项目文本。</returns>
    private static string BuildGodotProjectSource(
        TableKitContract contract,
        string targetFramework,
        string coreProjectReference)
    {
        XDocument project = new(
            new XElement(
                "Project",
                new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement(
                    "PropertyGroup",
                    new XElement("TargetFramework", targetFramework),
                    new XElement("LangVersion", "9.0"),
                    new XElement("AssemblyName", contract.AssemblyName),
                    new XElement("RootNamespace", contract.TopModule),
                    new XElement("ImplicitUsings", "disable"),
                    new XElement("Nullable", "disable"),
                    new XElement("EnableDefaultCompileItems", "false"),
                    new XElement("Deterministic", "true"),
                    new XElement("TreatWarningsAsErrors", "true"),
                    new XElement("GodotProjectDir", new XAttribute("Condition", "'$(GodotProjectDir)' == ''"), "$(MSBuildProjectDirectory)"),
                    new XElement("BaseOutputPath", "$(GodotProjectDir)/.godot/yokiframe/tablekit/bin/"),
                    new XElement("BaseIntermediateOutputPath", "$(GodotProjectDir)/.godot/yokiframe/tablekit/obj/")),
                BuildGodotCompileItems(contract.CodeTarget, coreProjectReference)));
        return project.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>构建生成项目的显式源码集合和代码 target 依赖引用。</summary>
    /// <param name="codeTarget">Luban 代码 target。</param>
    /// <param name="coreProjectReference">相对生成项目的 YokiFrame Core project 路径。</param>
    /// <returns>MSBuild ItemGroup。</returns>
    private static XElement BuildGodotCompileItems(
        string codeTarget,
        string coreProjectReference)
    {
        XElement itemGroup = new(
            "ItemGroup",
            new XElement(
                "Compile",
                new XAttribute("Include", "**/*.cs"),
                new XAttribute("Exclude", "bin/**/*.cs;obj/**/*.cs")),
            new XElement("Reference", new XAttribute("Include", "Luban.Runtime")),
            new XElement(
                "ProjectReference",
                new XAttribute("Include", coreProjectReference),
                new XElement("AdditionalProperties", "YokiFrameToolsBuild=$(YokiFrameToolsBuild)")));
        string? optionalReference = ResolveOptionalCodeTargetReference(codeTarget);
        if (optionalReference != null)
        {
            itemGroup.Add(new XElement("Reference", new XAttribute("Include", optionalReference)));
        }
        return itemGroup;
    }

    /// <summary>寻找唯一顶层 Godot 主 C# 项目；TableKit 生成项目固定位于项目子目录。</summary>
    /// <param name="projectRoot">Godot 项目根。</param>
    /// <returns>主项目绝对路径。</returns>
    private static string FindGodotMainProject(string projectRoot)
    {
        string[] projects = Directory.EnumerateFiles(Path.GetFullPath(projectRoot), "*.csproj", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (projects.Length != 1)
        {
            throw new InvalidDataException("TableKit 要求 Godot 项目根存在唯一主 C# project。");
        }
        return projects[0];
    }

    /// <summary>读取 Godot 主项目唯一无条件 TargetFramework。</summary>
    /// <param name="project">Godot 主项目 XML。</param>
    /// <param name="projectPath">用于错误诊断的项目路径。</param>
    /// <returns>目标框架文本。</returns>
    private static string ReadTargetFramework(XDocument project, string projectPath)
    {
        string[] frameworks = project.Descendants()
            .Where(static element => element.Name.LocalName == "TargetFramework" && element.Parent?.Attribute("Condition") == null)
            .Select(static element => element.Value.Trim())
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (frameworks.Length != 1)
        {
            throw new InvalidDataException("Godot 主项目缺少唯一 TargetFramework: " + projectPath);
        }
        return frameworks[0];
    }

    /// <summary>结构化维护 Godot 主项目中的 TableKit 独立程序集 owner group。</summary>
    /// <param name="mainProjectPath">Godot 主项目路径。</param>
    /// <param name="project">已加载的主项目 XML。</param>
    /// <param name="projectRoot">Godot 项目根。</param>
    /// <param name="outputDirectory">TableKit 代码根。</param>
    /// <param name="generatedProjectPath">TableKit 生成 project 路径。</param>
    private static void PatchGodotMainProject(
        string mainProjectPath,
        XDocument project,
        string projectRoot,
        string outputDirectory,
        string generatedProjectPath)
    {
        XElement root = project.Root ?? throw new InvalidDataException("Godot C# project 缺少 Project 根节点。");
        XElement[] ownedGroups = root.Elements()
            .Where(static element => element.Name.LocalName == "ItemGroup")
            .Where(static element => string.Equals((string?)element.Attribute("Label"), GODOT_OWNER_LABEL, StringComparison.Ordinal))
            .ToArray();
        if (ownedGroups.Length > 1) throw new InvalidDataException("Godot 主项目存在多个 YokiFrame.TableKit owner group。");

        XNamespace xmlNamespace = root.Name.Namespace;
        string outputRelative = ToProjectRelativePath(projectRoot, outputDirectory);
        string projectRelative = ToProjectRelativePath(projectRoot, generatedProjectPath);
        XElement replacement = new(
            xmlNamespace + "ItemGroup",
            new XAttribute("Label", GODOT_OWNER_LABEL),
            new XElement(xmlNamespace + "Compile", new XAttribute("Remove", outputRelative + "/**/*.cs")),
            new XElement(
                xmlNamespace + "ProjectReference",
                new XAttribute("Include", projectRelative),
                new XElement(
                    xmlNamespace + "AdditionalProperties",
                    "GodotProjectDir=$(MSBuildProjectDirectory);YokiFrameToolsBuild=" + GODOT_TOOLS_CONDITION)));
        if (ownedGroups.Length == 1) ownedGroups[0].ReplaceWith(replacement);
        else root.Add(replacement);
        WriteAtomically(mainProjectPath, project.ToString(SaveOptions.DisableFormatting));
    }

    /// <summary>把项目内路径转换为稳定正斜杠相对路径。</summary>
    /// <param name="projectRoot">项目根。</param>
    /// <param name="path">项目内绝对路径。</param>
    /// <returns>可写入 MSBuild 的相对路径。</returns>
    private static string ToProjectRelativePath(string projectRoot, string path)
    {
        return Path.GetRelativePath(Path.GetFullPath(projectRoot), Path.GetFullPath(path)).Replace('\\', '/');
    }

    /// <summary>向 Unity asmdef 引用数组加入代码 target 所需的可选 JSON 程序集。</summary>
    /// <param name="references">asmdef 引用数组。</param>
    /// <param name="codeTarget">Luban 代码 target。</param>
    private static void AddOptionalCodeTargetReference(JsonArray references, string codeTarget)
    {
        string? optionalReference = ResolveOptionalCodeTargetReference(codeTarget);
        if (optionalReference != null)
        {
            JsonNode? referenceNode = JsonValue.Create(optionalReference);
            references.Add(referenceNode);
        }
    }

    /// <summary>解析已知 C# JSON target 需要的额外程序集名。</summary>
    /// <param name="codeTarget">Luban 代码 target。</param>
    /// <returns>额外程序集名；无需额外依赖时返回 null。</returns>
    private static string? ResolveOptionalCodeTargetReference(string codeTarget)
    {
        return string.Equals(codeTarget, "cs-newtonsoft-json", StringComparison.OrdinalIgnoreCase)
            ? "Newtonsoft.Json"
            : null;
    }
}
