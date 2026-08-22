using Avalonia.Media;
using YokiFrame.Tooling.Application.Packages;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 维护 Workbench Shell 的框架总览和导航投影数据。
/// </summary>
public sealed partial class WorkbenchShellViewModel
{
    private const int MAX_LOG_LINES = 80;
    private static readonly IReadOnlyList<string> sDisplayFontOptions = new[]
    {
        "默认字体",
        "等宽字体",
        "LXGW WenKai"
    };
    private static readonly IReadOnlyList<string> sCultureOptions = new[] { "中文", "English" };
    private IReadOnlyList<WorkbenchMetricCard> mEngineCards = Array.Empty<WorkbenchMetricCard>();
    private IReadOnlyList<WorkbenchLogLine> mLogLines = Array.Empty<WorkbenchLogLine>();
    private readonly List<WorkbenchLogLine> mLogBuffer = new(MAX_LOG_LINES);
    private IReadOnlyList<WorkbenchMetricCard> mSnapshotCards = Array.Empty<WorkbenchMetricCard>();
    private IReadOnlyList<WorkbenchMetricCard> mSummaryCards = Array.Empty<WorkbenchMetricCard>();
    private WorkbenchNavigationItem? mSelectedNavigationItem;
    private string mConnectionBadgeText = "未连接";
    private string mCultureText = "中文";
    private string mSelectedDisplayFontName = "默认字体";
    private FontFamily mSelectedDisplayFontFamily = CreateDisplayFontFamily("默认字体");
    private string mVersionText = "版本未知";
    private Uri? mRepositoryUri;

    /// <summary>
    /// 获取左侧导航分组。
    /// </summary>
    public IReadOnlyList<WorkbenchNavigationGroup> NavigationGroups { get; private set; } = Array.Empty<WorkbenchNavigationGroup>();

    /// <summary>
    /// 获取当前选中的导航项。
    /// </summary>
    public WorkbenchNavigationItem? SelectedNavigationItem
    {
        get => mSelectedNavigationItem;
        set
        {
            if (SetProperty(ref mSelectedNavigationItem, value) && value != null)
            {
                SelectedPage = value.PageName;
            }
        }
    }

    /// <summary>
    /// 获取顶部摘要卡片。
    /// </summary>
    public IReadOnlyList<WorkbenchMetricCard> SummaryCards
    {
        get => mSummaryCards;
        private set => SetProperty(ref mSummaryCards, value);
    }

    /// <summary>
    /// 获取引擎状态卡片。
    /// </summary>
    public IReadOnlyList<WorkbenchMetricCard> EngineCards
    {
        get => mEngineCards;
        private set => SetProperty(ref mEngineCards, value);
    }

    /// <summary>
    /// 获取实时数据区域使用的 snapshot 状态卡片。
    /// </summary>
    public IReadOnlyList<WorkbenchMetricCard> SnapshotCards
    {
        get => mSnapshotCards;
        private set => SetProperty(ref mSnapshotCards, value);
    }

    /// <summary>
    /// 获取运行日志行。
    /// </summary>
    public IReadOnlyList<WorkbenchLogLine> LogLines
    {
        get => mLogLines;
        private set => SetProperty(ref mLogLines, value);
    }

    /// <summary>
    /// 创建适合复制到剪贴板的运行日志文本。
    /// </summary>
    /// <returns>按时间顺序拼接的日志文本。</returns>
    public string CreateLogClipboardText()
    {
        return string.Join(
            Environment.NewLine,
            LogLines.Select(static line => line.Timestamp + "  " + line.Message));
    }

    /// <summary>
    /// 获取总览页可选显示字体名称。
    /// </summary>
    public IReadOnlyList<string> DisplayFontOptions => sDisplayFontOptions;

    /// <summary>
    /// 获取或设置当前显示字体名称；修改后会同步刷新 Shell 根容器字体。
    /// </summary>
    public string SelectedDisplayFontName
    {
        get => mSelectedDisplayFontName;
        set
        {
            var nextName = NormalizeDisplayFontName(value);
            if (SetProperty(ref mSelectedDisplayFontName, nextName))
            {
                SelectedDisplayFontFamily = CreateDisplayFontFamily(nextName);
                AddLogLine("显示字体已切换: " + nextName);
            }
        }
    }

    /// <summary>
    /// 获取当前 Shell 使用的 Avalonia 字体族。
    /// </summary>
    public FontFamily SelectedDisplayFontFamily
    {
        get => mSelectedDisplayFontFamily;
        private set => SetProperty(ref mSelectedDisplayFontFamily, value);
    }

    /// <summary>
    /// 获取连接状态徽标文本。
    /// </summary>
    public string ConnectionBadgeText
    {
        get => mConnectionBadgeText;
        private set => SetProperty(ref mConnectionBadgeText, value);
    }

    /// <summary>
    /// 获取顶部语言选择器可用的语言选项。
    /// </summary>
    public IReadOnlyList<string> CultureOptions => sCultureOptions;

    /// <summary>
    /// 获取或设置顶部语言选择器当前值。
    /// </summary>
    public string CultureText
    {
        get => mCultureText;
        set => SetProperty(ref mCultureText, value);
    }

    /// <summary>
    /// 获取版本文本。
    /// </summary>
    public string VersionText
    {
        get => mVersionText;
        private set => SetProperty(ref mVersionText, value);
    }

    /// <summary>
    /// 获取 package.json repository.url 规范化后的仓库主页文本。
    /// </summary>
    public string RepositoryUrl => mRepositoryUri?.AbsoluteUri ?? string.Empty;

    /// <summary>
    /// 获取通过平台默认浏览器打开 YokiFrame 仓库主页的命令。
    /// </summary>
    public AsyncRelayCommand OpenRepositoryCommand { get; }

    /// <summary>
    /// 初始化框架总览需要的静态投影数据。
    /// 快捷命令目录不再预置硬编码默认值：目录由宿主 System/list_commands 在会话建立后填充，
    /// 离线时保持空目录，避免在 UI 层维护第二份命令清单。
    /// </summary>
    private void InitializeWorkbenchLayout()
    {
        NavigationGroups = CreatePageNavigationGroups();
        SelectedNavigationItem = FindNavigationItem(DefaultPageName);
        RefreshNavigationSelection();
        RefreshWorkbenchLayout();
        AddLogLine("Workbench 总览已初始化，等待宿主状态刷新。");
    }

    /// <summary>
    /// 根据最新 dashboard 或默认占位数据刷新卡片投影。
    /// </summary>
    private void RefreshWorkbenchLayout()
    {
        SummaryCards = CreateSummaryCards(mDashboardState);
        EngineCards = CreateEngineCards(mDashboardState);
        SnapshotCards = CreateSnapshotCards(mDashboardState);
        ConnectionBadgeText = mDashboardState?.BridgeHealth.RequiresReconnect == false ? "已连接" : "未连接";
    }

    /// <summary>
    /// 把 Application 解析完成的包版本和仓库主页投影到侧栏状态。
    /// </summary>
    /// <param name="packageMetadata">真实包元数据；设计时或无包根模式下为空。</param>
    private void ApplyPackageMetadata(YokiFramePackageMetadata? packageMetadata)
    {
        mRepositoryUri = packageMetadata?.RepositoryUri;
        VersionText = packageMetadata == null
            ? "版本未知"
            : "v" + packageMetadata.Version.TrimStart('v', 'V');
        OnPropertyChanged(nameof(RepositoryUrl));
    }

    /// <summary>
    /// 把仓库主页交给窗口注入的平台 Launcher，并把失败转换为可见诊断。
    /// </summary>
    /// <returns>外部浏览器启动任务。</returns>
    private async Task OpenRepositoryAsync()
    {
        if (mRepositoryUri == null || mOpenUriAsync == null)
        {
            return;
        }

        try
        {
            await mOpenUriAsync(mRepositoryUri);
            AddLogLine("已打开 YokiFrame GitHub: " + mRepositoryUri.AbsoluteUri);
        }
        catch (Exception exception)
        {
            ShowTransientError("打开 GitHub 失败: " + exception.Message);
        }
    }

    /// <summary>
    /// 仅在包元数据和平台 Launcher 同时可用时启用 GitHub 按钮。
    /// </summary>
    /// <returns>允许打开仓库主页时返回 true。</returns>
    private bool CanOpenRepository()
    {
        return mRepositoryUri != null && mOpenUriAsync != null;
    }

    /// <summary>
    /// 刷新左侧导航项的选中态。
    /// </summary>
    private void RefreshNavigationSelection()
    {
        foreach (var item in NavigationGroups.SelectMany(static group => group.Items))
        {
            item.IsSelected = string.Equals(item.PageName, SelectedPage, StringComparison.Ordinal);
            if (item.IsSelected && !ReferenceEquals(mSelectedNavigationItem, item))
            {
                mSelectedNavigationItem = item;
                OnPropertyChanged(nameof(SelectedNavigationItem));
            }
        }
    }

    /// <summary>
    /// 添加运行日志，并限制日志行数量避免 UI 无限增长。
    /// </summary>
    /// <param name="message">日志内容。</param>
    private void AddLogLine(string message)
    {
        AddLogLine(message, WorkbenchLogLineKind.Information);
    }

    /// <summary>
    /// 添加带语义类型的运行日志，并限制日志行数量避免 UI 无限增长。
    /// </summary>
    /// <param name="message">日志内容。</param>
    /// <param name="kind">日志语义类型。</param>
    private void AddLogLine(string message, WorkbenchLogLineKind kind)
    {
        if (mLogBuffer.Count >= MAX_LOG_LINES) mLogBuffer.RemoveAt(0);
        mLogBuffer.Add(new WorkbenchLogLine(DateTime.Now.ToString("HH:mm:ss"), message, kind));
        LogLines = mLogBuffer.ToArray();
    }

    /// <summary>
    /// 清空运行日志，供日志控制台按钮复用。
    /// </summary>
    private void ClearLogLines()
    {
        mLogBuffer.Clear();
        LogLines = Array.Empty<WorkbenchLogLine>();
    }

    /// <summary>
    /// 按页面名查找导航项。
    /// </summary>
    /// <param name="pageName">页面内部名称。</param>
    /// <returns>匹配的导航项；不存在时返回 null。</returns>
    private WorkbenchNavigationItem? FindNavigationItem(string pageName)
    {
        return NavigationGroups.SelectMany(static group => group.Items)
            .FirstOrDefault(item => string.Equals(item.PageName, pageName, StringComparison.Ordinal));
    }

    /// <summary>
    /// 规范化字体名称，避免绑定层传入空值或未知选项后破坏显示字体。
    /// </summary>
    /// <param name="name">候选字体名称。</param>
    /// <returns>可用字体名称。</returns>
    private static string NormalizeDisplayFontName(string name)
    {
        return sDisplayFontOptions.Contains(name, StringComparer.Ordinal) ? name : "默认字体";
    }

    /// <summary>
    /// 把 UI 字体名称转换为 Avalonia 字体族，系统缺字时由后续 fallback 自动接管。
    /// </summary>
    /// <param name="name">字体名称。</param>
    /// <returns>Avalonia 字体族。</returns>
    private static FontFamily CreateDisplayFontFamily(string name)
    {
        return name switch
        {
            "等宽字体" => new FontFamily("Cascadia Mono, Consolas, Microsoft YaHei UI"),
            "LXGW WenKai" => new FontFamily("LXGW WenKai, Microsoft YaHei UI, Inter"),
            _ => new FontFamily("Inter, Segoe UI, Microsoft YaHei UI, PingFang SC")
        };
    }
}
