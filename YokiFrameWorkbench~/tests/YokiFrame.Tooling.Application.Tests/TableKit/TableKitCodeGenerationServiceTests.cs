using System.Text.Json;
using System.Xml.Linq;
using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Tooling.Application.Services.TableKit;

namespace YokiFrame.Tooling.Application.Tests.TableKit;

/// <summary>验证 Workbench 编排 TableKit Editor/CodeGenKit 生成跨宿主项目代码。</summary>
public sealed partial class TableKitCodeGenerationServiceTests
{
    /// <summary>TableKit 专属模板与通用 CodeGenKit 必须分别归属对应 Editor 程序集。</summary>
    [Fact]
    public void GetAssembly_TableKit生成器_AssemblyBoundary引用Editor()
    {
        Assert.Equal(
            "YokiFrame.TableKit.Editor",
            typeof(global::YokiFrame.TableKitSourceCodeGenerator).Assembly.GetName().Name);
        Assert.Contains(
            typeof(global::YokiFrame.TableKitSourceCodeGenerator).Assembly.GetReferencedAssemblies(),
            static assembly => string.Equals(assembly.Name, "YokiFrame.Editor", StringComparison.Ordinal));
    }

    /// <summary>Unity 直接生成门面、加载契约和 asmdef，不再产生 manifest 或 Contracts 引用。</summary>
    [Fact]
    public void Generate_Unity完整输出_写入门面且不产生清单()
    {
        string root = CreateProjectRoot("unity");
        try
        {
            TableKitOptions options = CreateOptions(root, "Assets/Scripts/TableKit") with
            {
                UseAssemblyDefinition = true,
                GenerateExternalTypeUtil = true
            };
            TableKitContract contract = CreateContract(root, options.OutputCodeDir) with
            {
                ExternalTypeMappings = CreateExternalTypeMappings()
            };
            string outputRoot = contract.OutputCodeDirectory;
            SeedLegacyOutputs(outputRoot);

            IReadOnlyList<string> files = new TableKitCodeGenerationService().Generate(options, contract);

            AssertUnityOutputFiles(outputRoot, files);
            AssertUnityGeneratedSources(outputRoot);
            AssertUnityAssemblyDefinition(Path.Combine(outputRoot, "YokiFrame.TableKit.asmdef"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>显式 topModule 只决定 Luban manager 类型，TableKit 门面和 Loader 契约仍固定在 YokiFrame。</summary>
    [Fact]
    public void Generate_显式topModule_门面仍在YokiFrame()
    {
        string root = CreateProjectRoot("unity");
        try
        {
            TableKitOptions options = CreateOptions(root, "Assets/Scripts/TableKit");
            TableKitContract contract = CreateContract(root, options.OutputCodeDir) with
            {
                TopModule = "Game.Config"
            };
            WriteManagerSource(contract);

            new TableKitCodeGenerationService().Generate(options, contract);

            string facade = ToolingFileObservation.ReadUtf8(Path.Combine(contract.OutputCodeDirectory, "TableKit.cs"));
            string loader = ToolingFileObservation.ReadUtf8(Path.Combine(contract.OutputCodeDirectory, "ITableDataLoader.cs"));
            AssertCodeLines(
                facade,
                "namespace YokiFrame",
                "private static global::Game.Config.Tables sTables;",
                "public static global::Game.Config.Tables Tables => sTables ?? throw new InvalidOperationException(\"TableKit 尚未初始化。\");",
                "public static global::Game.Config.Tables TablesEditor");
            AssertOmitsCodeLines(facade, "namespace Game.Config");
            AssertOmitsIdentifiers(facade, "LubanTables");
            AssertCodeLines(loader, "namespace YokiFrame");
            AssertOmitsCodeLines(loader, "namespace YokiFrame.TableKit");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>Godot 使用同名 csproj，并由主项目排除源码 glob 后通过 ProjectReference 接入。</summary>
    [Fact]
    public void Generate_Godot项目_主工程引用独立程序集()
    {
        string root = CreateProjectRoot("godot");
        try
        {
            TableKitOptions options = CreateOptions(root, "Scripts/TableKit");
            TableKitContract contract = CreateContract(root, options.OutputCodeDir);

            IReadOnlyList<string> files = new TableKitCodeGenerationService().Generate(options, contract);

            string generatedProjectPath = Path.Combine(contract.OutputCodeDirectory, "YokiFrame.TableKit.csproj");
            Assert.Contains(generatedProjectPath, files);
            Assert.False(File.Exists(Path.Combine(contract.OutputCodeDirectory, "YokiFrame.TableKit.asmdef")));
            XDocument generatedProject = XDocument.Load(generatedProjectPath);
            Assert.Equal("Microsoft.NET.Sdk", (string?)generatedProject.Root?.Attribute("Sdk"));
            Assert.Equal("net8.0", Assert.Single(generatedProject.Descendants("TargetFramework")).Value);
            Assert.Equal("YokiFrame.TableKit", Assert.Single(generatedProject.Descendants("AssemblyName")).Value);
            Assert.Contains(generatedProject.Descendants("Reference"), static reference =>
                string.Equals((string?)reference.Attribute("Include"), "Luban.Runtime", StringComparison.Ordinal));
            XElement coreReference = Assert.Single(generatedProject.Descendants("ProjectReference"));
            Assert.Equal(
                "../../addons/yokiframe/package/YokiFrame/Core/Runtime/YokiFrame.csproj",
                ((string?)coreReference.Attribute("Include"))?.Replace('\\', '/'));
            Assert.Equal(
                "YokiFrameToolsBuild=$(YokiFrameToolsBuild)",
                Assert.Single(coreReference.Elements("AdditionalProperties")).Value);

            XDocument mainProject = XDocument.Load(Path.Combine(root, "Game.csproj"));
            XElement ownerGroup = Assert.Single(mainProject.Root!.Elements("ItemGroup"), static group =>
                string.Equals((string?)group.Attribute("Label"), "YokiFrame.TableKit", StringComparison.Ordinal));
            Assert.Equal("Scripts/TableKit/**/*.cs", (string?)Assert.Single(ownerGroup.Elements("Compile")).Attribute("Remove"));
            Assert.Equal(
                "Scripts/TableKit/YokiFrame.TableKit.csproj",
                (string?)Assert.Single(ownerGroup.Elements("ProjectReference")).Attribute("Include"));
            Assert.Equal(
                "GodotProjectDir=$(MSBuildProjectDirectory);YokiFrameToolsBuild=$([System.String]::Copy(';$(DefineConstants);').Contains(';TOOLS;'))",
                Assert.Single(ownerGroup.Descendants("AdditionalProperties")).Value);
            string[] defineConstants = generatedProject.Descendants("DefineConstants").Select(static constants => constants.Value).ToArray();
            Assert.Contains(defineConstants, static constants => constants == "$(DefineConstants);GODOT");
            Assert.Contains(defineConstants, static constants => constants == "$(DefineConstants);TOOLS");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>只修改运行时配置不会改写稳定生成源码或更新时间戳。</summary>
    [Fact]
    public void Generate_只改运行时配置_不重写源码字节()
    {
        string root = CreateProjectRoot("unity");
        try
        {
            TableKitOptions options = CreateOptions(root, "Assets/Scripts/TableKit");
            TableKitContract contract = CreateContract(root, options.OutputCodeDir);
            TableKitCodeGenerationService service = new();
            service.Generate(options, contract);
            string facadePath = Path.Combine(contract.OutputCodeDirectory, "TableKit.cs");
            string loaderPath = Path.Combine(contract.OutputCodeDirectory, "ITableDataLoader.cs");
            byte[] facade = File.ReadAllBytes(facadePath);
            byte[] loader = File.ReadAllBytes(loaderPath);
            DateTime stableTimestamp = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(facadePath, stableTimestamp);
            File.SetLastWriteTimeUtc(loaderPath, stableTimestamp);

            TableKitOptions changedOptions = options with
            {
                OutputDataDir = "Assets/StreamingAssets/Generated/Tables",
                IsAddressable = true,
                RuntimePathPattern = "external://tables/{0}",
                UseRawResourceLoading = false
            };
            TableKitContract changedContract = contract with
            {
                DataTarget = "json",
                DataExtension = "json",
                IsAddressable = true,
                RuntimePathPattern = "external://tables/{0}",
                OutputDataDirectory = Path.Combine(root, "Assets", "StreamingAssets", "Generated", "Tables")
            };

            service.Generate(changedOptions, changedContract);

            Assert.True(facade.AsSpan().SequenceEqual(File.ReadAllBytes(facadePath)));
            Assert.True(loader.AsSpan().SequenceEqual(File.ReadAllBytes(loaderPath)));
            Assert.Equal(stableTimestamp, File.GetLastWriteTimeUtc(facadePath));
            Assert.Equal(stableTimestamp, File.GetLastWriteTimeUtc(loaderPath));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>修改 Editor 数据路径时只更新门面的 Editor 默认值，Loader 契约保持稳定。</summary>
    [Fact]
    public void Generate_修改编辑器数据路径_只更新门面()
    {
        string root = CreateProjectRoot("unity");
        try
        {
            TableKitOptions options = CreateOptions(root, "Assets/Scripts/TableKit");
            TableKitContract contract = CreateContract(root, options.OutputCodeDir);
            TableKitCodeGenerationService service = new();
            service.Generate(options, contract);
            string facadePath = Path.Combine(contract.OutputCodeDirectory, "TableKit.cs");
            string loaderPath = Path.Combine(contract.OutputCodeDirectory, "ITableDataLoader.cs");
            byte[] originalFacade = File.ReadAllBytes(facadePath);
            byte[] originalLoader = File.ReadAllBytes(loaderPath);

            service.Generate(options with { EditorDataPath = "Assets/EditorOnly/Tables" }, contract);

            string changedFacade = ToolingFileObservation.ReadUtf8(facadePath);
            Assert.False(originalFacade.AsSpan().SequenceEqual(File.ReadAllBytes(facadePath)));
            AssertCodeLines(changedFacade, "private static string sEditorDataPath = \"Assets/EditorOnly/Tables/\";");
            AssertOmitsCodeLines(changedFacade, "private static string sEditorDataPath = \"Assets/Resources/Art/Table/\";");
            Assert.True(originalLoader.AsSpan().SequenceEqual(File.ReadAllBytes(loaderPath)));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>创建带最小 Unity 或 Godot 识别文件的隔离项目根。</summary>
    /// <param name="kind">项目类型。</param>
    /// <returns>项目根绝对路径。</returns>
    private static string CreateProjectRoot(string kind)
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-codegen-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        if (kind == "unity")
        {
            Directory.CreateDirectory(Path.Combine(root, "Assets"));
            Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
            File.WriteAllText(Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 2022.3.0f1");
        }
        else
        {
            File.WriteAllText(Path.Combine(root, "project.godot"), "[application]\nconfig/name=\"Game\"\n");
            File.WriteAllText(
                Path.Combine(root, "Game.csproj"),
                "<Project Sdk=\"Godot.NET.Sdk/4.7.0\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
        }
        return root;
    }

    /// <summary>创建代码生成测试使用的页面选项。</summary>
    /// <param name="root">项目根。</param>
    /// <param name="outputCodeDir">项目相对代码根。</param>
    /// <returns>完整 TableKit 选项。</returns>
    private static TableKitOptions CreateOptions(string root, string outputCodeDir)
    {
        return new TableKitOptions
        {
            ProjectRoot = root,
            LubanConfigPath = Path.Combine(root, "Luban", "luban.conf"),
            OutputCodeDir = outputCodeDir,
            EditorDataPath = "Assets/Resources/Art/Table",
            AssemblyName = "YokiFrame.TableKit"
        };
    }

    /// <summary>创建默认 Cfg.Tables Luban 生成契约。</summary>
    /// <param name="root">项目根。</param>
    /// <param name="outputCodeDir">项目相对代码根。</param>
    /// <returns>主 target 契约。</returns>
    private static TableKitContract CreateContract(string root, string outputCodeDir)
    {
        TableKitContract contract = new()
        {
            ConfigPath = Path.Combine(root, "Luban", "luban.conf"),
            TargetName = "client",
            TopModule = "Cfg",
            Manager = "Tables",
            CodeTarget = "cs-bin",
            DataTarget = "bin",
            DataExtension = "bytes",
            IsAddressable = false,
            RuntimePathPattern = "Art/Table/{0}",
            OutputCodeDirectory = Path.GetFullPath(Path.Combine(root, outputCodeDir)),
            OutputDataDirectory = Path.Combine(root, "Data"),
            AssemblyName = "YokiFrame.TableKit"
        };
        WriteManagerSource(contract);
        return contract;
    }

    /// <summary>写入测试使用的最小 Luban manager 原始生成形状。</summary>
    /// <param name="contract">决定命名空间、manager 名和输出根的契约。</param>
    private static void WriteManagerSource(TableKitContract contract)
    {
        string lubanRoot = Path.Combine(contract.OutputCodeDirectory, "Luban");
        Directory.CreateDirectory(lubanRoot);
        string source = "using Luban;\n"
            + "namespace " + contract.TopModule + "\n"
            + "{\n"
            + "    public partial class " + contract.Manager + "\n"
            + "    {\n"
            + "        public " + contract.Manager + "(System.Func<string, ByteBuf> loader)\n"
            + "        {\n"
            + "            _ = loader(\"config\");\n"
            + "            _ = loader(\"item\");\n"
            + "        }\n"
            + "    }\n"
            + "}\n";
        File.WriteAllText(Path.Combine(lubanRoot, contract.Manager + ".cs"), source);
    }

    /// <summary>创建一条模拟 builtin.xml 自定义 constructor 名称的外部类型映射。</summary>
    /// <returns>用于生成 helper 的 mapper 列表。</returns>
    private static IReadOnlyList<TableKitExternalTypeMapping> CreateExternalTypeMappings()
    {
        return new[]
        {
            new TableKitExternalTypeMapping
            {
                SourceTypeName = "global::Cfg.vector2",
                TargetTypeName = "UnityEngine.Vector2",
                HelperNamespace = "Cfg",
                HelperTypeName = "ConfiguredTypeMapper",
                HelperMethodName = "CreateVector2",
                MemberNames = new[] { "X", "Y" }
            }
        };
    }

    /// <summary>放入会被生成删除或保留的旧文件，用来观察输出副作用。</summary>
    /// <param name="outputRoot">代码输出目录。</param>
    private static void SeedLegacyOutputs(string outputRoot)
    {
        Directory.CreateDirectory(outputRoot);
        File.WriteAllText(Path.Combine(outputRoot, "tablekit.manifest.json"), "{}");
        File.WriteAllText(
            Path.Combine(outputRoot, "ExternalTypeUtil.cs"),
            "// <auto-generated>\n// 由 YokiFrame Workbench TableKit 直接生成，请在 External/ 中放置用户自定义转换代码。\n// </auto-generated>\n");
        File.WriteAllText(Path.Combine(outputRoot, "ProjectConversions.cs"), "// user-owned\n");
    }

    /// <summary>断言 Unity 输出清单、用户文件保留和旧 manifest 删除。</summary>
    /// <param name="outputRoot">代码输出目录。</param>
    /// <param name="files">生成服务返回的路径。</param>
    private static void AssertUnityOutputFiles(string outputRoot, IReadOnlyList<string> files)
    {
        string facadePath = Path.Combine(outputRoot, "TableKit.cs");
        string loaderPath = Path.Combine(outputRoot, "ITableDataLoader.cs");
        string asmdefPath = Path.Combine(outputRoot, "YokiFrame.TableKit.asmdef");
        string mapperPath = Path.Combine(outputRoot, "ConfiguredTypeMapper.cs");
        Assert.Contains(facadePath, files);
        Assert.Contains(loaderPath, files);
        Assert.Contains(asmdefPath, files);
        Assert.Contains(mapperPath, files);
        Assert.True(File.Exists(mapperPath));
        Assert.False(File.Exists(Path.Combine(outputRoot, "ExternalTypeUtil.cs")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "ProjectConversions.cs")));
        Assert.False(File.Exists(Path.Combine(outputRoot, "tablekit.manifest.json")));
        Assert.False(File.Exists(Path.Combine(outputRoot, "YokiFrame.TableKit.csproj")));
    }

    /// <summary>用完整代码行、标识符和字符串字面量断言 Unity 生成源码。</summary>
    /// <param name="outputRoot">代码输出目录。</param>
    private static void AssertUnityGeneratedSources(string outputRoot)
    {
        string facade = ToolingFileObservation.ReadUtf8(Path.Combine(outputRoot, "TableKit.cs"));
        string loader = ToolingFileObservation.ReadUtf8(Path.Combine(outputRoot, "ITableDataLoader.cs"));
        string mapper = ToolingFileObservation.ReadUtf8(Path.Combine(outputRoot, "ConfiguredTypeMapper.cs"));
        AssertUnityFacadeLines(facade);
        AssertOmitsIdentifiers(
            facade,
            "LubanTables",
            "UseAsyncLoading",
            "ConfigureAwait",
            "TablesTypeName",
            "CodeTarget",
            "DataTarget",
            "DataExtension",
            "TableDataLocation",
            "LogicalAddress");
        AssertOmitsStringLiterals(facade, "TableKit 未设置 ITableDataLoader");
        AssertUnityLoaderLines(loader);
        AssertConfiguredMapper(mapper);
    }

    /// <summary>断言门面设置、初始化、Loader 调用和 Editor 入口都是完整代码行。</summary>
    /// <param name="facade">生成的 TableKit.cs。</param>
    private static void AssertUnityFacadeLines(string facade)
    {
        AssertCodeLines(
            facade,
            "namespace YokiFrame",
            "private static global::Cfg.Tables sTables;",
            "#if YOKIFRAME_UNITASK_SUPPORT",
            "using Cysharp.Threading.Tasks;",
            "public static async UniTask InitAsync()",
            "public static async Task InitAsync()",
            "public static async UniTask InitAsync(string resourcePathPattern)",
            "public static async Task InitAsync(string resourcePathPattern)",
            "public static async UniTask<global::Cfg.Tables> LoadAsync()",
            "public static async Task<global::Cfg.Tables> LoadAsync()",
            "public static async UniTask<global::Cfg.Tables> LoadAsync(string resourcePathPattern)",
            "public static async Task<global::Cfg.Tables> LoadAsync(string resourcePathPattern)",
            "public static string RuntimePathPattern => KitSettings.GetString(SETTINGS_KIT, RUNTIME_PATH_PATTERN_KEY, string.Empty);",
            "public static bool UseRawResourceLoading => KitSettings.GetBool(SETTINGS_KIT, USE_RAW_RESOURCE_LOADING_KEY, true);",
            "? TableDataResourceLoadMode.Raw",
            ": TableDataResourceLoadMode.Asset;",
            "public static void Init()",
            "public static void Init(string resourcePathPattern)",
            "sTables = LoadTables(RuntimePathPattern);",
            "sTables = await LoadTablesAsync(RuntimePathPattern);",
            "if (sLoader == null) sLoader = new ResKitTableDataLoader(sTableNames);",
            "global::Cfg.Tables tables = GetLoader().Load<global::Cfg.Tables>(resourcePathPattern, ResourceLoadMode);",
            "global::Cfg.Tables tables = await GetLoader().LoadAsync<global::Cfg.Tables>(resourcePathPattern, ResourceLoadMode);",
            "\"config\",",
            "\"item\",");
        AssertCodeLines(
            facade,
            "#if UNITY_EDITOR || (GODOT && TOOLS)",
            "public static string EditorDataPath",
            "public static global::Cfg.Tables TablesEditor",
            "public static void SetEditorDataPath(string path)",
            "public static void RefreshEditor()",
            "UnityEngine.TextAsset asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(path);");
    }

    /// <summary>断言 Loader 契约、默认实现和接口约束是完整代码行。</summary>
    /// <param name="loader">生成的 ITableDataLoader.cs。</param>
    private static void AssertUnityLoaderLines(string loader)
    {
        AssertCodeLines(
            loader,
            "namespace YokiFrame",
            "public enum TableDataResourceLoadMode",
            "public interface ITableDataLoader",
            "internal sealed class ResKitTableDataLoader : ITableDataLoader",
            "? ResKit.LoadRaw(path)",
            "? await ResKit.LoadRawAsync(path)",
            "UnityEngine.TextAsset asset = ResKit.Load<UnityEngine.TextAsset>(path);",
            "UnityEngine.TextAsset asset = await ResKit.LoadAsync<UnityEngine.TextAsset>(path);",
            "return await LoadBinaryTablesAsync<TTables>(constructor, resourcePathPattern, resourceLoadMode);",
            "public TTables Load<TTables>(string resourcePathPattern, TableDataResourceLoadMode resourceLoadMode) where TTables : class",
            "public async UniTask<TTables> LoadAsync<TTables>(string resourcePathPattern, TableDataResourceLoadMode resourceLoadMode) where TTables : class",
            "public async Task<TTables> LoadAsync<TTables>(string resourcePathPattern, TableDataResourceLoadMode resourceLoadMode) where TTables : class",
            "TTables Load<TTables>(string resourcePathPattern, TableDataResourceLoadMode resourceLoadMode) where TTables : class;",
            "UniTask<TTables> LoadAsync<TTables>(string resourcePathPattern, TableDataResourceLoadMode resourceLoadMode) where TTables : class;",
            "Task<TTables> LoadAsync<TTables>(string resourcePathPattern, TableDataResourceLoadMode resourceLoadMode) where TTables : class;");
        AssertOmitsCodeLines(loader, "namespace YokiFrame.TableKit");
        AssertOmitsIdentifiers(loader, "tablesTypeName", "ITableDataLocationLoader", "TableDataLocation");
    }

    /// <summary>断言外部类型 helper 的命名空间、方法和构造调用是完整代码行。</summary>
    /// <param name="mapper">生成的 ConfiguredTypeMapper.cs。</param>
    private static void AssertConfiguredMapper(string mapper)
    {
        AssertCodeLines(
            mapper,
            "public static class ConfiguredTypeMapper",
            "namespace Cfg",
            "public static UnityEngine.Vector2 CreateVector2(global::Cfg.vector2 value)",
            "return new UnityEngine.Vector2(value.X, value.Y);");
        AssertOmitsCodeLines(mapper, "using Godot;");
    }

    /// <summary>断言 Unity asmdef 的名称和引用字段。</summary>
    /// <param name="asmdefPath">生成的 asmdef 路径。</param>
    private static void AssertUnityAssemblyDefinition(string asmdefPath)
    {
        using JsonDocument asmdef = ToolingFileObservation.OpenJson(asmdefPath);
        Assert.Equal("YokiFrame.TableKit", asmdef.RootElement.GetProperty("name").GetString());
        string[] references = asmdef.RootElement.GetProperty("references").EnumerateArray().Select(static value => value.GetString() ?? string.Empty).ToArray();
        Assert.Contains("YokiFrame", references);
        Assert.Contains("Luban.Runtime", references);
        Assert.Contains("UniTask", references);
        Assert.DoesNotContain("YokiFrame.TableKit.Contracts", references);
    }

}
