using System.Reflection;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖框架总览页的信息架构，避免 Workbench 回退成无功能的旧界面仿制。
/// </summary>
public sealed partial class WorkbenchShellOverviewLayoutTests
{
    /// <summary>
    /// 验证框架总览不再把 Kit 实时数据作为独立首屏卡片展示。
    /// </summary>
    [Fact]
    public void FrameworkOverviewMovesRealtimeDataOutOfDashboard()
    {
        var xaml = ReadWorkbenchShellViewXaml();

        Assert.DoesNotContain("实时数据", xaml);
        Assert.DoesNotContain("SnapshotCards", xaml);
        Assert.DoesNotContain("状态详情", xaml);
        Assert.Contains("CurrentSections", xaml);
        Assert.Contains("IsOverviewPage", xaml);
    }

    /// <summary>
    /// 验证框架总览围绕新版运行状态组织布局，而不是继续复制旧 Tauri 展示块。
    /// </summary>
    [Fact]
    public void FrameworkOverviewSurfacesOperationalSections()
    {
        var xaml = ReadWorkbenchShellViewXaml();

        Assert.Contains("CurrentPageTitle", xaml);
        Assert.Contains("Classes=\"page-header\"", xaml);
        Assert.Contains("CurrentPageDescription", xaml);
        Assert.Contains("运行状态", xaml);
        Assert.Contains("命令桥", xaml);
        Assert.Contains("安装 Skill", xaml);
        Assert.Contains("components:LogConsole", xaml);
        Assert.Contains("SummaryCards", xaml);
        Assert.Contains("EngineCards", xaml);
        Assert.Contains("SkillTargets", xaml);
        Assert.Contains("SkillOptions", xaml);
        Assert.Contains("SkillStatusCards", xaml);
        Assert.DoesNotContain("CommandTraceText", xaml);
        Assert.DoesNotContain("Native Debug Console", xaml);
        Assert.DoesNotContain("YokiFrame Kit 调试工作台", xaml);
    }

    /// <summary>
    /// 验证总览使用真实字号的非对称双栏网格，不再通过 Viewbox 缩小整页内容。
    /// </summary>
    [Fact]
    public void FrameworkOverviewUsesAsymmetricColumnsWithoutPageScaling()
    {
        var xaml = ReadWorkbenchShellViewXaml();

        Assert.Contains("ColumnDefinitions=\"*,420\"", xaml);
        Assert.DoesNotContain("MinWidth=\"1180\"", xaml);
        Assert.Contains("x:Name=\"OverviewDesignSurface\"", xaml);
        Assert.DoesNotContain("x:Name=\"OverviewScaleBox\"", xaml);
        Assert.DoesNotContain("<Viewbox", xaml);
        Assert.DoesNotContain("x:Name=\"OverviewScroll\"", xaml);
        Assert.DoesNotContain("ColumnDefinitions=\"1.35*,0.65*\"", xaml);
    }

    /// <summary>
    /// 验证框架总览按单页工作台布局分配高度，运行日志与命令桥不会被状态卡挤出首屏。
    /// </summary>
    [Fact]
    public void FrameworkOverviewFitsPrimaryWorkflowIntoOnePage()
    {
        var xaml = ReadWorkbenchShellViewXaml();
        var detailStart = xaml.IndexOf(
            "<Grid IsVisible=\"{CompiledBinding IsDetailPage}\"",
            StringComparison.Ordinal);
        var overviewXaml = detailStart > 0 ? xaml[..detailStart] : xaml;

        Assert.Contains("RowDefinitions=\"Auto,52,*\"", xaml);
        Assert.Contains("RowDefinitions=\"Auto,Auto,Auto,*,Auto\"", xaml);
        Assert.Contains("<UniformGrid Rows=\"1\" />", xaml);
        Assert.Contains("Classes=\"overview-metric-card\"", xaml);
        Assert.Contains("FontSize=\"{DynamicResource FontSize.Lg}\"", ReadMetricCardXaml());
        Assert.Contains("MaxLines=\"1\"", ReadMetricCardXaml());
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", ReadMetricCardXaml());
        Assert.DoesNotContain("<StackPanel Spacing=\"14\">", xaml);
        Assert.DoesNotContain("VerticalScrollBarVisibility=\"Auto\"", overviewXaml);
    }

    /// <summary>
    /// 验证命令桥卡片只承载真实链路验证入口，发送和响应明细统一沉到运行日志中。
    /// </summary>
    [Fact]
    public void FrameworkOverviewKeepsCommandBridgeAsVerificationPanel()
    {
        var xaml = ReadWorkbenchShellViewXaml();

        Assert.Contains("Text=\"命令桥\"", xaml);
        Assert.Contains("Content=\"Ping\"", xaml);
        Assert.Contains("Content=\"状态\"", xaml);
        Assert.Contains("Content=\"目录\"", xaml);
        Assert.Contains("PingCommand", xaml);
        Assert.Contains("BridgeStatusCommand", xaml);
        Assert.Contains("RefreshCommandCatalogCommand", xaml);
        Assert.DoesNotContain("最近命令", xaml);
        Assert.DoesNotContain("CommandGroups", xaml);
        Assert.DoesNotContain("CommandActions", xaml);
    }

    /// <summary>
    /// 验证框架总览把字体选择合并到紧凑控制带，并绑定为真实显示偏好。
    /// </summary>
    [Fact]
    public void FrameworkOverviewRestoresDisplayFontCard()
    {
        var xaml = ReadWorkbenchShellViewXaml();

        Assert.Contains("Text=\"字体\"", xaml);
        Assert.Contains("DisplayFontOptions", xaml);
        Assert.Contains("SelectedDisplayFontName", xaml);
        Assert.Contains("SelectedDisplayFontFamily", xaml);
    }

    /// <summary>
    /// 验证运行日志通过样式类区分发送、接收和错误消息，避免命令桥卡片承担结果展示。
    /// </summary>
    [Fact]
    public void FrameworkOverviewColorsCommandTrafficInLogConsole()
    {
        var logConsole = ReadLogConsoleXaml();
        var terminalStyles = ReadTerminalStylesXaml();

        Assert.Contains("Classes.outbound=\"{CompiledBinding IsOutbound}\"", logConsole);
        Assert.Contains("Classes.inbound=\"{CompiledBinding IsInbound}\"", logConsole);
        Assert.Contains("Classes.error=\"{CompiledBinding IsError}\"", logConsole);
        Assert.Contains("TextBlock.terminal-line.outbound", terminalStyles);
        Assert.Contains("TextBlock.terminal-line.inbound", terminalStyles);
        Assert.Contains("TextBlock.terminal-line.error", terminalStyles);
        Assert.Contains("FontSize.Md", terminalStyles);
    }

    /// <summary>
    /// 验证运行日志的复制和清空按钮绑定真实动作，不再只是静态按钮。
    /// </summary>
    [Fact]
    public void FrameworkOverviewBindsLogConsoleActions()
    {
        var logConsole = ReadLogConsoleXaml();
        var codeBehind = ReadLogConsoleCodeBehind();
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);

        viewModel.ShowCommandInFlight("System", "ping");
        var text = InvokeCreateLogClipboardText(viewModel);
        InvokeClearLogCommand(viewModel);

        Assert.Contains("Click=\"OnCopyLogsButtonClick\"", logConsole);
        Assert.Contains("Command=\"{CompiledBinding ClearLogCommand}\"", logConsole);
        Assert.Contains("Clipboard", codeBehind);
        Assert.Contains("catch (Exception exception)", codeBehind);
        Assert.Contains("ShowTransientError(\"复制日志失败:", codeBehind);
        Assert.Contains("System/ping", text);
        Assert.Empty(viewModel.LogLines);
    }

    /// <summary>
    /// 验证 ViewModel 为命令发送、响应和错误写入不同类型的运行日志。
    /// </summary>
    [Fact]
    public void FrameworkOverviewWritesDirectionalCommandLogLines()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);

        viewModel.ShowCommandInFlight("System", "ping");
        viewModel.ShowCommandResult(new WorkbenchCommandState("System", "ping", true, "Ok", "{\"pong\":true}", string.Empty));
        viewModel.ShowTransientError("bridge timeout");

        AssertLogLineKind(viewModel.LogLines[^3], "Outbound", "命令发送");
        AssertLogLineKind(viewModel.LogLines[^2], "Inbound", "命令接收");
        AssertLogLineKind(viewModel.LogLines[^1], "Error", "错误");
    }

    /// <summary>
    /// 验证 Skill 安装面板绑定真实目标和安装命令，避免恢复成静态说明块。
    /// </summary>
    [Fact]
    public void FrameworkOverviewRestoresCommandBackedSkillInstaller()
    {
        var xaml = ReadWorkbenchShellViewXaml();

        Assert.Contains("SkillTargets", xaml);
        Assert.Contains("SkillOptions", xaml);
        Assert.Contains("SelectCommand", xaml);
        Assert.Contains("InstallCommand", xaml);
        Assert.Contains("UninstallCommand", xaml);
        Assert.DoesNotContain("RefreshSkillStatusCommand", xaml);
        Assert.Contains("SkillInstallStatusText", xaml);
        Assert.Contains("SkillStatusCards", xaml);
        Assert.DoesNotContain("安装Skill", xaml);
        Assert.DoesNotContain("ItemsSource=\"{CompiledBinding SkillNames}\"", xaml);
    }

    /// <summary>
    /// 验证 Skill 面板使用摘要网格和全宽操作行组织信息，并提供自定义目录安装入口。
    /// </summary>
    [Fact]
    public void FrameworkOverviewUsesSkillSummaryGridAndOperationalTargetRows()
    {
        var xaml = ReadWorkbenchShellViewXaml();

        Assert.Contains("UniformGrid Columns=\"3\"", xaml);
        Assert.Contains("Classes=\"card skill-target-row\"", xaml);
        Assert.Contains("Classes.neutral=\"{CompiledBinding !IsInstalled}\"", xaml);
        Assert.Contains("CustomSkillPath", xaml);
        Assert.Contains("InstallCustomSkillCommand", xaml);
        Assert.Contains("UninstallCustomSkillCommand", xaml);
        Assert.DoesNotContain("WrapPanel", xaml);
        Assert.DoesNotContain("Width=\"168\"", xaml);
    }

    /// <summary>
    /// 验证自定义目录安装命令会调用 Installer.Core 真实写入当前选中的 Skill。
    /// </summary>
    [Fact]
    public void FrameworkOverviewInstallsSkillToCustomDirectory()
    {
        var projectRoot = CreateProjectWithPackagedSkill("yokiframe");
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);

        viewModel.UpdateDashboard(CreateDashboardState(projectRoot));
        SetPropertyValue(viewModel, "CustomSkillPath", "custom/skills");
        InvokeCommandProperty(viewModel, "InstallCustomSkillCommand");

        Assert.True(File.Exists(Path.Combine(projectRoot, "custom", "skills", "yokiframe", "SKILL.md")));
    }

    /// <summary>
    /// 验证 ViewModel 能把首批 snapshot 投影为总览卡片，供实时数据区域直接展示。
    /// </summary>
    [Fact]
    public void FrameworkOverviewProjectsSnapshotCardsFromDashboard()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);
        var property = typeof(WorkbenchShellViewModel).GetProperty("SnapshotCards", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        viewModel.UpdateDashboard(CreateDashboardState());
        var cards = Assert.IsAssignableFrom<IReadOnlyList<WorkbenchMetricCard>>(property.GetValue(viewModel));

        AssertMetricCard(cards, "EventKit", "snapshot", "available");
        AssertMetricCard(cards, "LogKit", "telemetry", "missing: stale");
    }

    /// <summary>
    /// 验证桥接状态卡片只保留框架总览首屏真正需要的紧凑诊断信息。
    /// </summary>
    [Fact]
    public void FrameworkOverviewProjectsTauriLikeEngineStatusCards()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);

        viewModel.UpdateDashboard(CreateDashboardState());

        Assert.InRange(viewModel.EngineCards.Count, 1, 4);
        Assert.Contains(viewModel.EngineCards, card => card.Title == "心跳");
        Assert.Contains(viewModel.EngineCards, card => card.Title == "命令");
        Assert.Contains(viewModel.EngineCards, card => card.Title == "事件");
        Assert.Contains(viewModel.EngineCards, card => card.Title == "背压");
        Assert.DoesNotContain(viewModel.EngineCards, card => card.Value.Contains("F:/Project", StringComparison.Ordinal));
        Assert.DoesNotContain(viewModel.EngineCards, card => card.Detail.Contains("F:/Project", StringComparison.Ordinal));
    }
}
