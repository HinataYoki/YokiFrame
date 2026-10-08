using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Threading;
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
    /// <summary>
    /// 语言选项只暴露中文和英文，避免多出一个未实现文化仍能通过包含断言。
    /// </summary>
    [Fact]
    public void CultureOptions_默认服务_只提供中文和英文()
    {
        var service = WorkbenchI18nService.Instance;
        Assert.Equal(new[] { "中文", "English" }, service.CultureOptions);
    }

    /// <summary>
    /// 未知显示名称或文化名称不得静默改变当前语言，避免输入错误触发意外的资源重投影。
    /// </summary>
    [Fact]
    public void SetCulture_未知文化_拒绝且不改变当前文化()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        Dispatcher.UIThread.Invoke(() =>
        {
            var service = WorkbenchI18nService.Instance;
            service.SetCulture("zh-CN");
            try
            {
                service.SetCulture("en-US");
                Assert.False(service.SetCultureByDisplayName("日本語"));
                Assert.False(service.SetCulture("ja-JP"));
                Assert.Equal("en-US", service.CurrentCultureName);
            }
            finally
            {
                service.SetCulture("zh-CN");
            }
        });
    }

    /// <summary>
    /// 已知显示名必须切换当前文化，并在每次成功切换时触发文化事件。
    /// </summary>
    [Fact]
    public async Task SetCultureByDisplayName_已知显示名_切换文化并触发事件()
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
                service.SetCulture("zh-CN");
            }
        });
    }

    /// <summary>
    /// 验证启动后 Application 资源已注入当前语言字符串表。
    /// 字符串表迁出 axaml 后，ApplyCurrentCulture 是 XAML DynamicResource 的唯一注入点，
    /// 该调用缺失会让全部界面文案退化为资源键。
    /// </summary>
    [Fact]
    public async Task SetCulture_启动后切换语言_重新注入资源表()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var service = WorkbenchI18nService.Instance;
            service.SetCulture("zh-CN");
            try
            {
                var resources = Application.Current?.Resources;
                Assert.NotNull(resources);

                Assert.True(resources!.TryGetResource("String.Nav.Workspace", null, out var zhValue));
                Assert.Equal("工作台", Assert.IsType<string>(zhValue));

                service.SetCulture("en-US");
                Assert.True(resources.TryGetResource("String.Nav.Workspace", null, out var enValue));
                Assert.Equal("Workspace", Assert.IsType<string>(enValue));
            }
            finally
            {
                service.SetCulture("zh-CN");
            }
        });
    }

    /// <summary>
    /// Shell 文化显示名切换后，导航分组标题必须跟随当前语言重投影。
    /// </summary>
    [Fact]
    public async Task CultureText_切换显示名_同步导航标题()
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
                Assert.Contains(viewModel.NavigationGroups, static group => group.Title == "工作台");

                viewModel.CultureText = "English";
                Assert.Equal("English", viewModel.CultureText);
                Assert.Equal("en-US", service.CurrentCultureName);
                Assert.Contains(viewModel.NavigationGroups, static group => group.Title == "Workspace");

                viewModel.CultureText = "中文";
                Assert.Equal("中文", viewModel.CultureText);
                Assert.Contains(viewModel.NavigationGroups, static group => group.Title == "工作台");
            }
            finally
            {
                viewModel.Dispose();
                service.SetCulture("zh-CN");
            }
        });
    }

    /// <summary>
    /// 页面释放后不应再收到静态语言服务事件，防止关闭窗口后的幽灵通知。
    /// </summary>
    [Fact]
    public async Task Dispose_已释放UIKit页_不再接收文化事件()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var service = WorkbenchI18nService.Instance;
            service.SetCulture("zh-CN");
            var viewModel = new UIKitPageViewModel();
            try
            {
                var notificationCount = 0;
                viewModel.PropertyChanged += (_, _) => notificationCount++;

                viewModel.Dispose();
                service.SetCulture("en-US");

                Assert.Equal(0, notificationCount);
            }
            finally
            {
                service.SetCulture("zh-CN");
            }
        });
    }

    /// <summary>
    /// 验证 UIKit 无 Runtime 数据时的来源占位文本随当前语言初始化、重置和切换。
    /// </summary>
    [Fact]
    public async Task Source_无运行时数据_跟随当前语言()
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
    public async Task Dispose_已释放Shell_不再接收文化事件()
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
            try
            {
                var notificationCount = 0;
                viewModel.PropertyChanged += (_, _) => notificationCount++;

                viewModel.Dispose();
                service.SetCulture("en-US");

                Assert.Equal(0, notificationCount);
            }
            finally
            {
                service.SetCulture("zh-CN");
            }
        });
    }

    /// <summary>
    /// 标题栏文化组合框显示当前文化名称，并列出全部支持的显示名。测试结束恢复文化和临时目录。
    /// </summary>
    [Fact]
    public async Task AppTitleBar_当前为中文_组合框显示文化名称()
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
                service.SetCulture("zh-CN");
                if (Directory.Exists(projectRoot))
                {
                    Directory.Delete(projectRoot, recursive: true);
                }
            }
        });
    }
}
