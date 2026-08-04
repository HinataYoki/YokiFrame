using YokiFrame.Tooling.Application.Models;
using YokiFrame.Workbench.Avalonia.Pages;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 负责 Workbench Shell 的页面选择、Catalog 查找和 Overview/Detail 呈现状态。
/// </summary>
public sealed partial class WorkbenchShellViewModel
{
    /// <summary>
    /// 获取 Workbench 打开后的默认页面名称。
    /// </summary>
    public const string DefaultPageName = WorkbenchDefaultPageModules.DEFAULT_PAGE_NAME;

    private static readonly WorkbenchPageModuleCatalog sPageCatalog = WorkbenchDefaultPageModules.Catalog;
    private IReadOnlyList<WorkbenchDisplaySection> mCurrentSections = Array.Empty<WorkbenchDisplaySection>();
    private string mCurrentPageTitle = sPageCatalog.DefaultModule.PageTitle;
    private string mCurrentPageDescription = sPageCatalog.DefaultModule.Description;
    private string mSelectedPage = DefaultPageName;
    private object? mActiveWorkspacePage;
    private bool mIsOverviewPage = true;
    private bool mIsDetailPage;
    private bool mIsEventKitPage;
    private bool mIsFsmKitPage;
    private bool mIsLogKitPage;
    private bool mIsPoolKitPage;
    private bool mIsResKitPage;
    private bool mIsActionKitPage;
    private bool mIsAudioKitPage;
    private bool mIsSpatialKitPage;
    private bool mIsUIKitPage;
    private bool mIsTableKitPage;
    private bool mIsLocalizationKitPage;
    private bool mIsSaveKitPage;
    private bool mIsDocumentationPage;

    /// <summary>
    /// 获取当前专用工作台页面的数据上下文；总览和通用详情页返回空，使其不会创建隐藏的 Kit 视觉树。
    /// </summary>
    public object? ActiveWorkspacePage
    {
        get => mActiveWorkspacePage;
        private set
        {
            if (SetProperty(ref mActiveWorkspacePage, value))
            {
                OnPropertyChanged(nameof(IsWorkspacePage));
            }
        }
    }

    /// <summary>
    /// 获取当前页面是否使用延迟创建的 Kit 工作台视图。
    /// </summary>
    public bool IsWorkspacePage => ActiveWorkspacePage != null;

    /// <summary>
    /// 获取 Catalog 声明的稳定页面列表。
    /// </summary>
    public static IReadOnlyList<string> PageNames => sPageCatalog.PageNames;

    /// <summary>
    /// 获取 XAML 一级导航实际显示的页面名称。
    /// </summary>
    public IReadOnlyList<string> NavigationPages => sPageCatalog.NavigationPageNames;

    /// <summary>
    /// 获取或设置当前导航页面；空值和未知页面统一回落默认页。
    /// </summary>
    public string SelectedPage
    {
        get => mSelectedPage;
        set
        {
            var pageName = sPageCatalog.Find(value)?.PageName ?? DefaultPageName;
            if (SetProperty(ref mSelectedPage, pageName))
            {
                RefreshNavigationSelection();
                UpdateCurrentPage();
            }
        }
    }

    /// <summary>
    /// 获取当前页面显示标题。
    /// </summary>
    public string CurrentPageTitle
    {
        get => mCurrentPageTitle;
        private set => SetProperty(ref mCurrentPageTitle, value);
    }

    /// <summary>
    /// 获取当前页面紧凑页头的一句话功能介绍。
    /// </summary>
    public string CurrentPageDescription
    {
        get => mCurrentPageDescription;
        private set => SetProperty(ref mCurrentPageDescription, value);
    }

    /// <summary>
    /// 获取当前详情页面显示段落。
    /// </summary>
    public IReadOnlyList<WorkbenchDisplaySection> CurrentSections
    {
        get => mCurrentSections;
        private set => SetProperty(ref mCurrentSections, value);
    }

    /// <summary>
    /// 获取当前页面是否使用 Framework 专用总览布局。
    /// </summary>
    public bool IsOverviewPage
    {
        get => mIsOverviewPage;
        private set => SetProperty(ref mIsOverviewPage, value);
    }

    /// <summary>
    /// 获取当前页面是否使用统一详情段落布局。
    /// </summary>
    public bool IsDetailPage
    {
        get => mIsDetailPage;
        private set => SetProperty(ref mIsDetailPage, value);
    }

    /// <summary>
    /// 获取当前页面是否使用 EventKit 专用工作台。
    /// </summary>
    public bool IsEventKitPage
    {
        get => mIsEventKitPage;
        private set => SetProperty(ref mIsEventKitPage, value);
    }

    /// <summary>
    /// 获取当前页面是否使用 FsmKit 专用工作台。
    /// </summary>
    public bool IsFsmKitPage
    {
        get => mIsFsmKitPage;
        private set => SetProperty(ref mIsFsmKitPage, value);
    }

    /// <summary>
    /// 获取当前页面是否使用 LogKit 专用工作台。
    /// </summary>
    public bool IsLogKitPage
    {
        get => mIsLogKitPage;
        private set => SetProperty(ref mIsLogKitPage, value);
    }

    /// <summary>
    /// 获取当前页面是否使用 PoolKit 对象池诊断工作台。
    /// </summary>
    public bool IsPoolKitPage
    {
        get => mIsPoolKitPage;
        private set => SetProperty(ref mIsPoolKitPage, value);
    }

    /// <summary>
    /// 获取当前页面是否使用 ResKit 资源诊断工作台。
    /// </summary>
    public bool IsResKitPage
    {
        get => mIsResKitPage;
        private set => SetProperty(ref mIsResKitPage, value);
    }

    /// <summary>
    /// 获取当前页面是否使用 ActionKit 活动树诊断工作台。
    /// </summary>
    public bool IsActionKitPage
    {
        get => mIsActionKitPage;
        private set => SetProperty(ref mIsActionKitPage, value);
    }

    /// <summary>获取当前页面是否使用 AudioKit 只读观察器。</summary>
    public bool IsAudioKitPage
    {
        get => mIsAudioKitPage;
        private set => SetProperty(ref mIsAudioKitPage, value);
    }

    /// <summary>获取当前页面是否使用 SpatialKit 空间索引工作台。</summary>
    public bool IsSpatialKitPage
    {
        get => mIsSpatialKitPage;
        private set => SetProperty(ref mIsSpatialKitPage, value);
    }

    /// <summary>获取当前页面是否使用 Unity UIKit Runtime 诊断工作台。</summary>
    public bool IsUIKitPage
    {
        get => mIsUIKitPage;
        private set => SetProperty(ref mIsUIKitPage, value);
    }

    /// <summary>获取当前页面是否使用 TableKit 生成工作台。</summary>
    public bool IsTableKitPage
    {
        get => mIsTableKitPage;
        private set => SetProperty(ref mIsTableKitPage, value);
    }

    /// <summary>获取当前页面是否使用 LocalizationKit 本地化工作台。</summary>
    public bool IsLocalizationKitPage
    {
        get => mIsLocalizationKitPage;
        private set => SetProperty(ref mIsLocalizationKitPage, value);
    }

    /// <summary>获取当前页面是否使用 SaveKit 存档工作台。</summary>
    public bool IsSaveKitPage
    {
        get => mIsSaveKitPage;
        private set => SetProperty(ref mIsSaveKitPage, value);
    }

    /// <summary>
    /// 获取当前页面是否使用离线文档阅读器。
    /// </summary>
    public bool IsDocumentationPage
    {
        get => mIsDocumentationPage;
        private set => SetProperty(ref mIsDocumentationPage, value);
    }

    /// <summary>
    /// 根据当前 Catalog 模块刷新标题、呈现类型和详情段落。
    /// </summary>
    private void UpdateCurrentPage()
    {
        var module = sPageCatalog.Find(SelectedPage) ?? sPageCatalog.DefaultModule;
        var sections = mDashboardState == null
            ? new[] { new WorkbenchDisplaySection("Status", "loading") }
            : module.CreateSections(mDashboardState);
        ReplaceCurrentPage(module, sections);
    }

    /// <summary>
    /// 统一替换页面状态，保证 XAML 收到完整且连续的属性通知。
    /// </summary>
    /// <param name="module">当前页面模块。</param>
    /// <param name="sections">页面详情段落。</param>
    private void ReplaceCurrentPage(
        WorkbenchPageModule module,
        IReadOnlyList<WorkbenchDisplaySection> sections)
    {
        CurrentPageTitle = module.PageTitle;
        CurrentPageDescription = module.Description;
        CurrentSections = sections;
        IsOverviewPage = module.Presentation == WorkbenchPagePresentation.Overview;
        IsDetailPage = module.Presentation == WorkbenchPagePresentation.Detail;
        IsEventKitPage = module.Presentation == WorkbenchPagePresentation.EventKit;
        IsFsmKitPage = module.Presentation == WorkbenchPagePresentation.FsmKit;
        IsLogKitPage = module.Presentation == WorkbenchPagePresentation.LogKit;
        IsPoolKitPage = module.Presentation == WorkbenchPagePresentation.PoolKit;
        IsResKitPage = module.Presentation == WorkbenchPagePresentation.ResKit;
        IsActionKitPage = module.Presentation == WorkbenchPagePresentation.ActionKit;
        IsAudioKitPage = module.Presentation == WorkbenchPagePresentation.AudioKit;
        IsSpatialKitPage = module.Presentation == WorkbenchPagePresentation.SpatialKit;
        IsUIKitPage = module.Presentation == WorkbenchPagePresentation.UIKit;
        IsTableKitPage = module.Presentation == WorkbenchPagePresentation.TableKit;
        IsLocalizationKitPage = module.Presentation == WorkbenchPagePresentation.LocalizationKit;
        IsSaveKitPage = module.Presentation == WorkbenchPagePresentation.SaveKit;
        IsDocumentationPage = module.Presentation == WorkbenchPagePresentation.Documentation;
        ActiveWorkspacePage = ResolveActiveWorkspacePage(module.Presentation);
        ApplyActiveWorkspaceState();
        if (IsDocumentationPage)
        {
            TrackPageTask(DocumentationPage.EnsureLoadedAsync());
        }

        if (IsLocalizationKitPage)
        {
            TrackPageTask(LocalizationKitPage.EnsureLoadedAsync());
        }

        EventKitPage.SetPageActive(IsEventKitPage);
        LogKitPage.SetPageActive(IsLogKitPage);
    }

    /// <summary>
    /// 根据当前呈现类型返回唯一需要进入视觉树的 Kit 页面状态，避免首屏构造所有隐藏页面。
    /// </summary>
    /// <param name="presentation">当前 Catalog 模块的呈现类型。</param>
    /// <returns>当前专用页的 ViewModel；非专用页返回空。</returns>
    private object? ResolveActiveWorkspacePage(WorkbenchPagePresentation presentation)
    {
        return presentation switch
        {
            WorkbenchPagePresentation.EventKit => EventKitPage,
            WorkbenchPagePresentation.FsmKit => FsmKitPage,
            WorkbenchPagePresentation.LogKit => LogKitPage,
            WorkbenchPagePresentation.PoolKit => PoolKitPage,
            WorkbenchPagePresentation.ResKit => ResKitPage,
            WorkbenchPagePresentation.ActionKit => ActionKitPage,
            WorkbenchPagePresentation.AudioKit => AudioKitPage,
            WorkbenchPagePresentation.SpatialKit => SpatialKitPage,
            WorkbenchPagePresentation.UIKit => UIKitPage,
            WorkbenchPagePresentation.TableKit => TableKitPage,
            WorkbenchPagePresentation.LocalizationKit => LocalizationKitPage,
            WorkbenchPagePresentation.SaveKit => SaveKitPage,
            WorkbenchPagePresentation.Documentation => DocumentationPage,
            _ => null
        };
    }

    /// <summary>
    /// 仅把最近一次 Dashboard 投影到活动 Kit，后台页在下次切换时再接收同一份最新状态。
    /// </summary>
    private void ApplyActiveWorkspaceState()
    {
        if (mDashboardState == null)
        {
            return;
        }

        WorkbenchDashboardState state = mDashboardState;
        if (IsEventKitPage)
        {
            EventKitPage.SetProjectRoot(state.ProjectRoot);
            EventKitPage.ApplyPeriodicState(state.EventKitState);
            return;
        }

        if (IsFsmKitPage)
        {
            FsmKitPage.ApplyPeriodicState(state.FsmKitState);
            return;
        }

        if (IsLogKitPage)
        {
            LogKitPage.ApplyPeriodicState(state.LogKitState);
            return;
        }

        if (IsPoolKitPage)
        {
            PoolKitPage.ApplyPeriodicState(state.PoolKitState);
            return;
        }

        if (IsResKitPage)
        {
            ResKitPage.ApplyPeriodicState(state.ResKitState);
            return;
        }

        if (IsActionKitPage)
        {
            ActionKitPage.ApplyPeriodicState(state.ActionKitState);
            return;
        }

        if (IsAudioKitPage)
        {
            AudioKitPage.SetProjectRoot(state.ProjectRoot);
            AudioKitPage.ApplyPeriodicState(state.AudioKitState);
            return;
        }

        if (IsSpatialKitPage)
        {
            SpatialKitPage.ApplyPeriodicState(state.SpatialKitState);
            return;
        }

        if (IsUIKitPage)
        {
            UIKitPage.SetEditorEngine(state.SelectedEngineId);
            UIKitPage.ApplyPeriodicState(state.UIKitState);
            return;
        }

        if (IsSaveKitPage)
        {
            SaveKitPage.SetEngine(state.SelectedEngineId);
            SaveKitPage.ApplyPeriodicState(state.SaveKitState);
            return;
        }

        if (IsLocalizationKitPage)
        {
            LocalizationKitPage.SetProjectRoot(state.ProjectRoot);
        }
    }

    /// <summary>
    /// 根据 Catalog 创建新的导航项集合，避免多个 Shell 共享选中态。
    /// </summary>
    /// <returns>左侧导航分组。</returns>
    private static IReadOnlyList<WorkbenchNavigationGroup> CreatePageNavigationGroups()
    {
        return sPageCatalog.CreateNavigationGroups();
    }
}
