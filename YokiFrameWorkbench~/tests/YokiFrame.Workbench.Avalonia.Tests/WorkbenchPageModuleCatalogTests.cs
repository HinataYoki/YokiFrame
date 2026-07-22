using System.Reflection;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Workbench 编译期 Page Module Catalog 的唯一事实源和 section factory 契约。
/// </summary>
public sealed class WorkbenchPageModuleCatalogTests
{
    private const string PAGE_NAMESPACE = "YokiFrame.Workbench.Avalonia.Pages.";

    /// <summary>
    /// 验证默认 Catalog 使用 Framework Overview 作为稳定首屏。
    /// </summary>
    [Fact]
    public void DefaultCatalogUsesFrameworkOverviewAsDefaultPage()
    {
        var catalog = ReadDefaultCatalog();
        var defaultModule = ReadProperty<object>(catalog, "DefaultModule");

        Assert.Equal("Framework", ReadProperty<string>(catalog, "DefaultPageName"));
        Assert.Equal("Framework", ReadProperty<string>(defaultModule, "PageName"));
        Assert.Equal("框架总览", ReadProperty<string>(defaultModule, "PageTitle"));
        Assert.NotEmpty(ReadProperty<string>(defaultModule, "Description"));
        Assert.Equal("Overview", ReadProperty<object>(defaultModule, "Presentation").ToString());
    }

    /// <summary>
    /// 验证 Catalog 拒绝重复页面名，避免导航和投影出现两个事实源。
    /// </summary>
    [Fact]
    public void CatalogRejectsDuplicatePageNames()
    {
        var moduleType = ReadPageType("WorkbenchPageModule");
        var catalogType = ReadPageType("WorkbenchPageModuleCatalog");
        var modules = Array.CreateInstance(moduleType, 2);
        modules.SetValue(CreateModule(moduleType, "Framework"), 0);
        modules.SetValue(CreateModule(moduleType, "Framework"), 1);

        var exception = Assert.Throws<TargetInvocationException>(
            () => Activator.CreateInstance(catalogType, new object[] { modules, "Framework" }));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    /// <summary>
    /// 验证默认导航只公开框架、文档与已经落地且值得独立操作的 Kit 页面。
    /// </summary>
    [Fact]
    public void DefaultCatalogBuildsStableNavigationGroups()
    {
        var catalog = ReadDefaultCatalog();
        var groups = Invoke<IReadOnlyList<WorkbenchNavigationGroup>>(catalog, "CreateNavigationGroups");

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
    /// 验证 Doctor module 使用诊断报告创建结构化段落，而不是落入通用 Kit missing 页面。
    /// </summary>
    [Fact]
    public void DoctorModuleCreatesDiagnosticSections()
    {
        var module = GetRequiredModule("Doctor");

        var sections = Invoke<IReadOnlyList<WorkbenchDisplaySection>>(
            module,
            "CreateSections",
            CreateDashboardState());

        Assert.Contains(sections, static section => section.Label == "Level" && section.Value == "Warning");
        Assert.Contains(sections, static section => section.Label == "Issues" && section.Value.Contains("HeartbeatMissing", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证 FsmKit module 使用专用页面呈现，不再落入通用 JSON 段落。
    /// </summary>
    [Fact]
    public void FsmKitModuleUsesDedicatedPresentation()
    {
        var module = GetRequiredModule("FsmKit");

        Assert.Equal("FsmKit", ReadProperty<object>(module, "Presentation").ToString());
    }

    /// <summary>
    /// 验证 PoolKit module 使用专用对象池监控页面，不回落通用 JSON 段落。
    /// </summary>
    [Fact]
    public void PoolKitModuleUsesDedicatedPresentation()
    {
        var module = GetRequiredModule("PoolKit");

        Assert.Equal("PoolKit", ReadProperty<object>(module, "Presentation").ToString());
    }

    /// <summary>
    /// 验证 ActionKit module 使用专用活动树页面，不回落通用 JSON 段落。
    /// </summary>
    [Fact]
    public void ActionKitModuleUsesDedicatedPresentation()
    {
        var module = GetRequiredModule("ActionKit");

        Assert.Equal("ActionKit", ReadProperty<object>(module, "Presentation").ToString());
    }

    /// <summary>
    /// 验证 Docs module 使用专用阅读器呈现，不再显示包路径占位段落。
    /// </summary>
    [Fact]
    public void DocsModuleUsesDedicatedPresentation()
    {
        var module = GetRequiredModule("Docs");

        Assert.Equal("Documentation", ReadProperty<object>(module, "Presentation").ToString());
    }

    /// <summary>
    /// 读取默认模块 Catalog；类型或属性缺失时产生明确 RED 断言。
    /// </summary>
    /// <returns>默认 Catalog 对象。</returns>
    private static object ReadDefaultCatalog()
    {
        var type = ReadPageType("WorkbenchDefaultPageModules");
        var property = type.GetProperty("Catalog", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(property);
        return Assert.IsAssignableFrom<object>(property.GetValue(null));
    }

    /// <summary>
    /// 从默认 Catalog 获取指定页面模块。
    /// </summary>
    /// <param name="pageName">页面内部名称。</param>
    /// <returns>匹配模块。</returns>
    private static object GetRequiredModule(string pageName)
    {
        return Invoke<object>(ReadDefaultCatalog(), "GetRequired", pageName);
    }

    /// <summary>
    /// 创建重复校验测试使用的最小页面模块。
    /// </summary>
    /// <param name="moduleType">页面模块类型。</param>
    /// <param name="pageName">页面内部名称。</param>
    /// <returns>页面模块对象。</returns>
    private static object CreateModule(Type moduleType, string pageName)
    {
        var presentationType = ReadPageType("WorkbenchPagePresentation");
        var presentation = Enum.Parse(presentationType, "Detail");
        var navigationVisibilityType = ReadPageType("WorkbenchPageNavigationVisibility");
        var navigationVisibility = Enum.Parse(navigationVisibilityType, "Primary");
        Func<WorkbenchDashboardState, IReadOnlyList<WorkbenchDisplaySection>> factory =
            static _ => Array.Empty<WorkbenchDisplaySection>();
        return Assert.IsAssignableFrom<object>(Activator.CreateInstance(
            moduleType,
            pageName,
            pageName,
            "Test",
            "#",
            presentation,
            navigationVisibility,
            factory));
    }

    /// <summary>
    /// 从 Workbench 程序集读取 Pages 命名空间中的类型。
    /// </summary>
    /// <param name="typeName">不含命名空间的类型名。</param>
    /// <returns>页面类型。</returns>
    private static Type ReadPageType(string typeName)
    {
        var type = typeof(WorkbenchShellViewModel).Assembly.GetType(PAGE_NAMESPACE + typeName);

        Assert.NotNull(type);
        return type;
    }

    /// <summary>
    /// 调用目标对象的公开实例方法，并断言返回值类型。
    /// </summary>
    /// <typeparam name="T">预期返回类型。</typeparam>
    /// <param name="target">目标对象。</param>
    /// <param name="methodName">公开方法名。</param>
    /// <param name="arguments">方法参数。</param>
    /// <returns>方法返回值。</returns>
    private static T Invoke<T>(object target, string methodName, params object[] arguments)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        return Assert.IsAssignableFrom<T>(method.Invoke(target, arguments));
    }

    /// <summary>
    /// 读取目标对象的公开属性，并断言属性值类型。
    /// </summary>
    /// <typeparam name="T">预期属性类型。</typeparam>
    /// <param name="target">目标对象。</param>
    /// <param name="propertyName">属性名。</param>
    /// <returns>属性值。</returns>
    private static T ReadProperty<T>(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        return Assert.IsAssignableFrom<T>(property.GetValue(target));
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
