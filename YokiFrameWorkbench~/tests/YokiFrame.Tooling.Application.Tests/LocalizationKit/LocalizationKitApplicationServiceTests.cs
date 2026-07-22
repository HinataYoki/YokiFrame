using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using YokiFrame;
using YokiFrame.Tooling.Application.Models.LocalizationKit;
using YokiFrame.Tooling.Application.Models.Luban;
using YokiFrame.Tooling.Application.Services.LocalizationKit;

namespace YokiFrame.Tooling.Application.Tests.LocalizationKit;

/// <summary>验证 LocalizationKit Application 目录、缺失、写入和模板用例。</summary>
public sealed class LocalizationKitApplicationServiceTests
{
    /// <summary>搜索应同时匹配 ID、语言值并返回缺失诊断。</summary>
    [Fact]
    public void SearchAndCheckBuildCatalog()
    {
        using TemporaryProject project = TemporaryProject.Create();
        LocalizationKitApplicationService service = new();
        LocalizationOperationResult search = service.Search(new LocalizationSearchRequest { Options = project.Options, Keyword = "开始" });
        Assert.True(search.Succeeded);
        Assert.Single(search.Entries);
        Assert.Equal(1, search.Catalog!.MissingEntryCount);
        Assert.Single(service.Check(project.Options).Entries);
    }

    /// <summary>补充值应写入 JSON 且已有值默认拒绝覆盖。</summary>
    [Fact]
    public void AddWritesAtomicallyAndRejectsAccidentalOverwrite()
    {
        using TemporaryProject project = TemporaryProject.Create();
        LocalizationKitApplicationService service = new();
        LocalizationOperationResult add = service.Add(new LocalizationAddRequest { Options = project.Options, TextId = 1, Language = "English", Value = "Start" });
        Assert.True(add.Succeeded, string.Join("; ", add.Diagnostics));
        Assert.False(service.Add(new LocalizationAddRequest { Options = project.Options, TextId = 1, Language = "English", Value = "Override" }).Succeeded);
        Assert.Contains("Start", File.ReadAllText(project.SourcePath));
    }

    /// <summary>无效复数分类必须在写入前失败，避免 Tooling 生成 Runtime 无法加载的 JSON。</summary>
    [Fact]
    public void AddRejectsInvalidPluralCategoryBeforeWriting()
    {
        using TemporaryProject project = TemporaryProject.Create();
        string original = File.ReadAllText(project.SourcePath);

        LocalizationOperationResult result = new LocalizationKitApplicationService().Add(new LocalizationAddRequest
        {
            Options = project.Options,
            TextId = 1,
            Language = "English",
            Value = "Start",
            PluralCategory = "Single"
        });

        Assert.False(result.Succeeded);
        Assert.Equal(original, File.ReadAllText(project.SourcePath));
    }

    /// <summary>Tooling 应规范化枚举名称，避免同一语言因大小写差异产生重复 JSON 键。</summary>
    [Fact]
    public void AddNormalizesLanguageIdentifier()
    {
        using TemporaryProject project = TemporaryProject.Create();

        LocalizationOperationResult result = new LocalizationKitApplicationService().Add(new LocalizationAddRequest
        {
            Options = project.Options,
            TextId = 1,
            Language = "english",
            Value = "Start"
        });

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(project.SourcePath));
        JsonElement values = document.RootElement.GetProperty("texts")[0].GetProperty("values");
        Assert.True(values.TryGetProperty("English", out JsonElement english));
        Assert.Equal("Start", english.GetString());
        Assert.False(values.TryGetProperty("english", out _));
    }

    /// <summary>Luban 模板生成应输出已注册的 XML schema 与单一 Localization 工作表。</summary>
    [Fact]
    public void GenerateLubanTemplateWritesSchemaAndWorkbook()
    {
        using TemporaryProject project = TemporaryProject.CreateLubanProject();
        LocalizationOperationResult result = new LocalizationKitApplicationService().GenerateLubanTemplate(new LocalizationLubanTemplateRequest
        {
            ProjectRoot = project.Root,
            Languages = new[] { "ChineseSimplified", "English" }
        });

        Assert.True(result.Succeeded);
        string schema = Path.Combine(project.Root, "Luban", "MiniTemplate", "Defines", "LocalizationKit.xml");
        string workbook = Path.Combine(project.Root, "Luban", "MiniTemplate", "Datas", "LocalizationKit", "LocalizationKit.xlsx");
        Assert.True(File.Exists(schema));
        string schemaContent = File.ReadAllText(schema);
        Assert.Contains("LocalizationEntry", schemaContent);
        Assert.Contains("name=\"LocalizationEntryTable\"", schemaContent);
        Assert.Contains("index=\"id\"", schemaContent);
        Assert.Contains("name=\"LocalizationValueKind\"", schemaContent);
        Assert.Contains("name=\"variants\"", schemaContent);
        Assert.Contains("map,LocalizationValueKind,LocalizationTranslations", schemaContent);
        Assert.DoesNotContain("id+pluralCategory", schemaContent);
        Assert.DoesNotContain("LocalizationLanguageTable", schemaContent);
        Assert.DoesNotContain("LocalizationTextTable", schemaContent);
        Assert.DoesNotContain("LocalizationPluralTable", schemaContent);
        using ZipArchive archive = ZipFile.OpenRead(workbook);
        ZipArchiveEntry? workbookEntry = archive.GetEntry("xl/workbook.xml");
        Assert.NotNull(workbookEntry);
        using StreamReader workbookReader = new(workbookEntry!.Open());
        Assert.Contains("sheet name=\"Localization\"", workbookReader.ReadToEnd());
        ZipArchiveEntry? sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheetEntry);
        using StreamReader sheetReader = new(sheetEntry!.Open());
        string sheetContent = sheetReader.ReadToEnd();
        Assert.Contains("*variants", sheetContent);
        Assert.Contains("$key", sheetContent);
        Assert.Single(Regex.Matches(sheetContent, ">2000<").Cast<Match>());
        Assert.Null(archive.GetEntry("xl/worksheets/sheet2.xml"));
        Assert.Null(archive.GetEntry("xl/worksheets/sheet3.xml"));
    }

    /// <summary>模板应提供完整的本地化样例，并通过样式资源表达字段层级和作者输入区域。</summary>
    [Fact]
    public void GenerateLubanTemplateWritesStyledComprehensiveExamples()
    {
        using TemporaryProject project = TemporaryProject.CreateLubanProject();
        LocalizationOperationResult result = new LocalizationKitApplicationService().GenerateLubanTemplate(new LocalizationLubanTemplateRequest
        {
            ProjectRoot = project.Root,
            Languages = new[] { "ChineseSimplified", "English" }
        });

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        string workbook = Path.Combine(project.Root, "Luban", "MiniTemplate", "Datas", "LocalizationKit", "LocalizationKit.xlsx");
        using ZipArchive archive = ZipFile.OpenRead(workbook);
        ZipArchiveEntry? stylesEntry = archive.GetEntry("xl/styles.xml");
        ZipArchiveEntry? relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        ZipArchiveEntry? sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(stylesEntry);
        Assert.NotNull(relationshipsEntry);
        Assert.NotNull(sheetEntry);
        using StreamReader stylesReader = new(stylesEntry!.Open());
        using StreamReader relationshipsReader = new(relationshipsEntry!.Open());
        using StreamReader sheetReader = new(sheetEntry!.Open());
        string stylesContent = stylesReader.ReadToEnd();
        string relationshipsContent = relationshipsReader.ReadToEnd();
        string sheetContent = sheetReader.ReadToEnd();
        Assert.Contains("FFC6EFCE", stylesContent);
        Assert.Contains("FFFFC7CE", stylesContent);
        Assert.Contains("horizontal=\"left\"", stylesContent);
        Assert.Contains("horizontal=\"center\"", stylesContent);
        Assert.Contains("relationships/styles", relationshipsContent);
        Assert.Contains("<pane ySplit=\"4\"", sheetContent);
        Assert.Contains("<cols>", sheetContent);
        Assert.Contains("player.greeting", sheetContent);
        Assert.Contains("inventory.summary", sheetContent);
        Assert.Single(Regex.Matches(sheetContent, ">2000<").Cast<Match>());
        Assert.Single(Regex.Matches(sheetContent, ">3000<").Cast<Match>());
        foreach (string valueKind in new[] { "Zero", "One", "Two", "Few", "Many", "Other" })
        {
            Assert.Contains(">" + valueKind + "<", sheetContent);
        }
    }

    /// <summary>显式 Luban 工作目录应定位同一配置下的 LocalizationKit Excel 作者目录。</summary>
    [Fact]
    public void ResolveLubanWorkspaceUsesConfiguredWorkDirectory()
    {
        using TemporaryProject project = TemporaryProject.CreateLubanProject();
        string workbookDirectory = Path.Combine(project.Root, "Luban", "MiniTemplate", "Datas", "LocalizationKit");
        Directory.CreateDirectory(workbookDirectory);

        LocalizationLubanWorkspaceResult result = new LocalizationKitApplicationService()
            .ResolveLubanWorkspace(project.Root, Path.Combine("Luban", "MiniTemplate"));

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        Assert.Equal(Path.Combine(project.Root, "Luban", "MiniTemplate"), result.WorkDirectory);
        Assert.Equal(workbookDirectory, result.WorkbookDirectory);
        Assert.Equal(Path.Combine(workbookDirectory, "LocalizationKit.xlsx"), result.WorkbookPath);
    }

    /// <summary>用户显式配置不存在的工作目录时，不得悄悄退回 standalone JSON。</summary>
    [Fact]
    public async Task PreferredLoadDoesNotFallbackWhenConfiguredWorkDirectoryIsInvalid()
    {
        using TemporaryProject project = TemporaryProject.Create();

        LocalizationOperationResult result = await new LocalizationKitApplicationService()
            .LoadPreferredAsync(project.Root, "localization.json", "Luban/MissingTemplate");

        Assert.False(result.Succeeded);
        Assert.Equal("Luban", result.Provider);
    }

    /// <summary>已注册的 Luban schema 缺少工具时不得退回 standalone JSON，以免 Workbench 显示过期数据源。</summary>
    [Fact]
    public async Task PreferredLoadKeepsLubanFailureWhenRegisteredSchemaCannotRun()
    {
        using TemporaryProject project = TemporaryProject.CreateLubanProject();
        LocalizationKitApplicationService service = new();
        LocalizationOperationResult template = service.GenerateLubanTemplate(new LocalizationLubanTemplateRequest
        {
            ProjectRoot = project.Root
        });
        Assert.True(template.Succeeded, string.Join("; ", template.Diagnostics));
        File.Delete(Path.Combine(project.Root, "Luban", "Tools", "Luban", "Luban.dll"));

        LocalizationOperationResult result = await service.LoadPreferredAsync(project.Root, "localization.json");

        Assert.False(result.Succeeded);
        Assert.Equal("Luban", result.Provider);
    }

    /// <summary>Luban 的单一 Localization 预览表应投影为与 standalone JSON 一致的目录模型。</summary>
    [Fact]
    public void ParseLubanCatalogBuildsTextsAndPlurals()
    {
        LubanJsonPreviewTable[] tables =
        {
            new LubanJsonPreviewTable
            {
                Name = "localization_entry_table",
                Count = 2,
                PreviewJson = "[{\"id\":1000,\"key\":\"menu.start\",\"variants\":{\"Text\":{\"ChineseSimplified\":\"开始\",\"English\":\"Start\"}}},{\"id\":2000,\"key\":\"inventory.item\",\"variants\":{\"One\":{\"ChineseSimplified\":\"\",\"English\":\"{0} item\"},\"Other\":{\"ChineseSimplified\":\"{0} 个物品\",\"English\":\"{0} items\"}}}]"
            }
        };

        LocalizationCatalog catalog = LocalizationKitApplicationService.ParseLubanCatalog(tables, "LocalizationKit.xlsx");

        Assert.Equal("Luban", catalog.Provider);
        Assert.Equal(2, catalog.Languages.Count);
        Assert.Equal(2, catalog.Entries.Count);
        LocalizationEntryRecord text = Assert.Single(catalog.Entries, static entry => entry.Id == 1000);
        Assert.Equal("Start", text.Values["English"]);
        LocalizationEntryRecord plural = Assert.Single(catalog.Entries, static entry => entry.Id == 2000);
        Assert.True(plural.HasPlural);
        Assert.Equal("{0} items", plural.PluralValues["English"]["Other"]);
    }

    /// <summary>Luban JSON 即使来自非标准 target，也必须拒绝重复主键，避免 Workbench 静默覆盖同一文本记录。</summary>
    [Fact]
    public void ParseLubanCatalogRejectsDuplicatePrimaryIds()
    {
        LubanJsonPreviewTable[] tables =
        {
            new LubanJsonPreviewTable
            {
                Name = "localization_entry_table",
                Count = 2,
                PreviewJson = "[{\"id\":2000,\"variants\":{\"One\":{\"English\":\"{0} item\"}}},{\"id\":2000,\"variants\":{\"Other\":{\"English\":\"{0} items\"}}}]"
            }
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => LocalizationKitApplicationService.ParseLubanCatalog(tables, "LocalizationKit.xlsx"));

        Assert.Contains("id 重复", exception.Message);
    }

    /// <summary>当 JSON target 将 Luban enum 导出为数字 map key 时，Text 与复数分类仍应正确投影。</summary>
    [Fact]
    public void ParseLubanCatalogAcceptsNumericVariantKeys()
    {
        LubanJsonPreviewTable[] tables =
        {
            new LubanJsonPreviewTable
            {
                Name = "localization_entry_table",
                Count = 1,
                PreviewJson = "[{\"id\":2000,\"variants\":{\"2\":{\"English\":\"{0} item\"},\"6\":{\"English\":\"{0} items\"}}}]"
            }
        };

        LocalizationCatalog catalog = LocalizationKitApplicationService.ParseLubanCatalog(tables, "LocalizationKit.xlsx");

        LocalizationEntryRecord entry = Assert.Single(catalog.Entries);
        Assert.Equal("{0} item", entry.PluralValues["English"]["One"]);
        Assert.Equal("{0} items", entry.PluralValues["English"]["Other"]);
    }

    /// <summary>数值和名称形式指向同一复数枚举键时必须失败，避免 map 数据被静默覆盖。</summary>
    [Fact]
    public void ParseLubanCatalogRejectsEquivalentVariantKeys()
    {
        LubanJsonPreviewTable[] tables =
        {
            new LubanJsonPreviewTable
            {
                Name = "localization_entry_table",
                Count = 1,
                PreviewJson = "[{\"id\":2000,\"variants\":[[2,{\"English\":\"{0} item\"}],[\"One\",{\"English\":\"{0} duplicate item\"}]]}]"
            }
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => LocalizationKitApplicationService.ParseLubanCatalog(tables, "LocalizationKit.xlsx"));

        Assert.Contains("映射键重复", exception.Message);
    }

    /// <summary>真实 Luban JSON target 将 map 导出为键值对数组时，普通与复数枚举键仍应正确投影。</summary>
    [Fact]
    public void ParseLubanCatalogAcceptsStandardLubanMapEntries()
    {
        LubanJsonPreviewTable[] tables =
        {
            new LubanJsonPreviewTable
            {
                Name = "localization_entry_table",
                Count = 2,
                PreviewJson = "[{\"id\":1000,\"key\":\"menu.start\",\"variants\":[[0,{\"ChineseSimplified\":\"开始游戏\",\"English\":\"Start Game\"}]]},{\"id\":2000,\"key\":\"inventory.item\",\"variants\":[[2,{\"ChineseSimplified\":\"\",\"English\":\"{0} item\"}],[6,{\"ChineseSimplified\":\"{0} 个物品\",\"English\":\"{0} items\"}]]}]"
            }
        };

        LocalizationCatalog catalog = LocalizationKitApplicationService.ParseLubanCatalog(tables, "LocalizationKit.xlsx");

        Assert.Equal(new[] { "ChineseSimplified", "English" }, catalog.Languages.Select(static language => language.Id));
        Assert.Equal("Start Game", Assert.Single(catalog.Entries, static entry => entry.Id == 1000).Values["English"]);
        LocalizationEntryRecord plural = Assert.Single(catalog.Entries, static entry => entry.Id == 2000);
        Assert.Equal("{0} item", plural.PluralValues["English"]["One"]);
        Assert.Equal("{0} items", plural.PluralValues["English"]["Other"]);
    }

    /// <summary>单表暂时没有数据行时，XML schema 声明的语言列仍应保留在目录中。</summary>
    [Fact]
    public void ParseLubanCatalogKeepsSchemaLanguagesForEmptyEntryTable()
    {
        LubanJsonPreviewTable[] tables =
        {
            new LubanJsonPreviewTable
            {
                Name = "localization_entry_table",
                Count = 0,
                PreviewJson = "[]"
            }
        };

        LocalizationCatalog catalog = LocalizationKitApplicationService.ParseLubanCatalog(
            tables,
            "LocalizationKit.xlsx",
            schemaLanguages: new[] { LanguageId.ChineseSimplified, LanguageId.English });

        Assert.Empty(catalog.Entries);
        Assert.Equal(new[] { "ChineseSimplified", "English" }, catalog.Languages.Select(static language => language.Id));
    }

    /// <summary>创建带最小 schema 的临时项目。</summary>
    private sealed class TemporaryProject : IDisposable
    {
        private TemporaryProject(string root)
        {
            Root = root;
            SourcePath = Path.Combine(root, "localization.json");
            Options = new LocalizationKitOptions { ProjectRoot = root, SourcePath = "localization.json" };
        }

        public string Root { get; }
        public string SourcePath { get; }
        public LocalizationKitOptions Options { get; }

        public static TemporaryProject Create()
        {
            TemporaryProject project = new(Path.Combine(Path.GetTempPath(), "yokiframe-localization-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(project.Root);
            File.WriteAllText(project.SourcePath, JsonSerializer.Serialize(new
            {
                formatVersion = 1,
                languages = new[] { new { id = "ChineseSimplified" }, new { id = "English" } },
                texts = new[] { new { id = 1, key = "start", values = new Dictionary<string, string> { ["ChineseSimplified"] = "开始" } } }
            }, new JsonSerializerOptions { WriteIndented = true }));
            return project;
        }

        /// <summary>创建包含唯一 Luban 配置、schema 目录和工具占位文件的临时项目。</summary>
        /// <returns>可验证模板发现和写入的临时项目。</returns>
        public static TemporaryProject CreateLubanProject()
        {
            TemporaryProject project = Create();
            string lubanRoot = Path.Combine(project.Root, "Luban");
            string templateRoot = Path.Combine(lubanRoot, "MiniTemplate");
            Directory.CreateDirectory(Path.Combine(templateRoot, "Defines"));
            Directory.CreateDirectory(Path.Combine(templateRoot, "Datas"));
            Directory.CreateDirectory(Path.Combine(lubanRoot, "Tools", "Luban"));
            File.WriteAllText(Path.Combine(lubanRoot, "Tools", "Luban", "Luban.dll"), "test");
            File.WriteAllText(Path.Combine(templateRoot, "luban.conf"), "{\"dataDir\":\"Datas\",\"schemaFiles\":[{\"fileName\":\"Defines\",\"type\":\"\"}],\"targets\":[{\"name\":\"client\"}]}");
            return project;
        }

        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
