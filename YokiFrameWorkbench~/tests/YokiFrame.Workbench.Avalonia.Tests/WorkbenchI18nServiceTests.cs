using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
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

    [Fact]
    public void ResourceDictionaries_ZhAndEnKeysAreAligned()
    {
        var zhContent = WorkbenchContractTestFiles.ReadSource("Resources", "I18n", "Strings.zh-CN.axaml");
        var enContent = WorkbenchContractTestFiles.ReadSource("Resources", "I18n", "Strings.en-US.axaml");

        Assert.False(string.IsNullOrWhiteSpace(zhContent), "Strings.zh-CN.axaml 内容不能为空");
        Assert.False(string.IsNullOrWhiteSpace(enContent), "Strings.en-US.axaml 内容不能为空");

        var zhDoc = XDocument.Parse(zhContent);
        var enDoc = XDocument.Parse(enContent);

        XNamespace xNs = "http://schemas.microsoft.com/winfx/2006/xaml";
        var zhKeys = zhDoc.Descendants(xNs + "String")
            .Select(el => el.Attribute(xNs + "Key")?.Value)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .OrderBy(k => k)
            .ToArray();

        var enKeys = enDoc.Descendants(xNs + "String")
            .Select(el => el.Attribute(xNs + "Key")?.Value)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .OrderBy(k => k)
            .ToArray();

        Assert.NotEmpty(zhKeys);
        Assert.NotEmpty(enKeys);
        Assert.Equal(zhKeys, enKeys);
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
