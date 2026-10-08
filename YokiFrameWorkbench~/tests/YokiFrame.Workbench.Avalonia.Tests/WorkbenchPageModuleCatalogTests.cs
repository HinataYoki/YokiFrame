using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Workbench.Avalonia.Pages;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;
/// <summary>
/// 覆盖 Workbench 编译期 Page Module Catalog 的唯一事实源和 section factory 契约。
/// </summary>
public sealed class WorkbenchPageModuleCatalogTests
{

    /// <summary>
    /// 验证默认 Catalog 使用 Framework Overview 作为稳定首屏。
    /// </summary>
    [Fact]
    public void DefaultCatalog_默认清单_首屏为Framework总览()
    {
        var catalog = WorkbenchDefaultPageModules.Catalog;
        Assert.Equal("Framework", catalog.DefaultPageName);
        Assert.Equal("Framework", catalog.DefaultModule.PageName);
        Assert.Equal("框架总览", catalog.DefaultModule.PageTitle);
        Assert.Equal("查看框架连接、引擎通信、AI Skills 与运行日志。", catalog.DefaultModule.Description);
        Assert.Equal(WorkbenchPagePresentation.Overview, catalog.DefaultModule.Presentation);
    }

    /// <summary>
    /// 验证 Catalog 拒绝重复页面名，避免导航和投影出现两个事实源。
    /// </summary>
    [Fact]
    public void WorkbenchPageModuleCatalog_重复页面名_抛出ArgumentException()
    {
        WorkbenchPageModule[] modules =
        {
            CreateModule("Framework"),
            CreateModule("Framework")
        };
        Assert.Throws<ArgumentException>(() => new WorkbenchPageModuleCatalog(modules, "Framework"));
    }

    /// <summary>
    /// 验证默认导航只公开框架、文档与已经落地且值得独立操作的 Kit 页面。
    /// </summary>
    [Fact]
    public void CreateNavigationGroups_默认清单_只公开已落地页面()
    {
        var groups = WorkbenchDefaultPageModules.Catalog.CreateNavigationGroups();
        Assert.Equal(new[] { "工作台", "Core", "Tools" }, groups.Select(static group => group.Title));
        Assert.Equal(
            new[] { "Framework", "Docs" },
            groups[0].Items.Select(static item => item.PageName));
        Assert.Equal(new[] { "EventKit", "FsmKit", "LogKit", "PoolKit", "ResKit" }, groups[1].Items.Select(static item => item.PageName));
        Assert.Equal(new[] { "ActionKit", "AudioKit", "SpatialKit", "UIKit", "TableKit", "LocalizationKit", "SaveKit" }, groups[2].Items.Select(static item => item.PageName));
        Assert.Equal(new[] { "framework", "docs", "eventkit", "fsm", "logkit", "poolkit", "reskit", "actionkit", "audiokit", "spatialkit", "uikit", "tablekit", "localization", "savekit" },
            groups.SelectMany(static group => group.Items).Select(static item => item.IconKey));
        Assert.DoesNotContain(
            groups.SelectMany(static group => group.Items),
            static item => item.PageName is "Doctor" or "Architecture" or "Automation");
    }

    /// <summary>
    /// 验证 UIKit 与 RoslynKit 是 Unity 专属页面，Godot engine 下不进入导航。
    /// </summary>
    [Fact]
    public void CreateLocalizedNavigationGroups_Godot引擎_隐藏Unity专属页面()
    {
        var groups = WorkbenchDefaultPageModules.Catalog.CreateLocalizedNavigationGroups(null, null, "godot-editor");
        var pageNames = groups.SelectMany(static group => group.Items).Select(static item => item.PageName);
        Assert.Equal(
            new[]
            {
                "Framework",
                "Docs",
                "EventKit",
                "FsmKit",
                "LogKit",
                "PoolKit",
                "ResKit",
                "ActionKit",
                "AudioKit",
                "SpatialKit",
                "TableKit",
                "LocalizationKit",
                "SaveKit"
            },
            pageNames);
    }

    /// <summary>
    /// 验证 Doctor module 使用诊断报告创建结构化段落，而不是落入通用 Kit missing 页面。
    /// </summary>
    [Fact]
    public void CreateSections_Doctor有诊断报告_输出Level和Issues()
    {
        var sections = WorkbenchDefaultPageModules.Catalog.GetRequired("Doctor").CreateSections(CreateDashboardState());
        var level = Assert.Single(sections, static section => section.Label == "Level");
        var issues = Assert.Single(sections, static section => section.Label == "Issues");
        Assert.Equal("Warning", level.Value);
        Assert.Equal("HeartbeatMissing | heartbeat missing | suggestion=restart adapter | evidence=F:/Project/.yokiframe/engines/unity-editor", issues.Value);
    }

    /// <summary>
    /// 验证 FsmKit module 使用专用页面呈现，不再落入通用 JSON 段落。
    /// </summary>
    [Fact]
    public void GetRequired_FsmKit_使用专用呈现()
    {
        var module = WorkbenchDefaultPageModules.Catalog.GetRequired("FsmKit");
        Assert.Equal(WorkbenchPagePresentation.FsmKit, module.Presentation);
    }

    /// <summary>
    /// 验证 PoolKit module 使用专用对象池监控页面，不回落通用 JSON 段落。
    /// </summary>
    [Fact]
    public void GetRequired_PoolKit_使用专用呈现()
    {
        var module = WorkbenchDefaultPageModules.Catalog.GetRequired("PoolKit");
        Assert.Equal(WorkbenchPagePresentation.PoolKit, module.Presentation);
    }

    /// <summary>
    /// 验证 ActionKit module 使用专用活动树页面，不回落通用 JSON 段落。
    /// </summary>
    [Fact]
    public void GetRequired_ActionKit_使用专用呈现()
    {
        var module = WorkbenchDefaultPageModules.Catalog.GetRequired("ActionKit");
        Assert.Equal(WorkbenchPagePresentation.ActionKit, module.Presentation);
    }

    /// <summary>
    /// 验证 Docs module 使用专用阅读器呈现，不再显示包路径占位段落。
    /// </summary>
    [Fact]
    public void GetRequired_Docs_使用文档呈现()
    {
        var module = WorkbenchDefaultPageModules.Catalog.GetRequired("Docs");
        Assert.Equal(WorkbenchPagePresentation.Documentation, module.Presentation);
    }

    /// <summary>
    /// 创建重复校验测试使用的最小页面模块。
    /// </summary>
    /// <param name="pageName">页面内部名称。</param>
    /// <returns>页面模块。</returns>

    private static WorkbenchPageModule CreateModule(string pageName)
    {
        return new WorkbenchPageModule(
            pageName,
            pageName,
            "Test",
            "#",
            WorkbenchPagePresentation.Detail,
            WorkbenchPageNavigationVisibility.Primary,
            static _ => Array.Empty<WorkbenchDisplaySection>());
    }

    /// <summary>
    /// 创建同时包含 Doctor issue 与 FsmKit telemetry snapshot 的 dashboard fixture。
    /// </summary>
    /// <returns>页面模块测试用 dashboard。</returns>

    private static WorkbenchDashboardState CreateDashboardState()
    {
        FileBridgeStatus status = new(
            "unity-editor",
            "F:/Project/.yokiframe/engines/unity-editor",
            "F:/Project/.yokiframe/engines/unity-editor/commands",
            "F:/Project/.yokiframe/engines/unity-editor/results");
        WorkbenchBridgeHealth health = new(
            WorkbenchBridgeConnectionState.Online,
            "online",
            "none",
            new[] { status.EngineRoot },
            1,
            15,
            "test",
            1,
            "EditMode",
            1);
        WorkbenchDoctorIssue issue = new(
            "HeartbeatMissing",
            "heartbeat missing",
            "restart adapter",
            new[] { status.EngineRoot });
        WorkbenchDoctorReport report = new(
            "unity-editor",
            DateTimeOffset.UtcNow,
            new[] { issue },
            status);
        WorkbenchSnapshotState snapshot = new(
            "FsmKit",
            "state",
            "F:/Project/fsm.json",
            "telemetry",
            true,
            "{\"current\":\"Idle\"}",
            string.Empty);
        return new WorkbenchDashboardState(
            "F:/Project",
            DateTimeOffset.UtcNow,
            Array.Empty<EngineRegistryEntry>(),
            "unity-editor",
            status,
            health,
            report,
            new[] { snapshot },
            "{}",
            Array.Empty<string>());
    }
}
