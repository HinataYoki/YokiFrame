using YokiFrame.Tooling.Application.Models;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Pages;

/// <summary>
/// 声明 Workbench 内置页面清单；新增页面只需在此显式注册一次。
/// </summary>
public static class WorkbenchDefaultPageModules
{
    /// <summary>
    /// 获取 Workbench 默认页面内部名称。
    /// </summary>
    public const string DEFAULT_PAGE_NAME = "Framework";

    /// <summary>
    /// 获取进程内共享的不可变默认页面 Catalog。
    /// </summary>
    public static WorkbenchPageModuleCatalog Catalog { get; } = CreateCatalog();

    /// <summary>
    /// 按导航顺序创建默认 Catalog，不执行反射扫描或运行时插件发现。
    /// </summary>
    /// <returns>默认页面 Catalog。</returns>
    private static WorkbenchPageModuleCatalog CreateCatalog()
    {
        WorkbenchPageModule[] modules =
        {
            CreateModule("Framework", "框架", "框架总览", "查看框架连接、引擎通信、AI Skills 与运行日志。", "工作台", "framework", WorkbenchPagePresentation.Overview, WorkbenchPageNavigationVisibility.Primary, WorkbenchPageSectionProjector.CreateFrameworkSections),
            CreateModule("Doctor", "诊断", "诊断", "检查宿主通信、协议状态与可操作诊断。", "工作台", "warning", WorkbenchPagePresentation.Detail, WorkbenchPageNavigationVisibility.Hidden, WorkbenchPageSectionProjector.CreateDoctorSections),
            CreateSpecializedModule("Docs", "文档", "文档", "浏览随 YokiFrame 包提供的离线文档与 API 参考。", "工作台", "docs", WorkbenchPagePresentation.Documentation, WorkbenchPageNavigationVisibility.Primary),
            CreateSpecializedModule("EventKit", "EventKit", "EventKit", "观察 Runtime 事件、活动监听与近期发送时间线。", "Core", "eventkit", WorkbenchPagePresentation.EventKit, WorkbenchPageNavigationVisibility.Primary),
            CreateSpecializedModule("FsmKit", "FsmKit", "FsmKit", "观察状态机实例、当前状态、转换历史与运行证据。", "Core", "fsm", WorkbenchPagePresentation.FsmKit, WorkbenchPageNavigationVisibility.Primary),
            CreateSpecializedModule("LogKit", "LogKit", "LogKit", "配置 Runtime 日志输出、等级、容量与文件策略。", "Core", "logkit", WorkbenchPagePresentation.LogKit, WorkbenchPageNavigationVisibility.Primary),
            CreateSpecializedModule("PoolKit", "PoolKit", "PoolKit 对象池监视", "对象池列表、借出对象、峰值和泄漏候选集中展示。", "Core", "poolkit", WorkbenchPagePresentation.PoolKit, WorkbenchPageNavigationVisibility.Primary),
            CreateSpecializedModule("ResKit", "ResKit", "ResKit 资源工作台", "观察 Provider、已加载资源、lease 来源与卸载历史。", "Core", "reskit", WorkbenchPagePresentation.ResKit, WorkbenchPageNavigationVisibility.Primary),
            CreateSpecializedModule("ActionKit", "ActionKit", "ActionKit 动作调度", "观察活动动作树、生命周期终态与按需调用堆栈。", "Tools", "actionkit", WorkbenchPagePresentation.ActionKit, WorkbenchPageNavigationVisibility.Primary),
            CreateSpecializedModule("AudioKit", "AudioKit", "AudioKit 观察器", "按 Bus 观察当前播放、进度与播放历史。", "Tools", "audiokit", WorkbenchPagePresentation.AudioKit, WorkbenchPageNavigationVisibility.Primary),
            CreateSpecializedModule("SpatialKit", "SpatialKit", "SpatialKit 空间索引", "查看运行中的索引实例、分区和单位疏密。", "Tools", "spatialkit", WorkbenchPagePresentation.SpatialKit, WorkbenchPageNavigationVisibility.Primary),
            CreateSpecializedModule("UIKit", "UIKit", "UIKit 运行时诊断", "观察 Unity 面板生命周期、命名栈、缓存和模态状态。", "Tools", "uikit", WorkbenchPagePresentation.UIKit, WorkbenchPageNavigationVisibility.Primary),
            CreateSpecializedModule("TableKit", "TableKit", "TableKit 数据生成", "读取 Luban 配置、验证 target 并一键生成表代码与数据。", "Tools", "tablekit", WorkbenchPagePresentation.TableKit, WorkbenchPageNavigationVisibility.Primary),
            CreateSpecializedModule("LocalizationKit", "LocalizationKit", "LocalizationKit 本地化", "预览、搜索本地化文本并检查语言缺失。", "Tools", "localization", WorkbenchPagePresentation.LocalizationKit, WorkbenchPageNavigationVisibility.Primary),
            CreateSpecializedModule("SaveKit", "SaveKit", "SaveKit 存档工作台", "配置存档目录、扩展名并浏览槽位与 Global 文件。", "Tools", "savekit", WorkbenchPagePresentation.SaveKit, WorkbenchPageNavigationVisibility.Primary),
        };
        return new WorkbenchPageModuleCatalog(modules, DEFAULT_PAGE_NAME);
    }

    /// <summary>
    /// 创建使用现成静态 section factory 的页面模块。
    /// </summary>
    /// <param name="pageName">页面内部名称。</param>
    /// <param name="displayName">页面显示名称。</param>
    /// <param name="pageTitle">紧凑页头标题。</param>
    /// <param name="description">页面功能的一句话介绍。</param>
    /// <param name="groupName">导航分组名称。</param>
    /// <param name="iconKey">导航矢量图标键。</param>
    /// <param name="presentation">页面呈现类型。</param>
    /// <param name="navigationVisibility">页面是否进入用户可见的一级导航。</param>
    /// <param name="sectionFactory">页面段落投影函数。</param>
    /// <returns>页面模块。</returns>
    private static WorkbenchPageModule CreateModule(
        string pageName,
        string displayName,
        string pageTitle,
        string description,
        string groupName,
        string iconKey,
        WorkbenchPagePresentation presentation,
        WorkbenchPageNavigationVisibility navigationVisibility,
        Func<WorkbenchDashboardState, IReadOnlyList<WorkbenchDisplaySection>> sectionFactory)
    {
        return new WorkbenchPageModule(
            pageName,
            displayName,
            groupName,
            iconKey,
            presentation,
            navigationVisibility,
            sectionFactory)
        {
            PageTitle = pageTitle,
            Description = description
        };
    }

    /// <summary>
    /// 创建由专用 ViewModel 和 XAML 承载的页面模块。
    /// </summary>
    /// <param name="pageName">页面内部名称。</param>
    /// <param name="displayName">页面显示名称。</param>
    /// <param name="pageTitle">紧凑页头标题。</param>
    /// <param name="description">页面功能的一句话介绍。</param>
    /// <param name="groupName">导航分组名称。</param>
    /// <param name="iconKey">导航矢量图标键。</param>
    /// <param name="presentation">专用页面呈现类型。</param>
    /// <param name="navigationVisibility">页面是否进入用户可见的一级导航。</param>
    /// <returns>专用页面模块。</returns>
    private static WorkbenchPageModule CreateSpecializedModule(
        string pageName,
        string displayName,
        string pageTitle,
        string description,
        string groupName,
        string iconKey,
        WorkbenchPagePresentation presentation,
        WorkbenchPageNavigationVisibility navigationVisibility)
    {
        return new WorkbenchPageModule(
            pageName,
            displayName,
            groupName,
            iconKey,
            presentation,
            navigationVisibility,
            static _ => Array.Empty<WorkbenchDisplaySection>())
        {
            PageTitle = pageTitle,
            Description = description
        };
    }
}
