using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Tooling.Application.Services.TableKit;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views;
using YokiFrame.Workbench.Avalonia.Views.Pages;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 TableKit 配置、三级数据浏览和控制台抽屉的真实 Avalonia 布局。</summary>
public sealed class TableKitPageViewTests
{
    /// <summary>验证主输出和新建额外输出都有明确的默认 target。</summary>
    [Fact]
    public void OutputTargetsHaveDefaults()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-targets-" + Guid.NewGuid().ToString("N"));
        TableKitPageViewModel viewModel = new(root, new TableKitApplicationService());
        viewModel.AddExtraOutputCommand.Execute(null);
        Assert.Equal("client", viewModel.TargetName);
        Assert.False(viewModel.IsAddressable);
        TableKitExtraOutputViewModel extra = Assert.Single(viewModel.ExtraOutputTargets);
        Assert.Equal("server", extra.TargetName);
        Assert.Equal("java-json", extra.CodeTarget);
        Assert.Equal("json", extra.DataTarget);
        Assert.Equal("Temp/LubanExtra/server/code", extra.OutputCodeDir);
    }

    /// <summary>验证非自定义编辑器数据路径实时跟随数据输出，并在关闭自定义时重新推断。</summary>
    [Fact]
    public void EditorDataPathFollowsOutputDataUntilCustomized()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-editor-data-" + Guid.NewGuid().ToString("N"));
        TableKitPageViewModel viewModel = new(root, new TableKitApplicationService());
        List<string?> changedProperties = new();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.OutputDataDir = "Assets/Art/Table";

        Assert.Equal("Assets/Art/Table", viewModel.EditorDataPath);
        Assert.Contains(nameof(TableKitPageViewModel.EditorDataPath), changedProperties);

        viewModel.CustomEditorDataPath = true;
        viewModel.EditorDataPath = "Assets/Editor/Table";
        viewModel.OutputDataDir = "Assets/Generated/Table";
        Assert.Equal("Assets/Editor/Table", viewModel.EditorDataPath);

        viewModel.CustomEditorDataPath = false;
        Assert.Equal("Assets/Generated/Table", viewModel.EditorDataPath);
    }

    /// <summary>验证历史绝对路径以项目相对形式显示，且四个主路径字段禁止键盘输入。</summary>
    [Fact]
    public async Task MainPathsAreRelativeAndReadOnly()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        string root = CreateConfiguredProject();
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                TableKitPageViewModel viewModel = new(root, new TableKitApplicationService());
                TableKitConfigurationView view = new() { DataContext = viewModel };
                Window window = new() { Width = 1000, Height = 680, Content = view };
                try
                {
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal("Luban/MiniTemplate", viewModel.LubanWorkDir);
                    Assert.Equal("Luban/Tools/Luban/Luban.dll", viewModel.LubanExecutablePath);
                    Assert.Equal("Assets/Resources/Art/Table", viewModel.OutputDataDir);
                    Assert.Equal("Assets/Scripts/TableKit", viewModel.OutputCodeDir);
                    Assert.Equal("client", viewModel.TargetName);
                    ToggleSwitch addressable = FindNamedDescendant<ToggleSwitch>(view, "TableKitAddressableToggle")!;
                    TextBox runtimePath = FindNamedDescendant<TextBox>(view, "TableKitRuntimePathPattern")!;
                    Assert.False(addressable.IsChecked);
                    Assert.Equal("Art/Table/{0}", runtimePath.Text);
                    Assert.True(FindNamedDescendant<Grid>(view, "TableKitRuntimePathRow")!.IsVisible);
                    viewModel.IsAddressable = true;
                    Dispatcher.UIThread.RunJobs();
                    Assert.False(FindNamedDescendant<Grid>(view, "TableKitRuntimePathRow")!.IsVisible);
                    Assert.True(FindNamedDescendant<TextBox>(view, "TableKitLubanWorkDir")!.IsReadOnly);
                    Assert.True(FindNamedDescendant<TextBox>(view, "TableKitLubanExecutablePath")!.IsReadOnly);
                    Assert.True(FindNamedDescendant<TextBox>(view, "TableKitOutputDataDir")!.IsReadOnly);
                    Assert.True(FindNamedDescendant<TextBox>(view, "TableKitOutputCodeDir")!.IsReadOnly);
                }
                finally
                {
                    window.Close();
                }
            });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
    /// <summary>验证 Luban.dll 文件选择器从字段原目录打开，并将选择折叠为项目相对路径。</summary>
    [Fact]
    public async Task LubanFilePickerStartsFromConfiguredPath()
    {
        string root = CreateConfiguredProject();
        try
        {
            string selected = Path.Combine(root, "Luban", "Selected", "Luban.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(selected)!);
            File.WriteAllText(selected, string.Empty);
            RecordingLubanFilePicker picker = new() { SelectedPath = selected };
            TableKitPageViewModel viewModel = new(root, new TableKitApplicationService(), lubanFilePicker: picker);

            await viewModel.BrowseLubanExecutableCommand.ExecuteAsync();

            Assert.Equal(Path.Combine(root, "Luban", "Tools", "Luban"), picker.LastSuggestedPath);
            Assert.Equal("Luban/Selected/Luban.dll", viewModel.LubanExecutablePath);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    /// <summary>验证额外输出完整展示 target、代码/数据 target 和两条只读输出路径。</summary>
    [Fact]
    public async Task ExtraOutputRendersCompleteContract()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            TableKitPageViewModel viewModel = new();
            viewModel.AddExtraOutputCommand.Execute(null);
            viewModel.AddExtraOutputCommand.Execute(null);
            viewModel.AddExtraOutputCommand.Execute(null);
            TableKitConfigurationView view = new() { DataContext = viewModel };
            Window window = new() { Width = 1000, Height = 680, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                TextBlock[] labels = view.GetVisualDescendants().OfType<TextBlock>().ToArray();
                Assert.Contains(labels, static item => item.Text == "Code target");
                Assert.Contains(labels, static item => item.Text == "数据目录");
                Assert.Contains(labels, static item => item.Text == "代码目录");
                TextBox[] extraPaths = view.GetVisualDescendants()
                    .OfType<TextBox>()
                    .Where(static item => item.DataContext is TableKitExtraOutputViewModel && item.IsReadOnly)
                    .ToArray();
                Assert.Equal(6, extraPaths.Length);
                ScrollViewer? extraOutputScroll = view.FindControl<ScrollViewer>("TableKitExtraOutputScroll");
                Assert.NotNull(extraOutputScroll);
                Assert.Contains(extraOutputScroll.GetVisualDescendants().OfType<ScrollBar>(),
                    static item => item.IsVisible && item.Orientation == Orientation.Vertical);
                AssertNoVisibleHorizontalScrollBar(view);
                SaveFrame(window, 1000, 680, "configuration-extra");
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>验证默认配置任务独占主区，关键分组完整且控制台保持 36px 收起态。</summary>
    [Theory]
    [InlineData(1000, 680)]
    [InlineData(1450, 900)]
    public async Task ConfigurationTaskUsesFullWorkspace(double width, double height)
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            TableKitPageViewModel viewModel = new();
            TableKitPageView view = new() { DataContext = viewModel };
            Window window = new() { Width = width, Height = height, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                TabControl? tabs = view.FindControl<TabControl>("TableKitWorkspaceTabs");
                GridSplitter? consoleSplitter = view.FindControl<GridSplitter>("TableKitConsoleSplitter");
                Border? consolePanel = view.FindControl<Border>("TableKitConsolePanel");
                Assert.NotNull(tabs);
                Assert.NotNull(consoleSplitter);
                Assert.NotNull(consolePanel);
                Assert.Equal(0, tabs.SelectedIndex);
                Assert.Equal(0, viewModel.SelectedWorkspaceIndex);
                Assert.False(viewModel.IsConsoleExpanded);
                Assert.False(consoleSplitter.IsVisible);
                Assert.InRange(consolePanel.Bounds.Height, 35d, 37d);
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "环境与路径配置");
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "目标与输出");
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "生成选项");
                Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>(), static item => item.Text == "生成契约");
                Assert.DoesNotContain(view.GetVisualDescendants().OfType<ScrollViewer>(), static item => item.Name == "TableKitConfigScroll");
                TableKitConfigurationView configurationView = view.FindControl<TableKitConfigurationView>("TableKitConfigView")!;
                Grid configLayout = configurationView.FindControl<Grid>("TableKitConfigLayout")!;
                Border leftPane = configurationView.FindControl<Border>("TableKitLeftPane")!;
                Border rightPane = configurationView.FindControl<Border>("TableKitRightPane")!;
                Border extraOutputCard = configurationView.FindControl<Border>("TableKitExtraOutputCard")!;
                ScrollViewer extraOutputScroll = configurationView.FindControl<ScrollViewer>("TableKitExtraOutputScroll")!;
                Assert.NotNull(configLayout);
                Assert.NotNull(leftPane);
                Assert.NotNull(rightPane);
                Assert.NotNull(extraOutputCard);
                Assert.NotNull(extraOutputScroll);
                Assert.InRange(rightPane.Bounds.X - leftPane.Bounds.Right, 19d, 21d);
                Assert.InRange(Math.Abs(leftPane.Bounds.Height - rightPane.Bounds.Height), 0d, 1d);
                Assert.True(leftPane.CornerRadius.TopLeft > 0d);
                Assert.True(rightPane.CornerRadius.TopLeft > 0d);
                Assert.True(extraOutputCard.CornerRadius.TopLeft > 0d);
                Assert.Equal(1d, leftPane.BorderThickness.Left);
                Assert.Equal(1d, rightPane.BorderThickness.Left);
                AssertNoVisibleVerticalScrollBar(view);
                AssertNoVisibleHorizontalScrollBar(view);
                AssertMinimumTextSize(view);
                SaveFrame(window, width, height, "configuration");

                viewModel.ConsoleEntries.Add(new TableKitConsoleEntryViewModel("12:00:00", "INFO", "Luban 验证已启动。"));
                Dispatcher.UIThread.RunJobs();
                Assert.False(viewModel.IsConsoleExpanded);
                Assert.False(consoleSplitter.IsVisible);

                viewModel.IsConsoleExpanded = true;
                Dispatcher.UIThread.RunJobs();
                Assert.True(consoleSplitter.IsVisible);
                Assert.True(view.FindControl<ListBox>("TableKitConsoleList")!.IsVisible);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>验证成功结果会自动进入数据任务，并选中首表首记录。</summary>
    [Fact]
    public void SuccessfulValidationSelectsFirstTableAndRecord()
    {
        TableKitPageViewModel viewModel = new();
        viewModel.IsConsoleExpanded = true;
        viewModel.ApplyOperationResult(CreateSuccessfulResult(), true);

        Assert.Equal(1, viewModel.SelectedWorkspaceIndex);
        Assert.False(viewModel.IsConsoleExpanded);
        Assert.Equal("buff", viewModel.SelectedPreviewTable?.Name);
        Assert.Equal(2, viewModel.SelectedPreviewRecords.Count);
        Assert.Equal("1. 10001", viewModel.SelectedPreviewRecord?.Title);
        Assert.Equal(4, viewModel.SelectedPreviewFields.Count);
        Assert.Contains("\"id\": 10001", viewModel.SelectedPreviewJson, StringComparison.Ordinal);
        Assert.Contains("0 错误", viewModel.ConsoleSummaryText, StringComparison.Ordinal);
    }

    /// <summary>在数据任务中验证表、记录、字段与原始 JSON 三级区域同时可见。</summary>
    [Theory]
    [InlineData(1000, 680)]
    [InlineData(1450, 900)]
    public async Task DataTaskRendersThreeLevelBrowser(double width, double height)
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            TableKitPageViewModel viewModel = new();
            viewModel.ApplyOperationResult(CreateSuccessfulResult(), true);
            TableKitPageView view = new() { DataContext = viewModel };
            Window window = new() { Width = width, Height = height, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(1, view.FindControl<TabControl>("TableKitWorkspaceTabs")!.SelectedIndex);
                Assert.NotNull(FindNamedDescendant<ListBox>(view, "TableKitPreviewTableList"));
                Assert.NotNull(FindNamedDescendant<ListBox>(view, "TableKitPreviewRecordList"));
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "原始 JSON");
                Assert.Contains(
                    view.GetVisualDescendants().OfType<Border>(),
                    static item => item.IsVisible && item.Classes.Contains("tablekit-field-card"));
                Assert.False(viewModel.IsConsoleExpanded);
                AssertNoVisibleHorizontalScrollBar(view);
                AssertMinimumTextSize(view);
                SaveFrame(window, width, height, "data");
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>验证完整 Workbench Shell 在最小窗口保留动作栏与完整配置任务。</summary>
    [Fact]
    public async Task TableKitShellRendersMinimumConfigurationLayout()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            WorkbenchShellViewModel viewModel = CreateShellViewModel();
            Window window = new() { Width = 1280, Height = 820, Content = new WorkbenchShellView(viewModel) };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "保存");
                Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "验证");
                Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "生成");
                Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "环境与路径配置");
                TableKitPageView tableKitPage = window.GetVisualDescendants().OfType<TableKitPageView>().Single();
                AssertNoVisibleHorizontalScrollBar(tableKitPage);
                AssertMinimumTextSize(tableKitPage);
                SaveFrame(window, 1280, 820, "shell-configuration");
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>验证宽屏完整 Shell 的成功态直接显示三级数据浏览，而不是空详情。</summary>
    [Fact]
    public async Task TableKitShellRendersSuccessfulDataLayout()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            WorkbenchShellViewModel viewModel = CreateShellViewModel();
            viewModel.TableKitPage.ApplyOperationResult(CreateSuccessfulResult(), true);
            Window window = new() { Width = 1556, Height = 1000, Content = new WorkbenchShellView(viewModel) };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "配置表");
                Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "记录");
                Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "原始 JSON");
                Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), static item => item.IsVisible && item.Text == "选择配置表查看 JSON 预览");
                TableKitPageView tableKitPage = window.GetVisualDescendants().OfType<TableKitPageView>().Single();
                AssertNoVisibleHorizontalScrollBar(tableKitPage);
                AssertMinimumTextSize(tableKitPage);
                SaveFrame(window, 1556, 1000, "shell-data");
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>创建已选择 TableKit 页的 Shell ViewModel。</summary>
    /// <returns>用于 Headless 渲染的 Shell 状态。</returns>
    private static WorkbenchShellViewModel CreateShellViewModel()
    {
        return new WorkbenchShellViewModel(() => { }, _ => { }, (_, _) => Task.CompletedTask)
        {
            SelectedPage = "TableKit"
        };
    }

    /// <summary>创建带历史绝对路径设置和已存在 Luban 目录的临时项目。</summary>
    /// <returns>临时项目根目录。</returns>
    private static string CreateConfiguredProject()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-tablekit-paths-" + Guid.NewGuid().ToString("N"));
        string workDir = Path.Combine(root, "Luban", "MiniTemplate");
        string executableDir = Path.Combine(root, "Luban", "Tools", "Luban");
        Directory.CreateDirectory(workDir);
        Directory.CreateDirectory(executableDir);
        Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
        File.WriteAllText(Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 2022.3.0f1");
        File.WriteAllText(Path.Combine(executableDir, "Luban.dll"), string.Empty);
        TableKitOptions options = new()
        {
            ProjectRoot = root,
            LubanConfigPath = Path.Combine(workDir, "luban.conf"),
            LubanWorkDir = workDir,
            LubanExecutablePath = Path.Combine(executableDir, "Luban.dll"),
            TargetName = string.Empty,
            OutputDataDir = Path.Combine(root, "Assets", "Resources", "Art", "Table"),
            OutputCodeDir = Path.Combine(root, "Assets", "Scripts", "TableKit")
        };
        new TableKitSettingsService().Save(root, options);
        return root;
    }

    /// <summary>创建包含两条结构化记录的成功验证结果。</summary>
    /// <returns>用于状态与视觉测试的 TableKit 结果。</returns>
    private static TableKitOperationResult CreateSuccessfulResult()
    {
        const string previewJson = """
            {
              "buff_config": [
                { "id": 10001, "name": "治疗药水", "show": true, "values": [1, 2] },
                { "id": 10002, "name": "护盾药水", "show": false, "values": [3] }
              ]
            }
            """;
        return new TableKitOperationResult
        {
            Succeeded = true,
            Log = "验证完成。",
            PreviewDirectory = "Temp/LubanValidate",
            PreviewTables = new[]
            {
                new TableKitPreviewTable { Name = "buff", Count = 2, PreviewJson = previewJson }
            }
        };
    }

    /// <summary>按名称查找嵌套 UserControl Namescope 中的可视控件。</summary>
    /// <typeparam name="TControl">目标控件类型。</typeparam>
    /// <param name="root">搜索根控件。</param>
    /// <param name="name">XAML 名称。</param>
    /// <returns>匹配控件；不存在时返回 null。</returns>
    private static TControl? FindNamedDescendant<TControl>(Control root, string name) where TControl : Control
    {
        return root.GetVisualDescendants().OfType<TControl>().FirstOrDefault(control => control.Name == name);
    }

    /// <summary>断言页面没有可见横向滚动条。</summary>
    /// <param name="root">待检查可视根。</param>
    private static void AssertNoVisibleHorizontalScrollBar(Control root)
    {
        ScrollBar[] horizontalScrollBars = root.GetVisualDescendants()
            .OfType<ScrollBar>()
            .Where(static item => item.IsVisible && item.Orientation == Orientation.Horizontal)
            .ToArray();
        Assert.Empty(horizontalScrollBars);
    }

    /// <summary>断言默认页面没有可见纵向滚动条，避免配置任务整体滚动。</summary>
    /// <param name="root">待检查可视根。</param>
    private static void AssertNoVisibleVerticalScrollBar(Control root)
    {
        ScrollBar[] verticalScrollBars = root.GetVisualDescendants()
            .OfType<ScrollBar>()
            .Where(static item => item.IsVisible && item.Orientation == Orientation.Vertical)
            .ToArray();
        Assert.Empty(verticalScrollBars);
    }

    /// <summary>断言所有可见文字遵守 12px 最小字号。</summary>
    /// <param name="root">待检查可视根。</param>
    private static void AssertMinimumTextSize(Control root)
    {
        TextBlock[] visibleText = root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(static item => item.IsVisible && item.Bounds.Width > 0d && item.Bounds.Height > 0d)
            .ToArray();
        Assert.All(visibleText, static item => Assert.True(item.FontSize >= 12d));
    }

    /// <summary>保存 TableKit Headless 截图并拒绝空白输出。</summary>
    private static void SaveFrame(Window window, double width, double height, string stateName)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        string outputDirectory = Path.Combine(
            WorkbenchContractTestFiles.FindWorkbenchRoot(), ".artifacts", "screenshots", "workbench");
        Directory.CreateDirectory(outputDirectory);
        string fileName = "tablekit-" + stateName + "-" + (int)width + "x" + (int)height + ".png";
        using FileStream stream = new(
            Path.Combine(outputDirectory, fileName), FileMode.Create, FileAccess.Write, FileShare.Read);
        frame.Save(stream);
        Assert.True(stream.Length > 1024, "TableKit Headless 截图内容为空或异常小。");
    }

    /// <summary>记录原生目录选择器调用参数，并返回预设目录。</summary>
    private sealed class RecordingFolderPicker : IInstallerFolderPicker
    {
        /// <summary>下一次选择器调用返回的目录。</summary>
        public string? SelectedPath { get; init; }
        /// <summary>最近一次选择器收到的建议起始目录。</summary>
        public string? LastSuggestedPath { get; private set; }

        /// <summary>记录建议目录并返回预设选择结果。</summary>
        /// <param name="title">选择器标题。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <param name="suggestedPath">建议起始目录。</param>
        /// <returns>预设目录。</returns>
        public Task<string?> PickFolderAsync(
            string title,
            CancellationToken cancellationToken = default,
            string? suggestedPath = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastSuggestedPath = suggestedPath;
            return Task.FromResult(SelectedPath);
        }
    }

    /// <summary>记录 Luban.dll 文件选择器调用参数，并返回预设文件。</summary>
    private sealed class RecordingLubanFilePicker : ITableKitLubanFilePicker
    {
        /// <summary>下一次选择器调用返回的文件。</summary>
        public string? SelectedPath { get; init; }
        /// <summary>最近一次选择器收到的建议起始目录。</summary>
        public string? LastSuggestedPath { get; private set; }

        /// <summary>记录建议目录并返回预设 Luban.dll 文件。</summary>
        /// <param name="title">选择器标题。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <param name="suggestedPath">建议起始目录。</param>
        /// <returns>预设文件路径。</returns>
        public Task<string?> PickLubanDllAsync(
            string title,
            CancellationToken cancellationToken = default,
            string? suggestedPath = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastSuggestedPath = suggestedPath;
            return Task.FromResult(SelectedPath);
        }
    }
}
