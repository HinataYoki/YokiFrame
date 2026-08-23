using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Globalization;
using YokiFrame.Tooling.Application.Models.UIKit;
using YokiFrame.Tooling.Application.Services.UIKit;
using YokiFrame.Workbench.Avalonia.Converters;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views;
using YokiFrame.Workbench.Avalonia.Views.Pages;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 UIKit 页面筛选、选择、响应式布局和 Headless 视觉结果。</summary>
public sealed class UIKitPageViewModelTests
{
    /// <summary>验证页面填充指标，保留选择并按 Name/Type 搜索面板。</summary>
    [Fact]
    public void ApplyPeriodicState_PopulatesAndFiltersRuntimePanels()
    {
        UIKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(6, 2, panelsTruncated: true));

        Assert.Equal(6, viewModel.Panels.Count);
        Assert.NotNull(viewModel.SelectedPanel);
        Assert.Equal(8, viewModel.PanelCount);
        Assert.True(viewModel.PanelsTruncated);
        Assert.Equal("显示 6 / 8", viewModel.CoverageText);
        Assert.Equal("根节点在线", viewModel.RootStatusText);
        Assert.Equal("遮罩已启用", viewModel.ModalStatusText);

        viewModel.SearchText = "Panel5";

        WorkbenchUIKitPanel panel = Assert.Single(viewModel.Panels);
        Assert.Equal("Panel5", panel.Name);
        Assert.Same(panel, viewModel.SelectedPanel);
    }

    /// <summary>验证切换命名栈集合后详情和覆盖率跟随当前选项卡。</summary>
    [Fact]
    public void SelectingStacks_UpdatesMasterDetailProjection()
    {
        UIKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(3, 2, stacksTruncated: true));

        viewModel.SelectedCollectionIndex = 1;

        Assert.True(viewModel.IsStacksView);
        Assert.True(viewModel.ShowStackDetails);
        Assert.NotNull(viewModel.SelectedStack);
        Assert.Equal("显示 2 / 3", viewModel.CoverageText);
        Assert.True(viewModel.CurrentCollectionTruncated);
    }

    /// <summary>验证页面 XAML 明确包含双尺寸布局、虚拟化和无横向滚动策略。</summary>
    [Fact]
    public void PageContractUsesAdaptiveVirtualizedMasterDetail()
    {
        string xaml = WorkbenchContractTestFiles.ReadSource("Views", "Pages", "UIKitPageView.axaml");
        string styles = WorkbenchContractTestFiles.ReadSource("Styles", "UIKit.axaml");

        Assert.Contains("UIKitWideLayout", xaml, StringComparison.Ordinal);
        Assert.Contains("UIKitCompactLayout", xaml, StringComparison.Ordinal);
        Assert.Contains("UIKitMetricsPanel", xaml, StringComparison.Ordinal);
        Assert.Contains("UIKitDetailPanel", xaml, StringComparison.Ordinal);
        Assert.Contains("VirtualizingStackPanel", xaml, StringComparison.Ordinal);
        // i18n 切片后页面词条改用 DynamicResource 资源 key，兼容旧中文直书与资源 key 两种契约。
        Assert.True(xaml.Contains("运行时诊断") || xaml.Contains("String.UIKit.RuntimeDiagnostics"), "UIKit 页面应包含运行时诊断词条");
        Assert.True(xaml.Contains("编辑器工具") || xaml.Contains("String.UIKit.EditorTools"), "UIKit 页面应包含编辑器工具词条");
        Assert.DoesNotContain("根节点设置", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("UIKitRootSettingsView", xaml, StringComparison.Ordinal);
        Assert.Contains("kit-panel-header", xaml, StringComparison.Ordinal);
        Assert.Contains("kit-stat uikit-summary-metric", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Runtime\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Panels\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("HorizontalScrollBarVisibility=\"Auto\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Viewbox", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#", styles, StringComparison.Ordinal);
    }

    /// <summary>验证运行时协议枚举只在表现层转换为简体中文。</summary>
    [Fact]
    public void DisplayConverterLocalizesRuntimeEnums()
    {
        UIKitDisplayTextConverter converter = new();

        Assert.Equal("已打开", converter.Convert("Open", typeof(string), "state", CultureInfo.InvariantCulture));
        Assert.Equal("已缓存", converter.Convert("Cached", typeof(string), "state", CultureInfo.InvariantCulture));
        Assert.Equal("常规层", converter.Convert("Common", typeof(string), "level", CultureInfo.InvariantCulture));
        Assert.Equal("可复用", converter.Convert("Reusable", typeof(string), "cache", CultureInfo.InvariantCulture));
        Assert.Equal("是", converter.Convert(true, typeof(string), "boolean", CultureInfo.InvariantCulture));
        Assert.Equal("Game.UI.Panel", converter.Convert("Game.UI.Panel", typeof(string), null, CultureInfo.InvariantCulture));
    }

    /// <summary>验证 Unity Editor Tools 任务切换、默认值回读和强类型操作委托。</summary>
    [Fact]
    public async Task EditorToolsTaskUsesUnityOnlyStronglyTypedActions()
    {
        WorkbenchUIKitEditorAction? lastAction = null;
        WorkbenchUIKitPanelGenerationRequest? lastRequest = null;
        UIKitPageViewModel viewModel = new(
            null,
            (action, request, _) =>
            {
                lastAction = action;
                lastRequest = request;
                return Task.FromResult(new WorkbenchUIKitEditorResult
                {
                    Succeeded = true,
                    Action = action,
                    Message = "done",
                    Context = CreateEditorContext(),
                });
            });
        viewModel.SetEditorEngine("unity-editor");

        await viewModel.ShowEditorToolsTaskCommand.ExecuteAsync();
        Assert.True(viewModel.IsEditorToolsTask);
        Assert.Equal(WorkbenchUIKitEditorAction.RefreshContext, lastAction);
        Assert.Equal("Assets/UI", viewModel.PrefabFolder);
        Assert.Equal("精简", viewModel.CodeTemplateDisplay);
        Assert.Equal(new[] { "默认", "精简", "TeamTemplate" }, viewModel.CodeTemplateOptions);
        Assert.Equal(new[] { "Assembly-CSharp", "Game.UI" }, viewModel.AssemblyNames);
        Assert.Equal("Game.UI", viewModel.AssemblyName);
        Assert.True(viewModel.CanGenerateCode);

        viewModel.PanelName = "InventoryPanel";
        viewModel.CodeTemplateDisplay = "TeamTemplate";
        await viewModel.CreatePanelPrefabCommand.ExecuteAsync();
        Assert.Equal(WorkbenchUIKitEditorAction.CreatePanelPrefab, lastAction);
        Assert.NotNull(lastRequest);
        Assert.Equal("InventoryPanel", lastRequest!.PanelName);
        Assert.Equal("TeamTemplate", lastRequest.CodeTemplate);
        Assert.Equal("done", viewModel.EditorStatusText);
    }

    /// <summary>验证选择相关操作会先读取最新上下文，再提交带当前 revision 的实际命令。</summary>
    [Fact]
    public async Task SelectionActionRefreshesContextBeforeSubmitting()
    {
        List<WorkbenchUIKitEditorAction> actions = new();
        WorkbenchUIKitPanelGenerationRequest? selectionRequest = null;
        UIKitPageViewModel viewModel = new(
            null,
            (action, request, _) =>
            {
                actions.Add(action);
                if (action == WorkbenchUIKitEditorAction.GenerateCodeForSelection)
                    selectionRequest = request;
                return Task.FromResult(new WorkbenchUIKitEditorResult
                {
                    Succeeded = true,
                    Action = action,
                    Context = CreateEditorContext(),
                    Message = "已就绪",
                });
            });
        viewModel.SetEditorEngine("unity-editor");
        await viewModel.ShowEditorToolsTaskCommand.ExecuteAsync();
        actions.Clear();

        await viewModel.GenerateCodeCommand.ExecuteAsync();

        Assert.Equal(
            new[]
            {
                WorkbenchUIKitEditorAction.RefreshContext,
                WorkbenchUIKitEditorAction.GenerateCodeForSelection,
            },
            actions);
        Assert.NotNull(selectionRequest);
        Assert.Equal(42L, selectionRequest!.ExpectedContextRevision);
        Assert.Equal("GlobalObjectId_V1-test", selectionRequest.TargetGlobalObjectId);
    }

    /// <summary>验证创建预制体前自动保存的 Editor Tools 配置会在新页面实例中恢复。</summary>
    [Fact]
    public async Task EditorToolsSettingsPersistAcrossViewModelInstances()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-uikit-editor-vm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            UIKitEditorSettingsService service = new(root);
            UIKitPageViewModel first = CreateEditorSettingsViewModel(service);
            first.SetEditorEngine("unity-editor");
            await first.ShowEditorToolsTaskCommand.ExecuteAsync();
            first.PrefabFolder = "Assets/Game/UI/Prefabs";
            first.ScriptFolder = "Assets/Game/UI/Scripts";
            first.ScriptNamespace = "Game.UI";
            first.AssemblyName = "Game.UI";
            first.CodeTemplateDisplay = "精简";
            first.PanelName = "InventoryPanel";

            await first.CreatePanelPrefabCommand.ExecuteAsync();

            UIKitPageViewModel second = CreateEditorSettingsViewModel(new UIKitEditorSettingsService(root));
            second.SetEditorEngine("unity-editor");
            await second.ShowEditorToolsTaskCommand.ExecuteAsync();
            Assert.Equal("Assets/Game/UI/Prefabs", second.PrefabFolder);
            Assert.Equal("Assets/Game/UI/Scripts", second.ScriptFolder);
            Assert.Equal("Game.UI", second.ScriptNamespace);
            Assert.Equal("Game.UI", second.AssemblyName);
            Assert.Equal("精简", second.CodeTemplateDisplay);
        }
        finally
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch
            {
                // 临时目录清理失败不覆盖测试的业务断言。
            }
        }
    }

    /// <summary>验证仅修改表单后关闭 Workbench 也会提交统一 Editor Settings。</summary>
    [Fact]
    public async Task EditorToolsSettingsPersistWhenWorkbenchClosesWithoutAction()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-uikit-editor-close-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            UIKitPageViewModel first = CreateEditorSettingsViewModel(new UIKitEditorSettingsService(root));
            first.SetEditorEngine("unity-editor");
            await first.ShowEditorToolsTaskCommand.ExecuteAsync();
            first.PrefabFolder = "Assets/Game/UI/ClosePrefabs";
            first.ScriptFolder = "Assets/Game/UI/CloseScripts";
            first.ScriptNamespace = "Game.CloseUI";
            first.AssemblyName = "Game.UI";
            first.CodeTemplateDisplay = "精简";

            await first.PersistEditorSettingsOnCloseAsync();

            UIKitPageViewModel second = CreateEditorSettingsViewModel(new UIKitEditorSettingsService(root));
            second.SetEditorEngine("unity-editor");
            await second.ShowEditorToolsTaskCommand.ExecuteAsync();
            Assert.Equal("Assets/Game/UI/ClosePrefabs", second.PrefabFolder);
            Assert.Equal("Assets/Game/UI/CloseScripts", second.ScriptFolder);
            Assert.Equal("Game.CloseUI", second.ScriptNamespace);
            Assert.Equal("Game.UI", second.AssemblyName);
            Assert.Equal("精简", second.CodeTemplateDisplay);
        }
        finally
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch
            {
                // 临时目录清理失败不覆盖测试的业务断言。
            }
        }
    }

    /// <summary>验证真实 Editor Tools ComboBox 选择程序集后关闭再打开仍恢复选择值。</summary>
    [Fact]
    public async Task EditorToolsAssemblySelectionPersistsWhenWorkbenchCloses()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-uikit-assembly-close-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            UIKitPageViewModel first = CreateEditorSettingsViewModel(
                new UIKitEditorSettingsService(root),
                defaultAssemblyName: "Assembly-CSharp");
            first.SetEditorEngine("unity-editor");
            await first.ShowEditorToolsTaskCommand.ExecuteAsync();

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                Window window = new()
                {
                    Width = 900,
                    Height = 620,
                    Content = new UIKitEditorToolsView { DataContext = first },
                };
                try
                {
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    UIKitEditorToolsView tools = Assert.Single(
                        window.GetVisualDescendants().OfType<UIKitEditorToolsView>());
                    ComboBox assemblySelector = tools.FindControl<ComboBox>("AssemblySelector")!;
                    assemblySelector.SelectedItem = "Game.UI";
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal("Game.UI", first.AssemblyName);
                    await first.PersistEditorSettingsOnCloseAsync();
                }
                finally
                {
                    window.Close();
                }
            });

            UIKitPageViewModel second = CreateEditorSettingsViewModel(new UIKitEditorSettingsService(root));
            second.SetEditorEngine("unity-editor");
            await second.ShowEditorToolsTaskCommand.ExecuteAsync();

            Assert.Equal("Game.UI", second.AssemblyName);
        }
        finally
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch
            {
                // 临时目录清理失败不覆盖测试的业务断言。
            }
        }
    }

    /// <summary>验证已保存的非默认程序集在第二次真实 ComboBox 绑定刷新后仍保持不变。</summary>
    [Fact]
    public async Task EditorToolsRestoresAssemblySelectionInSecondRealView()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-uikit-assembly-reopen-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            UIKitPageViewModel first = CreateEditorSettingsViewModel(
                new UIKitEditorSettingsService(root),
                defaultAssemblyName: "Assembly-CSharp");
            first.SetEditorEngine("unity-editor");
            await first.ShowEditorToolsTaskCommand.ExecuteAsync();

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                Window window = new()
                {
                    Width = 900,
                    Height = 620,
                    Content = new UIKitEditorToolsView { DataContext = first },
                };
                try
                {
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    ComboBox assemblySelector = Assert.Single(
                        window.GetVisualDescendants().OfType<ComboBox>(),
                        static item => item.Name == "AssemblySelector");
                    assemblySelector.SelectedItem = "Game.UI";
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal("Game.UI", first.AssemblyName);
                    await first.PersistEditorSettingsOnCloseAsync();
                }
                finally
                {
                    window.Close();
                }
            });

            UIKitPageViewModel second = CreateEditorSettingsViewModel(
                new UIKitEditorSettingsService(root),
                defaultAssemblyName: "Assembly-CSharp");
            second.SetEditorEngine("unity-editor");
            Window secondWindow = null!;
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                secondWindow = new Window
                {
                    Width = 900,
                    Height = 620,
                    Content = new UIKitEditorToolsView { DataContext = second },
                };
                secondWindow.Show();
                Dispatcher.UIThread.RunJobs();
            });
            try
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await second.ShowEditorToolsTaskCommand.ExecuteAsync();
                });
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal("Game.UI", second.AssemblyName);
                    ComboBox assemblySelector = Assert.Single(
                        secondWindow.GetVisualDescendants().OfType<ComboBox>(),
                        static item => item.Name == "AssemblySelector");
                    Assert.Equal("Game.UI", assemblySelector.SelectedItem);
                });
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(secondWindow.Close);
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch
            {
                // 临时目录清理失败不覆盖测试的业务断言。
            }
        }
    }

    /// <summary>验证 Unity context 暂时漏报程序集时不会覆盖已保存的目标程序集。</summary>
    [Fact]
    public async Task EditorToolsKeepsSavedAssemblyWhenContextTemporarilyOmitsIt()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-uikit-assembly-context-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            UIKitEditorSettingsService service = new(root);
            await service.SaveAsync(new WorkbenchUIKitPanelGenerationRequest
            {
                PrefabFolder = "Assets/UI",
                ScriptFolder = "Assets/Scripts/UI",
                ScriptNamespace = "Game.UI",
                AssemblyName = "Game.UI",
                CodeTemplate = "Minimal",
            }, CancellationToken.None);

            UIKitPageViewModel viewModel = new(
                null,
                (_, _, _) => Task.FromResult(new WorkbenchUIKitEditorResult
                {
                    Succeeded = true,
                    Context = CreateEditorContext(
                        assemblyName: "Assembly-CSharp",
                        assemblyNames: new[] { "Assembly-CSharp" }),
                    Message = "已就绪",
                }),
                service);
            viewModel.SetEditorEngine("unity-editor");
            await viewModel.ShowEditorToolsTaskCommand.ExecuteAsync();

            Assert.Equal("Game.UI", viewModel.AssemblyName);
            Assert.Contains("Game.UI", viewModel.AssemblyNames);

            await viewModel.PersistEditorSettingsOnCloseAsync();
            Assert.Equal("Game.UI", service.Load()?.AssemblyName);
        }
        finally
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch
            {
                // 临时目录清理失败不覆盖测试的业务断言。
            }
        }
    }

    /// <summary>验证 Unity context 暂时漏报项目代码模板时不会覆盖已保存的模板选择。</summary>
    [Fact]
    public async Task EditorToolsKeepsSavedCodeTemplateWhenContextTemporarilyOmitsIt()
    {
        string root = Path.Combine(Path.GetTempPath(), "yokiframe-uikit-template-context-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            UIKitEditorSettingsService service = new(root);
            await service.SaveAsync(new WorkbenchUIKitPanelGenerationRequest
            {
                PrefabFolder = "Assets/UI",
                ScriptFolder = "Assets/Scripts/UI",
                ScriptNamespace = "Game.UI",
                AssemblyName = "Game.UI",
                CodeTemplate = "TeamTemplate",
            }, CancellationToken.None);

            UIKitPageViewModel viewModel = new(
                null,
                (_, _, _) => Task.FromResult(new WorkbenchUIKitEditorResult
                {
                    Succeeded = true,
                    Context = CreateEditorContext(
                        codeTemplate: "Default",
                        codeTemplateOptions: new[] { "Default", "Minimal" }),
                    Message = "已就绪",
                }),
                service);
            viewModel.SetEditorEngine("unity-editor");
            await viewModel.ShowEditorToolsTaskCommand.ExecuteAsync();

            Assert.Equal("TeamTemplate", viewModel.CodeTemplate);
            Assert.Equal("TeamTemplate", viewModel.CodeTemplateDisplay);
            Assert.Contains("TeamTemplate", viewModel.CodeTemplateOptions);

            await viewModel.PersistEditorSettingsOnCloseAsync();
            Assert.Equal("TeamTemplate", service.Load()?.CodeTemplate);
        }
        finally
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch
            {
                // 临时目录清理失败不覆盖测试的业务断言。
            }
        }
    }

    /// <summary>验证 Editor Tools 仅保留面板创建与生成代码入口。</summary>
    [Fact]
    public void EditorToolsViewContractContainsOnlyGenerationActions()
    {
        string xaml = WorkbenchContractTestFiles.ReadSource("Views", "Pages", "UIKitEditorToolsView.axaml");

        Assert.Contains("uikit.editor.create-prefab", xaml, StringComparison.Ordinal);
        Assert.Contains("uikit.editor.generate-code", xaml, StringComparison.Ordinal);
        Assert.Contains("String.UIKit.Editor.CreatePrefab", xaml, StringComparison.Ordinal);
        Assert.Equal("创建预制体", WorkbenchI18nService.Instance.GetString("String.UIKit.Editor.CreatePrefab"));
        Assert.Contains("AssemblySelector", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{CompiledBinding AssemblyNames}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{CompiledBinding AssemblyName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{CompiledBinding CodeTemplateDisplay, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{CompiledBinding PrefabFolder, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{CompiledBinding ScriptFolder, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{CompiledBinding ScriptNamespace, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Classes=\"kit-panel uikit-editor-panel\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("uikit.editor.add-bind", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("uikit.editor.remove-bind", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("uikit.editor.save-settings", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("面板创建", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("uikit-editor-header", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Unity 当前选择", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("uikit-status-strip", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshEditorContextCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectionPanel", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Panel Prefab\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Create Prefab\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("WIP", xaml, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>渲染 Unity Editor Tools 宽屏界面，验证单一表单和操作按钮不为空。</summary>
    [Fact]
    public async Task EditorToolsRendersWideFormWithoutHorizontalOverflow()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        UIKitPageViewModel viewModel = new(
            null,
            (_, _, _) => Task.FromResult(new WorkbenchUIKitEditorResult
            {
                Succeeded = true,
                Context = CreateEditorContext(),
                Message = "已就绪",
            }));
        viewModel.SetEditorEngine("unity-editor");
        await viewModel.ShowEditorToolsTaskCommand.ExecuteAsync();
        await Dispatcher.UIThread.InvokeAsync(() => AssertEditorToolsWindow(viewModel));
    }

    /// <summary>验证宽屏和紧凑窗口都能渲染正确布局且没有横向滚动条。</summary>
    [Theory]
    [InlineData(1700, 1060, true)]
    [InlineData(1280, 820, false)]
    public async Task PageRendersWideAndCompactLayoutsWithoutHorizontalOverflow(
        double width,
        double height,
        bool expectWide)
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() => AssertAdaptiveLayout(width, height, expectWide));
    }

    /// <summary>在真实 Workbench Shell 中检查 UIKit 页面布局、列表和截图输出。</summary>
    private static void AssertAdaptiveLayout(double width, double height, bool expectWide)
    {
        WorkbenchShellViewModel viewModel = new(() => { }, _ => { }, (_, _) => Task.CompletedTask)
        {
            SelectedPage = "UIKit"
        };
        Assert.Equal("UIKit 运行时诊断", viewModel.CurrentPageTitle);
        viewModel.UIKitPage.ApplyPeriodicState(CreateState(18, 4, panelsTruncated: true));
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
            UIKitPageView page = Assert.Single(
                window.GetVisualDescendants().OfType<UIKitPageView>(),
                static item => item.IsVisible);
            Grid wide = page.FindControl<Grid>("UIKitWideLayout")!;
            Grid compact = page.FindControl<Grid>("UIKitCompactLayout")!;
            Assert.Equal(expectWide, wide.IsVisible);
            Assert.Equal(!expectWide, compact.IsVisible);
            Assert.Contains(
                page.GetVisualDescendants().OfType<ListBoxItem>(),
                static item => item.IsVisible);
            AssertNoHorizontalScrollBar(page);
            SaveFrame(window, width, height, expectWide ? "wide" : "compact");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>创建 Editor Tools 回读的最小强类型 context。</summary>
    private static WorkbenchUIKitEditorContext CreateEditorContext(
        string assemblyName = "Game.UI",
        IReadOnlyList<string>? assemblyNames = null,
        string codeTemplate = "Minimal",
        IReadOnlyList<string>? codeTemplateOptions = null)
    {
        return new WorkbenchUIKitEditorContext
        {
            Available = true,
            ContextRevision = 42L,
            ActiveGlobalObjectId = "GlobalObjectId_V1-test",
            SelectedAssetPath = "Assets/UI/Inventory.prefab",
            SelectedObjectName = "Inventory",
            SelectedGameObjectCount = 2,
            SelectedBindCount = 1,
            CanGenerateCode = true,
            Defaults = new WorkbenchUIKitPanelGenerationRequest
            {
                PrefabFolder = "Assets/UI",
                ScriptFolder = "Assets/Scripts/UI",
                ScriptNamespace = "Game.UI",
                AssemblyName = assemblyName,
                CodeTemplate = codeTemplate,
            },
            CodeTemplateOptions = codeTemplateOptions ?? new[] { "Default", "Minimal", "TeamTemplate" },
            AssemblyNames = assemblyNames ?? new[] { "Assembly-CSharp", "Game.UI" },
        };
    }

    /// <summary>创建返回固定 Provider 默认值并注入项目设置服务的 Editor Tools 页面。</summary>
    private static UIKitPageViewModel CreateEditorSettingsViewModel(
        UIKitEditorSettingsService service,
        string defaultAssemblyName = "Game.UI")
    {
        return new UIKitPageViewModel(
            null,
            (_, _, _) => Task.FromResult(new WorkbenchUIKitEditorResult
            {
                Succeeded = true,
                Context = CreateEditorContext(defaultAssemblyName),
                Message = "已就绪",
            }),
            service);
    }

    /// <summary>断言页面没有可见横向滚动条。</summary>
    private static void AssertNoHorizontalScrollBar(UIKitPageView page)
    {
        ScrollBar[] horizontal = page.GetVisualDescendants()
            .OfType<ScrollBar>()
            .Where(static item => item.IsVisible && item.Orientation == Orientation.Horizontal)
            .ToArray();
        Assert.Empty(horizontal);
    }

    /// <summary>在 Headless 视觉树中断言 Editor Tools 宽屏内容和截图。</summary>
    private static void AssertEditorToolsWindow(UIKitPageViewModel viewModel)
    {
        Window window = new()
        {
            Width = 1280,
            Height = 820,
            Content = new UIKitPageView { DataContext = viewModel },
        };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            UIKitEditorToolsView tools = Assert.Single(
                window.GetVisualDescendants().OfType<UIKitEditorToolsView>(),
                static item => item.IsVisible);
            Assert.NotNull(tools.FindControl<Button>("CreatePanelPrefabButton"));
            Assert.NotNull(tools.FindControl<Button>("GenerateCodeButton"));
            Assert.Null(tools.FindControl<Button>("SaveEditorSettingsButton"));
            Assert.Null(tools.FindControl<Button>("AddBindButton"));
            Assert.Null(tools.FindControl<Button>("RemoveBindButton"));
            ComboBox assemblySelector = tools.FindControl<ComboBox>("AssemblySelector")!;
            assemblySelector.SelectedItem = "Assembly-CSharp";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Assembly-CSharp", viewModel.AssemblyName);
            ComboBox templateSelector = tools.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(item => item != assemblySelector);
            templateSelector.SelectedItem = "默认";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Default", viewModel.CodeTemplate);
            AssertNoHorizontalScrollBar(tools);
            using WriteableBitmap? frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            string outputDirectory = Path.Combine(
                WorkbenchContractTestFiles.FindWorkbenchRoot(), ".artifacts", "screenshots", "workbench");
            Directory.CreateDirectory(outputDirectory);
            using FileStream stream = File.Create(Path.Combine(outputDirectory, "uikit-editor-tools-1280x820.png"));
            frame.Save(stream);
            Assert.True(stream.Length > 1024, "UIKit Editor Tools 截图内容为空或异常小。");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>断言指定 Editor Tools 视图没有可见横向滚动条。</summary>
    private static void AssertNoHorizontalScrollBar(UIKitEditorToolsView tools)
    {
        ScrollBar[] horizontal = tools.GetVisualDescendants()
            .OfType<ScrollBar>()
            .Where(static item => item.IsVisible && item.Orientation == Orientation.Horizontal)
            .ToArray();
        Assert.Empty(horizontal);
    }

    /// <summary>创建足量 UIKit 面板、命名栈、指标和覆盖率状态。</summary>
    private static WorkbenchUIKitState CreateState(
        int panelCount,
        int stackCount,
        bool panelsTruncated = false,
        bool stacksTruncated = false)
    {
        WorkbenchUIKitPanel[] panels = Enumerable.Range(0, panelCount)
            .Select(index => new WorkbenchUIKitPanel(
                "Game.UI.Panel" + index,
                "Panel" + index,
                index % 3 == 0 ? "Open" : index % 3 == 1 ? "Hide" : "Cached",
                index % 2 == 0 ? "Common" : "PopUI",
                index % 2 == 0 ? 20 : 30,
                index,
                index % 2 == 0 ? "Reusable" : "Persistent",
                index % 5 == 0,
                "stack-" + (index % Math.Max(1, stackCount))))
            .ToArray();
        WorkbenchUIKitStack[] stacks = Enumerable.Range(0, stackCount)
            .Select(index => new WorkbenchUIKitStack(
                "stack-" + index,
                index + 1,
                "Game.UI.Panel" + index,
                "Panel" + index))
            .ToArray();
        WorkbenchUIKitDataSource source = new(
            "unity-editor", "uikit-session", 4L, "PlayMode", DateTimeOffset.UtcNow,
            "telemetry", string.Empty, new[] { "Global\\YokiFrame.UIKit" }, string.Empty, "{}");
        return new WorkbenchUIKitState(
            source,
            1,
            new WorkbenchUIKitRoot(true),
            new WorkbenchUIKitStats(
                panelsTruncated ? panelCount + 2 : panelCount,
                stacksTruncated ? stackCount + 1 : stackCount,
                stackCount * 3,
                new WorkbenchUIKitPanelStates(1, 1, 5, 1, 4, 0, 3, 0)),
            new WorkbenchUIKitCache(24, 2, 9, 3, 7),
            new WorkbenchUIKitModal(true, 2),
            panels,
            stacks,
            panelsTruncated ? panelCount + 2 : panelCount,
            panelCount,
            panelsTruncated,
            stacksTruncated ? stackCount + 1 : stackCount,
            stackCount,
            stacksTruncated);
    }

    /// <summary>保存 Headless 截图并拒绝空白输出。</summary>
    private static void SaveFrame(Window window, double width, double height, string layout)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        string outputDirectory = Path.Combine(
            WorkbenchContractTestFiles.FindWorkbenchRoot(),
            ".artifacts",
            "screenshots",
            "workbench");
        Directory.CreateDirectory(outputDirectory);
        string fileName = "uikit-" + layout + "-" + (int)width + "x" + (int)height + ".png";
        using FileStream stream = new(
            Path.Combine(outputDirectory, fileName),
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read);
        frame.Save(stream);
        Assert.True(stream.Length > 1024, "UIKit Headless 截图内容为空或异常小。");
    }

}
