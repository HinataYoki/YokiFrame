using System.Globalization;
using YokiFrame.Workbench.Avalonia;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Workbench Shell 各自独立的页面目录、窗口策略、选择状态、空 dashboard 占位卡片和命令目录契约。
/// </summary>
public sealed partial class WorkbenchShellViewTests
{
    /// <summary>
    /// 验证 PageNames 只按 Catalog 顺序返回已实现页面。隐藏的 Doctor 仍在目录中；默认页和一级导航可见性不在本契约内。
    /// </summary>
    [Fact]
    public void PageNames_默认目录_只返回已实现页面()
    {
        Assert.Equal(
            new[]
            {
                "Framework",
                "Doctor",
                "Docs",
                "EventKit",
                "FsmKit",
                "LogKit",
                "PoolKit",
                "ResKit",
                "ActionKit",
                "AudioKit",
                "SpatialKit",
                "UIKit",
                "TableKit",
                "LocalizationKit",
                "SaveKit"
            },
            WorkbenchShellViewModel.PageNames);
    }

    /// <summary>
    /// 验证默认窗口尺寸契约为宽 1700、高 1060。不启动窗口，只锁定这两个公开默认值。
    /// </summary>
    [Fact]
    public void DefaultWindowWidth_默认窗口策略_使用1700x1060宽屏()
    {
        Assert.Equal(1700, WorkbenchWindow.DefaultWindowWidth);
        Assert.Equal(1060, WorkbenchWindow.DefaultWindowHeight);
    }

    /// <summary>
    /// 验证默认窗口边框契约：不进任务栏、不可最小化、可调整大小、客户区延伸到装饰，并且装饰仅为边框。
    /// </summary>
    [Fact]
    public void DefaultShowInTaskbar_默认窗口策略_使用沉浸式工具窗口边框()
    {
        Assert.False(WorkbenchWindow.DefaultShowInTaskbar);
        Assert.False(WorkbenchWindow.DefaultCanMinimize);
        Assert.True(WorkbenchWindow.DefaultCanResize);
        Assert.True(WorkbenchWindow.DefaultExtendClientAreaToDecorations);
        Assert.Equal(global::Avalonia.Controls.WindowDecorations.BorderOnly, WorkbenchWindow.DefaultWindowDecorations);
    }

    /// <summary>
    /// 验证默认窗口不绘制原生标题，避免与左上角品牌区重叠。
    /// </summary>
    [Fact]
    public void DefaultWindowTitle_默认窗口策略_不绘制原生标题()
    {
        var title = WorkbenchWindow.DefaultWindowTitle;
        Assert.Equal(string.Empty, title);
    }

    /// <summary>
    /// 验证 owner 绑定后的 Win32 样式契约。输入必须带子窗口位，才能证明该位被清除，而不是原本就不存在。
    /// 同时证明最小化被清除、顶层框架保留，并且扩展样式改为工具窗口。计算走 Sources 的 internal 测试缝，不反射 private。
    /// </summary>
    [Fact]
    public void CreateOwnedWindowStyle_当前样式含子窗口位_清除子窗口和最小化并保留顶层框架()
    {
        var currentStyle = WS_POPUP | WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_CHILD;
        var nextStyle = InvokeCreateOwnedWindowStyle(currentStyle);
        var nextExStyle = InvokeCreateOwnedToolWindowExStyle(WS_EX_APPWINDOW);

        Assert.Equal(0, nextStyle & WS_CHILD);
        Assert.Equal(WS_POPUP, nextStyle & WS_POPUP);
        Assert.Equal(WS_CAPTION, nextStyle & WS_CAPTION);
        Assert.Equal(WS_THICKFRAME, nextStyle & WS_THICKFRAME);
        Assert.Equal(WS_SYSMENU, nextStyle & WS_SYSMENU);
        Assert.Equal(0, nextStyle & WS_MINIMIZEBOX);
        Assert.Equal(WS_MAXIMIZEBOX, nextStyle & WS_MAXIMIZEBOX);
        Assert.Equal(0, nextExStyle & WS_EX_APPWINDOW);
        Assert.Equal(WS_EX_TOOLWINDOW, nextExStyle & WS_EX_TOOLWINDOW);
    }

    /// <summary>
    /// 验证应用默认主题契约为暗色，不跟随系统浅色主题。
    /// </summary>
    [Fact]
    public void DefaultThemeVariant_默认应用_强制暗色主题()
    {
        Assert.Equal(global::Avalonia.Styling.ThemeVariant.Dark, WorkbenchApp.DefaultThemeVariant);
    }

    /// <summary>
    /// 验证选择已实现页面后，SelectedPage 与当前页标题都落到该页。不验证专用页呈现标志。
    /// 标题经 WorkbenchI18nService 投影，因此先固定 zh-CN。finally 释放 ViewModel，并恢复进入前的线程文化与 Workbench 语言。
    /// </summary>
    [Fact]
    public void SelectedPage_选择已实现页面_同步当前页标题()
    {
        var previous = PinZhCnCulture();
        WorkbenchShellViewModel? viewModel = null;
        try
        {
            viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);
            viewModel.SelectedPage = "FsmKit";

            Assert.Equal("FsmKit", viewModel.SelectedPage);
            Assert.Equal("FsmKit", viewModel.CurrentPageTitle);
        }
        finally
        {
            viewModel?.Dispose();
            RestoreCulture(previous);
        }
    }

    /// <summary>
    /// 验证未加载 dashboard 时，除连接卡以外的占位卡片与 Overview 源码一致。连接卡由相邻测试单独锁定，避免重复证明同一字段。
    /// 中文标题依赖 WorkbenchI18nService，因此先固定 zh-CN。finally 释放 ViewModel，并恢复进入前的线程文化与 Workbench 语言。
    /// </summary>
    [Fact]
    public void SummaryCards_空dashboard_非连接占位卡片保持固定文案()
    {
        var previous = PinZhCnCulture();
        WorkbenchShellViewModel? viewModel = null;
        try
        {
            viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);

            Assert.Equal(4, viewModel.SummaryCards.Count);
            AssertMetricCard(viewModel.SummaryCards[1], "引擎", "等待发现", "0 registry");
            AssertMetricCard(viewModel.SummaryCards[2], "队列", "0 / 0", "results 0 / deadletter 0");
            AssertMetricCard(viewModel.SummaryCards[3], "最近问题", "--", "等待桥接状态");
            Assert.Equal(4, viewModel.EngineCards.Count);
            AssertMetricCard(viewModel.EngineCards[0], "心跳", "waiting", "status/heartbeat.json");
            AssertMetricCard(viewModel.EngineCards[1], "命令", "commands", "engine-scoped");
            AssertMetricCard(viewModel.EngineCards[2], "事件", "JSONL", "protocol 0 files / 0 bytes");
            AssertMetricCard(viewModel.EngineCards[3], "背压", "Idle", "BridgeBusy 0", isPositive: true);
            var snapshot = Assert.Single(viewModel.SnapshotCards);
            AssertMetricCard(snapshot, "实时数据", "waiting", "等待 dashboard");
        }
        finally
        {
            viewModel?.Dispose();
            RestoreCulture(previous);
        }
    }

    /// <summary>
    /// 验证空 dashboard 的连接卡使用稳定 FileBridge 名称，协议代际不进入展示名。
    /// 只断言这张卡的强类型字段。finally 释放 ViewModel，并恢复进入前的线程文化与 Workbench 语言。
    /// </summary>
    [Fact]
    public void SummaryCards_空dashboard_连接详情固定为FileBridge()
    {
        var previous = PinZhCnCulture();
        WorkbenchShellViewModel? viewModel = null;
        try
        {
            viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);

            AssertMetricCard(viewModel.SummaryCards[0], "连接", "等待数据", "FileBridge");
        }
        finally
        {
            viewModel?.Dispose();
            RestoreCulture(previous);
        }
    }

    /// <summary>
    /// 验证多 Kit 的 list_commands 结果会更新对应命令选项。JSON 只作为公开方法输入，断言的是分组和 action 的精确序列，不扫描 JSON 文本。
    /// finally 释放 ViewModel，并恢复进入前的线程文化与 Workbench 语言。
    /// </summary>
    [Fact]
    public void UpdateCommandCatalogJson_目录包含多个Kit_更新对应命令选项()
    {
        var previous = PinZhCnCulture();
        WorkbenchShellViewModel? viewModel = null;
        try
        {
            viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);
            var catalogJson = "{\"kits\":[{\"kit\":\"System\",\"actions\":[{\"action\":\"ping\"},{\"action\":\"list_commands\"}]},{\"kit\":\"EventKit\",\"actions\":[{\"action\":\"get_workbench_snapshot\"}]}]}";

            viewModel.UpdateCommandCatalogJson(catalogJson);

            Assert.Equal(new[] { "System", "EventKit" }, viewModel.CommandGroups);
            Assert.Equal(new[] { "ping", "list_commands" }, viewModel.CommandActions);
            viewModel.CommandGroup = "EventKit";
            Assert.Equal(new[] { "get_workbench_snapshot" }, viewModel.CommandActions);
        }
        finally
        {
            viewModel?.Dispose();
            RestoreCulture(previous);
        }
    }

    /// <summary>
    /// 将线程文化与 Workbench 语言固定为 zh-CN。卡片中文来自 WorkbenchI18nService，只改 CultureInfo 不能锁定文案。
    /// </summary>
    /// <returns>进入测试前的线程文化、UI 文化和 Workbench 语言名。</returns>
    private static (CultureInfo Culture, CultureInfo UiCulture, string I18nCulture) PinZhCnCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        var previousI18nCulture = WorkbenchI18nService.Instance.CurrentCultureName;
        CultureInfo.CurrentCulture = new CultureInfo("zh-CN");
        CultureInfo.CurrentUICulture = new CultureInfo("zh-CN");
        WorkbenchI18nService.Instance.SetCulture("zh-CN");
        return (previousCulture, previousUiCulture, previousI18nCulture);
    }

    /// <summary>
    /// 恢复进入测试前的线程文化与 Workbench 语言。必须在释放 ViewModel 之后调用，避免语言事件打到已释放对象。
    /// </summary>
    /// <param name="previous">PinZhCnCulture 返回的进入前文化。</param>
    private static void RestoreCulture((CultureInfo Culture, CultureInfo UiCulture, string I18nCulture) previous)
    {
        CultureInfo.CurrentCulture = previous.Culture;
        CultureInfo.CurrentUICulture = previous.UiCulture;
        WorkbenchI18nService.Instance.SetCulture(previous.I18nCulture);
    }

    /// <summary>
    /// 按强类型字段断言一张状态卡片，避免用数量下界或字符串搜索代替契约。
    /// </summary>
    /// <param name="card">实际卡片。</param>
    /// <param name="title">期望标题。</param>
    /// <param name="value">期望主值。</param>
    /// <param name="detail">期望详情。</param>
    /// <param name="isPositive">期望健康态。</param>
    /// <param name="isAccent">期望强调态。</param>
    private static void AssertMetricCard(
        WorkbenchMetricCard card,
        string title,
        string value,
        string detail,
        bool isPositive = false,
        bool isAccent = false)
    {
        Assert.Equal(title, card.Title);
        Assert.Equal(value, card.Value);
        Assert.Equal(detail, card.Detail);
        Assert.Equal(isPositive, card.IsPositive);
        Assert.Equal(isAccent, card.IsAccent);
    }
}