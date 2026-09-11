using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xunit;
using YokiFrame.Tooling.Application.Services;
using YokiFrame.Workbench.Avalonia.Components;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 验证 Workbench 多语言服务、资源字典对齐与响应式切换逻辑。
/// </summary>
public sealed class WorkbenchI18nServiceTests
{
    [Fact]
    public void CultureOptions_ContainsSupportedLanguages()
    {
        var service = WorkbenchI18nService.Instance;
        Assert.Contains("中文", service.CultureOptions);
        Assert.Contains("English", service.CultureOptions);
    }

    /// <summary>
    /// 未知显示名称或文化名称不得静默改变当前语言，避免输入错误触发意外的资源重投影。
    /// </summary>
    [Fact]
    public void UnknownCulture_IsRejectedWithoutChangingCurrentCulture()
    {
        var service = WorkbenchI18nService.Instance;
        service.SetCulture("en-US");

        Assert.False(service.SetCultureByDisplayName("日本語"));
        Assert.False(service.SetCulture("ja-JP"));
        Assert.Equal("en-US", service.CurrentCultureName);

        service.SetCulture("zh-CN");
    }

    [Fact]
    public async Task SetCultureByDisplayName_SwitchesCultureAndFiresEvent()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var service = WorkbenchI18nService.Instance;
            var eventFired = false;
            void OnCultureChanged() => eventFired = true;

            service.CultureChanged += OnCultureChanged;
            try
            {
                service.SetCultureByDisplayName("English");
                Assert.Equal("en-US", service.CurrentCultureName);
                Assert.Equal("English", service.CurrentCultureDisplayName);
                Assert.True(eventFired);

                eventFired = false;
                service.SetCultureByDisplayName("中文");
                Assert.Equal("zh-CN", service.CurrentCultureName);
                Assert.Equal("中文", service.CurrentCultureDisplayName);
                Assert.True(eventFired);
            }
            finally
            {
                service.CultureChanged -= OnCultureChanged;
                service.SetCultureByDisplayName("中文");
            }
        });
    }

    /// <summary>
    /// 验证启动后 Application 资源已注入当前语言字符串表。
    /// 字符串表迁出 axaml 后，ApplyCurrentCulture 是 XAML DynamicResource 的唯一注入点，
    /// 该调用缺失会让全部界面文案退化为资源键。
    /// </summary>
    [Fact]
    public async Task ApplicationResources_ArePopulatedAtStartup()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var service = WorkbenchI18nService.Instance;
            service.SetCulture("zh-CN");

            var resources = Application.Current?.Resources;
            Assert.NotNull(resources);

            Assert.True(resources!.TryGetResource("String.Nav.Workspace", null, out var zhValue));
            Assert.Equal("工作台", Assert.IsType<string>(zhValue));

            // 语言切换后必须重新注入，否则 DynamicResource 仍停留在旧语言。
            service.SetCulture("en-US");
            Assert.True(resources.TryGetResource("String.Nav.Workspace", null, out var enValue));
            Assert.Equal("Workspace", Assert.IsType<string>(enValue));

            service.SetCulture("zh-CN");
        });
    }

    /// <summary>
    /// 验证 zh/en 两份字符串表键集对齐；表已从 axaml 迁到 C#，此处直接比较两份表源码。
    /// </summary>
    [Fact]
    public void StringTables_ZhAndEnKeysAreAligned()
    {
        var zhSource = WorkbenchContractTestFiles.ReadSource("Services", "WorkbenchI18nService.Strings.Zh.cs");
        var enSource = WorkbenchContractTestFiles.ReadSource("Services", "WorkbenchI18nService.Strings.En.cs");

        var zhKeys = ExtractStringKeys(zhSource);
        var enKeys = ExtractStringKeys(enSource);

        Assert.NotEmpty(zhKeys);
        Assert.NotEmpty(enKeys);
        Assert.Equal(zhKeys, enKeys);
    }

    /// <summary>从字符串表源码提取按序排列的资源键。</summary>
    /// <param name="source">字符串表源码文本。</param>
    /// <returns>按序排列的资源键。</returns>
    private static string[] ExtractStringKeys(string source)
    {
        return System.Text.RegularExpressions.Regex
            .Matches(source, "\\[\"(String\\.[^\"]+)\"\\]\\s*=")
            .Select(static match => match.Groups[1].Value)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();
    }

    [Fact]
    public async Task WorkbenchShellViewModel_CultureText_TogglesLanguage()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var service = WorkbenchI18nService.Instance;
            service.SetCultureByDisplayName("中文");

            var viewModel = new WorkbenchShellViewModel(
                () => { },
                _ => { },
                (_, _) => Task.CompletedTask);
            try
            {
                Assert.Equal("中文", viewModel.CultureText);
                Assert.Contains(viewModel.NavigationGroups, g => g.Title == "工作台");

                viewModel.CultureText = "English";
                Assert.Equal("English", viewModel.CultureText);
                Assert.Equal("en-US", service.CurrentCultureName);
                Assert.Contains(viewModel.NavigationGroups, g => g.Title == "Workspace");

                // 切回默认
                viewModel.CultureText = "中文";
                Assert.Equal("中文", viewModel.CultureText);
                Assert.Contains(viewModel.NavigationGroups, g => g.Title == "工作台");
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// 页面释放后不应再收到静态语言服务事件，防止关闭窗口后的幽灵通知。
    /// </summary>
    [Fact]
    public async Task UIKitPageViewModel_DisposeDetachesCultureSubscription()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var service = WorkbenchI18nService.Instance;
            service.SetCulture("zh-CN");
            var viewModel = new UIKitPageViewModel();
            var notificationCount = 0;
            viewModel.PropertyChanged += (_, _) => notificationCount++;

            viewModel.Dispose();
            service.SetCulture("en-US");

            Assert.Equal(0, notificationCount);
            service.SetCulture("zh-CN");
        });
    }

    /// <summary>
    /// 验证 UIKit 无 Runtime 数据时的来源占位文本随当前语言初始化、重置和切换。
    /// </summary>
    [Fact]
    public async Task UIKitPageViewModel_LocalizesWaitingSource()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var service = WorkbenchI18nService.Instance;
            service.SetCulture("en-US");
            var viewModel = new UIKitPageViewModel();
            try
            {
                Assert.Equal("Waiting for data", viewModel.Source);

                viewModel.ApplyPeriodicState(null);
                Assert.Equal("Waiting for data", viewModel.Source);

                service.SetCulture("zh-CN");
                Assert.Equal("等待数据", viewModel.Source);
            }
            finally
            {
                viewModel.Dispose();
                service.SetCulture("zh-CN");
            }
        });
    }

    /// <summary>
    /// 验证 Shell 释放后不再响应静态语言事件，避免关闭窗口后的幽灵布局刷新。
    /// </summary>
    [Fact]
    public async Task WorkbenchShellViewModel_DisposeDetachesCultureSubscription()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var service = WorkbenchI18nService.Instance;
            service.SetCulture("zh-CN");
            var viewModel = new WorkbenchShellViewModel(
                () => { },
                _ => { },
                (_, _) => Task.CompletedTask);
            var notificationCount = 0;
            viewModel.PropertyChanged += (_, _) => notificationCount++;

            viewModel.Dispose();
            service.SetCulture("en-US");

            Assert.Equal(0, notificationCount);
            service.SetCulture("zh-CN");
        });
    }

    [Fact]
    public async Task WorkbenchWindow_AppTitleBar_ComboBox_DisplaysCultureDisplayName()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var service = WorkbenchI18nService.Instance;
            service.SetCultureByDisplayName("中文");
            var projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-titlebar-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectRoot);
            var options = new ToolStartupOptions(ToolStartupMode.Workbench, projectRoot, projectRoot, projectRoot);
            var window = new WorkbenchWindow(new WorkbenchDashboardService(projectRoot), options);
            try
            {
                window.Show();

                var titleBar = window.FindDescendantOfType<AppTitleBar>();
                Assert.NotNull(titleBar);

                var comboBox = titleBar.FindDescendantOfType<ComboBox>();
                Assert.NotNull(comboBox);

                Assert.Equal("中文", comboBox.SelectedItem);
                var items = comboBox.ItemsSource?.Cast<object>().ToArray();
                Assert.NotNull(items);
                Assert.Equal(new object[] { "中文", "English" }, items);
            }
            finally
            {
                window.Close();
                service.SetCultureByDisplayName("中文");
            }
        });
    }
}
