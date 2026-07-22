using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views;
using YokiFrame.Workbench.Avalonia.Views.Pages;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>覆盖 LogKit 导航、配置专页契约和默认/最小窗口布局。</summary>
public sealed class LogKitPageContractTests
{
    /// <summary>验证 LogKit 使用专页 presentation 并进入 Core 导航。</summary>
    [Fact]
    public void CatalogAndShellExposeDedicatedLogKitPage()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, (_, _) => Task.CompletedTask)
        {
            SelectedPage = "LogKit"
        };

        Assert.True(viewModel.IsLogKitPage);
        Assert.False(viewModel.IsOverviewPage);
        Assert.Equal("LogKit", viewModel.CurrentPageTitle);
        Assert.Contains(viewModel.NavigationGroups.SelectMany(static group => group.Items), static item => item.PageName == "LogKit");
    }

    /// <summary>验证 LogKit 只保留配置结构，旧日志浏览和筛选入口不再进入页面。</summary>
    [Fact]
    public void XamlUsesConfigurationOnlyLayout()
    {
        var page = WorkbenchContractTestFiles.ReadSource("Views", "Pages", "LogKitPageView.axaml");
        var shell = WorkbenchContractTestFiles.ReadSource("Views", "WorkbenchShellView.axaml");
        var styles = WorkbenchContractTestFiles.ReadSource("Styles", "LogKit.axaml");
        var app = WorkbenchContractTestFiles.ReadSource("App.axaml");

        Assert.Contains("RowDefinitions=\"Auto,*,Auto\"", page, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ConfigGrid\"", page, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"*,*\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Width=\"380\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Width=\"480\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Width=\"876\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxWidth=\"1080\"", page, StringComparison.Ordinal);
        Assert.Contains("Classes=\"logkit-runtime-banner\"", page, StringComparison.Ordinal);
        Assert.Contains("Classes=\"logkit-settings-footer\"", page, StringComparison.Ordinal);
        Assert.Contains("OutputSettingsCard", page, StringComparison.Ordinal);
        Assert.Contains("FileSettingsCard", page, StringComparison.Ordinal);
        Assert.Contains("CapacitySettingsCard", page, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewerPanel", page, StringComparison.Ordinal);
        Assert.DoesNotContain("HistoryRows", page, StringComparison.Ordinal);
        Assert.DoesNotContain("FilePreview", page, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectMemorySourceCommand", page, StringComparison.Ordinal);
        Assert.DoesNotContain("workbench.logkit.search", shell, StringComparison.Ordinal);
        Assert.Contains("LogKitPage.SaveSettingsCommand", shell, StringComparison.Ordinal);
        Assert.Contains("LogKitPage.ResetSettingsCommand", shell, StringComparison.Ordinal);
        Assert.Contains("LogKitPage.OpenDirectoryCommand", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("LogKitPage.ClearHistoryCommand", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("LogKitPage.DataChannelText", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("SupportsFileWriter", page, StringComparison.Ordinal);
        Assert.DoesNotContain("SupportsPlayerImGui", page, StringComparison.Ordinal);
        Assert.Contains("SupportsEncryption", page, StringComparison.Ordinal);
        Assert.Contains("EncryptionMethodText", page, StringComparison.Ordinal);
        Assert.Contains("DecryptionStatusText", page, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{CompiledBinding ProjectCanPersist}\"", page, StringComparison.Ordinal);
        Assert.Contains("pages:LogKitPageView", shell, StringComparison.Ordinal);
        Assert.Contains("Styles/LogKit.axaml", app, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize.Micro", page, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize.Xs", page, StringComparison.Ordinal);
        Assert.DoesNotContain("#", styles, StringComparison.Ordinal);
    }

    /// <summary>验证文件卡片位于顶部全宽，输出和容量卡片在宽屏等分、窄屏堆叠。</summary>
    [Theory]
    [InlineData(1700, 1060)]
    [InlineData(1280, 820)]
    [InlineData(2048, 1116)]
    [InlineData(1024, 768)]
    public async Task PageRendersWithoutPanelOverlap(double width, double height)
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() => AssertLayout(width, height));
    }

    /// <summary>验证即使 Runtime 返回历史记录，配置专页也不会重新渲染日志浏览器。</summary>
    [Fact]
    public async Task RuntimeHistoryDoesNotRestoreLogViewer()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(AssertPopulatedLayout);
    }

    /// <summary>验证未实现的 Runtime capability 不会锁定 Unity 项目配置控件。</summary>
    [Fact]
    public async Task ProjectSettingsRemainEditableWhenRuntimeCapabilitiesAreUnavailable()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(AssertUnsupportedCapabilitiesDoNotDisableSettings);
    }

    /// <summary>验证真实页面上的配置控件会通过 TwoWay 绑定回写 LogKit 项目草稿。</summary>
    [Fact]
    public async Task ProjectSettingsControlsWriteThroughBindings()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(AssertProjectSettingsWriteThroughBindings);
    }

    /// <summary>验证只读投影恢复后，真实配置控件会从禁用状态切换为可交互状态。</summary>
    [Fact]
    public async Task ProjectSettingsControlsRecoverAfterRuntimeIdentityArrives()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(AssertProjectSettingsControlsRecover);
    }

    /// <summary>在真实 Shell 中打开 LogKit 并检查响应式卡片关系、宽度上限和非空帧。</summary>
    private static void AssertLayout(double width, double height)
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, (_, _) => Task.CompletedTask)
        {
            SelectedPage = "LogKit"
        };
        Window window = new()
        {
            Width = width,
            Height = height,
            Content = new WorkbenchShellView(viewModel)
        };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var page = Assert.Single(window.GetVisualDescendants().OfType<LogKitPageView>(), static item => item.IsVisible);
            var settings = page.FindControl<Border>("SettingsPanel");
            var output = page.FindControl<Border>("OutputSettingsCard");
            var files = page.FindControl<Border>("FileSettingsCard");
            var capacity = page.FindControl<Border>("CapacitySettingsCard");
            Assert.NotNull(settings);
            Assert.NotNull(output);
            Assert.NotNull(files);
            Assert.NotNull(capacity);
            Assert.Equal(page.Bounds.Width, settings.Bounds.Width, 0.5d);
            Assert.True(output.Bounds.Width >= 360d);
            Assert.True(capacity.Bounds.Width >= 440d);
            Assert.True(capacity.Bounds.Height < 400d);
            bool useTwoColumns = page.Bounds.Width >= 920d;
            Assert.Equal(files.Bounds.Left, output.Bounds.Left, 0.5d);
            Assert.Equal(files.Bounds.Right, capacity.Bounds.Right, 0.5d);
            Assert.True(files.Bounds.Top <= output.Bounds.Top);
            if (useTwoColumns)
            {
                Assert.Equal(output.Bounds.Top, capacity.Bounds.Top, 0.5d);
                Assert.Equal(output.Bounds.Width, capacity.Bounds.Width, 0.5d);
                Assert.True(files.Bounds.Bottom <= output.Bounds.Top);
            }
            else
            {
                Assert.Equal(output.Bounds.Width, files.Bounds.Width, 0.5d);
                Assert.Equal(output.Bounds.Left, capacity.Bounds.Left, 0.5d);
                Assert.Equal(output.Bounds.Width, capacity.Bounds.Width, 0.5d);
                Assert.True(output.Bounds.Bottom <= capacity.Bounds.Top);
            }
            Assert.True(page.Bounds.Width <= window.ClientSize.Width);
            var visibleText = page.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(static item => item.IsVisible)
                .ToArray();
            Assert.NotEmpty(visibleText);
            Assert.All(visibleText, static item => Assert.True(item.FontSize >= 12d));
            AssertNonBlankFrame(window);
            SaveFrame(window, width, height);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>构造包含足量日志的 Runtime 状态并检查页面仍只展示配置。</summary>
    private static void AssertPopulatedLayout()
    {
        var settings = YokiFrame.Tooling.Application.Models.LogKit.WorkbenchLogKitSettings.CreateDefault();
        using var viewModel = LogKitContractTestData.CreateViewModel(
            load: _ => LogKitContractTestData.CreateProjectSettings(settings));
        var entries = Enumerable.Range(0, 40)
            .Select(static index => LogKitContractTestData.CreateEntry(
                index % 4 == 0 ? "Error" : (index % 3 == 0 ? "Warning" : "Info"),
                "Runtime message " + index,
                "2026-07-15T08:00:00." + index.ToString("000") + "Z",
                "Demo.Controller",
                index == 0 ? "Selected failure" : string.Empty))
            .ToArray();
        viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(entries, settings));
        LogKitPageView view = new() { DataContext = viewModel };
        Window window = new() { Width = 1200, Height = 760, Content = view };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var visibleText = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(static item => item.IsVisible)
                .Select(static item => item.Text)
                .ToArray();
            Assert.Contains("输出目标", visibleText);
            Assert.Contains("等级与容量", visibleText);
            Assert.Contains("日志文件", visibleText);
            Assert.DoesNotContain("Runtime message 0", visibleText);
            Assert.DoesNotContain("InvalidOperationException: Selected failure", visibleText);
            SaveFrame(window, "logkit-config-only-1200x760.png");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>加载可持久化 Unity 配置，并检查配置区全部输入控件保持启用。</summary>
    private static void AssertUnsupportedCapabilitiesDoNotDisableSettings()
    {
        var settings = YokiFrame.Tooling.Application.Models.LogKit.WorkbenchLogKitSettings.CreateDefault();
        using var viewModel = LogKitContractTestData.CreateViewModel(
            load: _ => LogKitContractTestData.CreateProjectSettings(settings));
        LogKitContractTestData.SetPageActive(viewModel, true);
        Assert.False(viewModel.SupportsSettingsApply);
        Assert.False(viewModel.SupportsFileWriter);
        Assert.False(viewModel.SupportsPlayerImGui);
        Assert.False(viewModel.SupportsEncryption);
        LogKitPageView view = new() { DataContext = viewModel };
        Window window = new() { Width = 1200, Height = 760, Content = view };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var panel = view.FindControl<Border>("SettingsPanel");
            Assert.NotNull(panel);
            var inputs = panel.GetVisualDescendants()
                .OfType<InputElement>()
                .Where(static item => item is ToggleSwitch or ComboBox or NumericUpDown or TextBox)
                .ToArray();
            Assert.NotEmpty(inputs);
            var encryptionToggle = view.FindControl<ToggleSwitch>("EncryptionToggle");
            Assert.NotNull(encryptionToggle);
            Assert.False(encryptionToggle.IsEffectivelyEnabled);
            Assert.False(encryptionToggle.IsChecked);
            Assert.All(
                inputs.Where(item => !ReferenceEquals(item, encryptionToggle)),
                static item => Assert.True(item.IsEffectivelyEnabled));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>直接更新配置控件公开值，验证页面的 TwoWay 绑定而不重复验证 Headless 输入注入机制。</summary>
    private static void AssertProjectSettingsWriteThroughBindings()
    {
        var settings = YokiFrame.Tooling.Application.Models.LogKit.WorkbenchLogKitSettings.CreateDefault();
        using var viewModel = LogKitContractTestData.CreateViewModel(
            load: _ => LogKitContractTestData.CreateProjectSettings(settings),
            save: (_, draft, _, _) => Task.FromResult(LogKitContractTestData.CreateSaveResult(
                LogKitContractTestData.CreateProjectSettings(draft, "fingerprint-2"),
                null)));
        LogKitContractTestData.SetPageActive(viewModel, true);
        LogKitPageView view = new() { DataContext = viewModel };
        Window window = new() { Width = 1200, Height = 760, Content = view };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var panel = view.FindControl<Border>("SettingsPanel");
            Assert.NotNull(panel);
            var editorFileToggle = view.FindControl<ToggleSwitch>("EditorFileWriteToggle");
            Assert.NotNull(editorFileToggle);
            Assert.True(editorFileToggle.IsEffectivelyEnabled);
            Assert.False(viewModel.SettingsDraft.SaveLogInEditor);
            editorFileToggle.IsChecked = true;
            Dispatcher.UIThread.RunJobs();

            var logDirectory = view.FindControl<TextBox>("LogDirectoryInput");
            Assert.NotNull(logDirectory);
            Assert.True(logDirectory.IsEffectivelyEnabled);
            logDirectory.Text = "/custom";
            Dispatcher.UIThread.RunJobs();

            Assert.True(viewModel.SettingsDraft.SaveLogInEditor);
            Assert.EndsWith("/custom", viewModel.SettingsDraft.LogDirectory, StringComparison.Ordinal);
            Assert.True(viewModel.IsSettingsDirty);
            Assert.True(viewModel.SaveSettingsCommand.CanExecute(null));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>构造离线只读投影并发送 Unity Runtime 帧，检查控件启用状态随项目配置恢复。</summary>
    private static void AssertProjectSettingsControlsRecover()
    {
        var settings = YokiFrame.Tooling.Application.Models.LogKit.WorkbenchLogKitSettings.CreateDefault();
        using var viewModel = LogKitContractTestData.CreateViewModel(
            load: engineId => LogKitContractTestData.CreateProjectSettings(
                settings,
                engineId: engineId,
                canPersist: string.Equals(engineId, "unity-editor", StringComparison.Ordinal)));
        LogKitContractTestData.SetPageActive(viewModel, true);
        LogKitPageView view = new() { DataContext = viewModel };
        Window window = new() { Width = 1200, Height = 760, Content = view };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var inputs = FindSettingsInputs(view);
            Assert.NotEmpty(inputs);
            Assert.All(inputs, static item => Assert.False(item.IsEffectivelyEnabled));

            viewModel.ApplyPeriodicState(LogKitContractTestData.CreateState(settings: settings));
            Dispatcher.UIThread.RunJobs();

            Assert.True(viewModel.ProjectCanPersist);
            var configGrid = view.FindControl<Grid>("ConfigGrid");
            Assert.NotNull(configGrid);
            Assert.True(configGrid.IsEnabled);
            Assert.True(configGrid.IsEffectivelyEnabled);
            Assert.All(inputs, static item => Assert.True(item.IsEffectivelyEnabled));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>提取 LogKit 配置区域中的所有输入控件，供启用状态回归断言复用。</summary>
    private static IReadOnlyList<InputElement> FindSettingsInputs(LogKitPageView view)
    {
        var panel = view.FindControl<Border>("SettingsPanel");
        Assert.NotNull(panel);
        return panel.GetVisualDescendants()
            .OfType<InputElement>()
            .Where(static item => item is ToggleSwitch or ComboBox or NumericUpDown or TextBox)
            .ToArray();
    }

    /// <summary>拒绝空白或异常小的 Headless 渲染帧。</summary>
    private static void AssertNonBlankFrame(Window window)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.True(frame.PixelSize.Width > 1000);
        Assert.True(frame.PixelSize.Height > 700);
    }

    /// <summary>保存 LogKit 默认与最小窗口视觉证据，供布局复核。</summary>
    private static void SaveFrame(Window window, double width, double height)
    {
        SaveFrame(window, "logkit-" + (int)width + "x" + (int)height + ".png");
    }

    /// <summary>保存指定名称的 LogKit Headless 视觉证据。</summary>
    private static void SaveFrame(Window window, string fileName)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var outputDirectory = Path.Combine(
            WorkbenchContractTestFiles.FindWorkbenchRoot(),
            ".artifacts",
            "screenshots",
            "workbench");
        Directory.CreateDirectory(outputDirectory);
        using FileStream stream = new(
            Path.Combine(outputDirectory, fileName),
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read);
        frame.Save(stream);
        Assert.True(stream.Length > 1024, "LogKit Headless 截图内容为空或异常小。");
    }
}
