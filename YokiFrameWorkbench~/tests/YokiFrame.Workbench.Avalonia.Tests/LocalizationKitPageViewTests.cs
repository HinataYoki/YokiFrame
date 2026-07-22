using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YokiFrame.Tooling.Application.Services.LocalizationKit;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.Views;
using YokiFrame.Workbench.Avalonia.Views.Pages;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 LocalizationKit 三栏诊断布局、默认选择和最小窗口可读性。</summary>
public sealed class LocalizationKitPageViewTests
{
    /// <summary>验证目录加载后默认选择首项，并生成语言对照和目录级覆盖率。</summary>
    [Fact]
    public async Task RefreshSelectsFirstEntryAndBuildsCoverage()
    {
        using TemporaryLocalizationProject project = TemporaryLocalizationProject.Create();
        LocalizationKitPageViewModel viewModel = new(project.Root, new LocalizationKitApplicationService());

        await viewModel.RefreshAsync();

        Assert.Equal(4, viewModel.Entries.Count);
        Assert.Equal("menu.start", viewModel.SelectedEntry?.Key);
        Assert.Equal(3, viewModel.SelectedValueRows.Count);
        Assert.Equal(3, viewModel.LanguageCoverage.Count);
        LocalizationLanguageCoverageViewModel japanese = Assert.Single(
            viewModel.LanguageCoverage, static item => item.Language == "Japanese");
        Assert.Equal("3 / 4", japanese.CoverageText);
        Assert.Equal(75d, japanese.CoveragePercent);
    }

    /// <summary>验证自动加载失败后周期页面更新保持稳定，只有显式刷新才重新读取目录。</summary>
    [Fact]
    public async Task EnsureLoadedKeepsFailureStableUntilExplicitRefresh()
    {
        using TemporaryLocalizationProject project = TemporaryLocalizationProject.Create();
        string catalogPath = Path.Combine(project.Root, "Assets", "Settings", "YokiFrame", "localization.json");
        string catalogJson = File.ReadAllText(catalogPath);
        File.Delete(catalogPath);
        LocalizationKitPageViewModel viewModel = new(project.Root, new LocalizationKitApplicationService());

        await viewModel.EnsureLoadedAsync();
        Assert.True(viewModel.HasLoadError);
        string failureStatus = viewModel.StatusText;
        File.WriteAllText(catalogPath, catalogJson);

        await viewModel.EnsureLoadedAsync();

        Assert.Equal(failureStatus, viewModel.StatusText);
        Assert.Empty(viewModel.Entries);
        await viewModel.RefreshAsync();
        Assert.Equal(4, viewModel.Entries.Count);
        Assert.False(viewModel.HasLoadError);
    }

    /// <summary>验证语言下拉筛选不会因重建选项集合而重入刷新或重复投影条目。</summary>
    [Fact]
    public async Task LanguageFilterKeepsUniqueEntriesDuringOptionRefresh()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        using TemporaryLocalizationProject project = TemporaryLocalizationProject.Create();
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            LocalizationKitPageViewModel viewModel = new(project.Root, new LocalizationKitApplicationService());
            await viewModel.RefreshAsync();
            LocalizationKitPageView view = new() { DataContext = viewModel };
            Window window = new() { Width = 1000, Height = 680, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                ComboBox languageFilter = view.FindControl<ComboBox>("LocalizationLanguageFilter")!;
                languageFilter.SelectedItem = "Japanese";
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("Japanese", viewModel.SelectedLanguage);
                Assert.Equal(3, viewModel.Entries.Count);
                Assert.Equal(viewModel.Entries.Count, viewModel.Entries.Select(static item => item.Id).Distinct().Count());

                languageFilter.SelectedItem = "English";
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("English", viewModel.SelectedLanguage);
                Assert.Equal(4, viewModel.Entries.Count);
                Assert.Equal(viewModel.Entries.Count, viewModel.Entries.Select(static item => item.Id).Distinct().Count());
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>验证直接页面在紧凑和宽屏内容区都保留三栏并把弹性宽度交给语言对照。</summary>
    [Theory]
    [InlineData(1000, 680)]
    [InlineData(1400, 900)]
    public async Task PageKeepsReadableThreePaneLayout(double width, double height)
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        using TemporaryLocalizationProject project = TemporaryLocalizationProject.Create();
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            LocalizationKitPageViewModel viewModel = new(project.Root, new LocalizationKitApplicationService());
            await viewModel.RefreshAsync();
            LocalizationKitPageView view = new() { DataContext = viewModel };
            Window window = new() { Width = width, Height = height, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Grid workspace = view.FindControl<Grid>("LocalizationWorkspace")!;
                Assert.NotNull(workspace);
                Assert.True(workspace.ColumnDefinitions[1].ActualWidth > workspace.ColumnDefinitions[0].ActualWidth);
                Assert.True(workspace.ColumnDefinitions[1].ActualWidth > workspace.ColumnDefinitions[2].ActualWidth);
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "条目索引");
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "语言对照");
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "语言覆盖");
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "语言筛选");
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "Luban 工作目录");
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "Excel 目录");
                Assert.NotNull(view.FindControl<TextBox>("LocalizationLubanWorkDir"));
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "Start Game");
                Assert.NotNull(viewModel.SelectedEntry);
                Assert.Equal(3, view.GetVisualDescendants().OfType<ProgressBar>().Count(static item => item.IsVisible));
                AssertNoVisibleHorizontalScrollBar(view);
                AssertMinimumTextSize(view);
                SaveFrame(window, width, height, "page");
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>验证完整 Workbench 在最低和宽屏尺寸下都完整展示 LocalizationKit 三栏。</summary>
    [Theory]
    [InlineData(1280, 820)]
    [InlineData(1700, 1060)]
    public async Task ShellKeepsLocalizationWorkspaceVisible(double width, double height)
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        using TemporaryLocalizationProject project = TemporaryLocalizationProject.Create();
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            WorkbenchShellViewModel viewModel = new(() => { }, _ => { }, (_, _) => Task.CompletedTask);
            viewModel.LocalizationKitPage.SetProjectRoot(project.Root);
            viewModel.SelectedPage = "LocalizationKit";
            await viewModel.LocalizationKitPage.RefreshAsync();
            Window window = new() { Width = width, Height = height, Content = new WorkbenchShellView(viewModel) };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                LocalizationKitPageView page = window.GetVisualDescendants().OfType<LocalizationKitPageView>().Single();
                Assert.Contains(page.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "menu.start");
                Assert.Contains(page.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "3 / 4");
                AssertNoVisibleHorizontalScrollBar(page);
                AssertMinimumTextSize(page);
                SaveFrame(window, width, height, "shell");
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>验证关键字筛选复用已加载目录，不因输入变化再次读取磁盘。</summary>
    [Fact]
    public async Task FiltersReuseLoadedCatalogWithoutReadingSourceAgain()
    {
        using TemporaryLocalizationProject project = TemporaryLocalizationProject.Create();
        LocalizationKitPageViewModel viewModel = new(project.Root, new LocalizationKitApplicationService());
        await viewModel.RefreshAsync();
        File.WriteAllText(Path.Combine(project.Root, "Assets", "Settings", "YokiFrame", "localization.json"), "{}");

        viewModel.SearchText = "menu.start";

        Assert.False(viewModel.HasLoadError);
        Assert.Single(viewModel.Entries);
        Assert.Equal("menu.start", viewModel.Entries[0].Key);
    }

    /// <summary>选择工作目录后应持久化覆盖项，并可通过宿主回调打开已创建的 Excel 作者目录。</summary>
    [Fact]
    public async Task LubanWorkspaceCommandsPersistSelectionAndOpenWorkbookDirectory()
    {
        using TemporaryLocalizationProject project = TemporaryLocalizationProject.Create();
        string workDirectory = Path.Combine(project.Root, "Luban", "MiniTemplate");
        Directory.CreateDirectory(Path.Combine(workDirectory, "Defines"));
        Directory.CreateDirectory(Path.Combine(workDirectory, "Datas"));
        Directory.CreateDirectory(Path.Combine(project.Root, "Luban", "Tools", "Luban"));
        File.WriteAllText(
            Path.Combine(workDirectory, "luban.conf"),
            "{\"dataDir\":\"Datas\",\"schemaFiles\":[{\"fileName\":\"Defines\",\"type\":\"\"}],\"targets\":[{\"name\":\"client\"}]}" );
        File.WriteAllText(Path.Combine(project.Root, "Luban", "Tools", "Luban", "Luban.dll"), string.Empty);
        LocalizationKitApplicationService service = new();
        Assert.True(service.GenerateLubanTemplate(new()
        {
            ProjectRoot = project.Root,
            LubanWorkDir = Path.Combine("Luban", "MiniTemplate")
        }).Succeeded);
        string? openedDirectory = null;
        LocalizationKitPageViewModel viewModel = new(
            project.Root,
            service,
            new FixedFolderPicker(workDirectory),
            path =>
            {
                openedDirectory = path;
                return Task.CompletedTask;
            });

        await viewModel.BrowseLubanWorkDirCommand.ExecuteAsync();
        await viewModel.OpenExcelDirectoryCommand.ExecuteAsync();

        Assert.Equal(Path.Combine("Luban", "MiniTemplate"), viewModel.LubanWorkDir);
        Assert.Equal(Path.Combine(workDirectory, "Datas", "LocalizationKit"), openedDirectory);
        Assert.Equal(
            Path.Combine("Luban", "MiniTemplate"),
            new LocalizationKitSettingsService().Load(project.Root).LubanWorkDir);
    }

    /// <summary>断言页面没有可见横向滚动条。</summary>
    /// <param name="root">待检查的页面根控件。</param>
    private static void AssertNoVisibleHorizontalScrollBar(Control root)
    {
        ScrollBar[] horizontalScrollBars = root.GetVisualDescendants()
            .OfType<ScrollBar>()
            .Where(static item => item.IsVisible && item.Orientation == Orientation.Horizontal)
            .ToArray();
        Assert.Empty(horizontalScrollBars);
    }

    /// <summary>断言 LocalizationKit 所有可见文字遵守 12px 最小字号。</summary>
    /// <param name="root">待检查的页面根控件。</param>
    private static void AssertMinimumTextSize(Control root)
    {
        TextBlock[] visibleText = root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(static item => item.IsVisible && item.Bounds.Width > 0d && item.Bounds.Height > 0d)
            .ToArray();
        Assert.All(visibleText, static item => Assert.True(item.FontSize >= 12d, item.Text));
    }

    /// <summary>保存 LocalizationKit Headless 截图并拒绝空白输出。</summary>
    /// <param name="window">已完成布局的测试窗口。</param>
    /// <param name="width">截图宽度。</param>
    /// <param name="height">截图高度。</param>
    /// <param name="stateName">截图状态名。</param>
    private static void SaveFrame(Window window, double width, double height, string stateName)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        string outputDirectory = Path.Combine(
            WorkbenchContractTestFiles.FindWorkbenchRoot(), ".artifacts", "screenshots", "workbench");
        Directory.CreateDirectory(outputDirectory);
        string fileName = "localizationkit-" + stateName + "-" + (int)width + "x" + (int)height + ".png";
        using FileStream stream = new(
            Path.Combine(outputDirectory, fileName), FileMode.Create, FileAccess.Write, FileShare.Read);
        frame.Save(stream);
        Assert.True(stream.Length > 1024, "LocalizationKit Headless 截图内容为空或异常小。");
    }

    /// <summary>提供固定目录选择结果，使页面命令测试不依赖桌面原生对话框。</summary>
    private sealed class FixedFolderPicker : IInstallerFolderPicker
    {
        private readonly string mPath;

        /// <summary>创建始终返回指定目录的测试选择器。</summary>
        /// <param name="path">模拟用户选择的绝对目录。</param>
        public FixedFolderPicker(string path) => mPath = path;

        /// <summary>返回预设目录，不显示实际原生对话框。</summary>
        /// <param name="title">不会被使用的对话框标题。</param>
        /// <param name="cancellationToken">调用方取消令牌。</param>
        /// <param name="suggestedPath">不会被使用的建议起点。</param>
        /// <returns>预设的项目内工作目录。</returns>
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default, string? suggestedPath = null)
        {
            return Task.FromResult<string?>(mPath);
        }
    }

    /// <summary>提供包含普通文本、复数配置和缺失语言的临时项目。</summary>
    private sealed class TemporaryLocalizationProject : IDisposable
    {
        /// <summary>创建绑定临时项目根的测试对象。</summary>
        /// <param name="root">临时项目根目录。</param>
        private TemporaryLocalizationProject(string root) => Root = root;

        /// <summary>临时项目根目录。</summary>
        public string Root { get; }

        /// <summary>创建可覆盖完整、缺失和复数三种状态的本地化目录。</summary>
        /// <returns>已经写入 localization.json 的临时项目。</returns>
        public static TemporaryLocalizationProject Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "yokiframe-localization-view-" + Guid.NewGuid().ToString("N"));
            string settingsDirectory = Path.Combine(root, "Assets", "Settings", "YokiFrame");
            Directory.CreateDirectory(settingsDirectory);
            File.WriteAllText(
                Path.Combine(settingsDirectory, "localization.json"),
                JsonSerializer.Serialize(CreateDocument(), new JsonSerializerOptions { WriteIndented = true }));
            return new TemporaryLocalizationProject(root);
        }

        /// <summary>释放测试项目目录。</summary>
        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }

        /// <summary>创建用于截图的四条本地化示例数据。</summary>
        /// <returns>可由 LocalizationKit Application 读取的匿名 JSON 文档。</returns>
        private static object CreateDocument()
        {
            string[] languages = ["ChineseSimplified", "English", "Japanese"];
            return new
            {
                formatVersion = 1,
                languages = languages.Select(static id => new { id }).ToArray(),
                texts = new object[]
                {
                    new { id = 1000, key = "menu.start", values = Values("开始游戏", "Start Game", "ゲーム開始") },
                    new { id = 1001, key = "menu.settings", values = Values("设置", "Settings", null) },
                    new { id = 1002, key = "menu.exit", values = Values("退出", "Exit", "終了") },
                    new
                    {
                        id = 2000,
                        key = "inventory.items",
                        plural = new Dictionary<string, Dictionary<string, string>>
                        {
                            ["ChineseSimplified"] = new() { ["Other"] = "{0} 个物品" },
                            ["English"] = new() { ["One"] = "{0} item", ["Other"] = "{0} items" },
                            ["Japanese"] = new() { ["Other"] = "{0} 個のアイテム" }
                        }
                    }
                }
            };
        }

        /// <summary>按固定语言顺序创建普通文本映射，忽略空值。</summary>
        /// <param name="chinese">简体中文译文。</param>
        /// <param name="english">英文译文。</param>
        /// <param name="japanese">日文译文。</param>
        /// <returns>只包含已配置语言的文本字典。</returns>
        private static Dictionary<string, string> Values(string chinese, string english, string? japanese)
        {
            Dictionary<string, string> values = new()
            {
                ["ChineseSimplified"] = chinese,
                ["English"] = english
            };
            if (!string.IsNullOrWhiteSpace(japanese))
            {
                values["Japanese"] = japanese;
            }
            return values;
        }
    }
}
