using YokiFrame.Tooling.Application.Models.EventKit;
using YokiFrame.Tooling.Application.Models.EventKit.Scan;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels.EventKit;

/// <summary>稳定合并一个事件的 Runtime 状态和静态发送、注册、注销关系。</summary>
public sealed class EventKitEventListItemViewModel : ViewModelBase
{
    private const int MAX_VISIBLE_CODE_FILES = 3;
    private readonly Func<WorkbenchEventKitCodeLocation, Task>? mOpenLocationAsync;
    private IReadOnlyList<EventKitCodeLocationItemViewModel> mSenders = Array.Empty<EventKitCodeLocationItemViewModel>();
    private IReadOnlyList<EventKitCodeLocationItemViewModel> mReceivers = Array.Empty<EventKitCodeLocationItemViewModel>();
    private IReadOnlyList<EventKitCodeLocationItemViewModel> mUnregisters = Array.Empty<EventKitCodeLocationItemViewModel>();
    private IReadOnlyList<EventKitCodeLocationGroupViewModel> mSenderGroups = Array.Empty<EventKitCodeLocationGroupViewModel>();
    private IReadOnlyList<EventKitCodeLocationGroupViewModel> mReceiverGroups = Array.Empty<EventKitCodeLocationGroupViewModel>();
    private IReadOnlyList<EventKitCodeLocationGroupViewModel> mUnregisterGroups = Array.Empty<EventKitCodeLocationGroupViewModel>();
    private IReadOnlyList<EventKitCodeLocationGroupViewModel> mVisibleSenderGroups = Array.Empty<EventKitCodeLocationGroupViewModel>();
    private IReadOnlyList<EventKitCodeLocationGroupViewModel> mVisibleReceiverGroups = Array.Empty<EventKitCodeLocationGroupViewModel>();
    private IReadOnlyList<EventKitCodeLocationGroupViewModel> mVisibleUnregisterGroups = Array.Empty<EventKitCodeLocationGroupViewModel>();
    private string mEventKey;
    private string mEventKeyDisplay;
    private string mPayloadType;
    private string mPayloadDisplay;
    private string mLastTime = string.Empty;
    private int mHandlerCount;
    private long mLastSequence;
    private bool mDeprecated;
    private bool mHasRuntime;
    private bool mHasStaticRelation;

    /// <summary>从 Runtime 事件创建稳定关系项。</summary>
    public EventKitEventListItemViewModel(
        WorkbenchEventKitEvent item,
        Func<WorkbenchEventKitCodeLocation, Task>? openLocationAsync = null)
    {
        Identity = item.Identity;
        Channel = item.Channel;
        mEventKey = item.EventKey;
        mEventKeyDisplay = WorkbenchEventKitDisplayName.CreateEventKey(Channel, item.EventKey);
        mPayloadType = item.PayloadType;
        mPayloadDisplay = WorkbenchEventKitDisplayName.CreatePayload(item.PayloadType, NoPayloadText);
        mOpenLocationAsync = openLocationAsync;
        Apply(item);
    }

    /// <summary>从静态源码关系创建稳定关系项。</summary>
    public EventKitEventListItemViewModel(
        WorkbenchEventKitCodeRelation relation,
        Func<WorkbenchEventKitCodeLocation, Task>? openLocationAsync = null)
    {
        Identity = relation.Identity;
        Channel = relation.Channel;
        mEventKey = relation.EventKey;
        mEventKeyDisplay = WorkbenchEventKitDisplayName.CreateEventKey(Channel, relation.EventKey);
        mPayloadType = relation.PayloadType;
        mPayloadDisplay = WorkbenchEventKitDisplayName.CreatePayload(relation.PayloadType, NoPayloadText);
        mOpenLocationAsync = openLocationAsync;
        Apply(relation);
    }

    /// <summary>获取 channel/key/payload 组成的稳定身份。</summary>
    public string Identity { get; }
    /// <summary>获取 Runtime 通道。</summary>
    public string Channel { get; }
    /// <summary>获取事件键。</summary>
    public string EventKey { get => mEventKey; private set => SetEventKey(value); }
    /// <summary>获取移除命名空间和外层声明类型后的事件短名。</summary>
    public string EventKeyDisplay => mEventKeyDisplay;
    /// <summary>获取负载类型。</summary>
    public string PayloadType { get => mPayloadType; private set => SetPayloadType(value); }
    /// <summary>获取活动监听器数量。</summary>
    public int HandlerCount { get => mHandlerCount; private set => SetHandlerCount(value); }
    /// <summary>获取最后活动 sequence。</summary>
    public long LastSequence { get => mLastSequence; private set => SetProperty(ref mLastSequence, value); }
    /// <summary>获取最后活动时间。</summary>
    public string LastTime { get => mLastTime; private set => SetLastTime(value); }
    /// <summary>获取是否为不建议新增的 String 通道。</summary>
    public bool Deprecated { get => mDeprecated; private set => SetProperty(ref mDeprecated, value); }
    /// <summary>获取当前身份是否存在 Runtime 事实。</summary>
    public bool HasRuntime { get => mHasRuntime; private set => SetProperty(ref mHasRuntime, value); }
    /// <summary>获取当前身份是否存在静态关系。</summary>
    public bool HasStaticRelation { get => mHasStaticRelation; private set => SetProperty(ref mHasStaticRelation, value); }
    /// <summary>获取静态发送位置。</summary>
    public IReadOnlyList<EventKitCodeLocationItemViewModel> Senders { get => mSenders; private set => SetProperty(ref mSenders, value); }
    /// <summary>获取静态注册位置。</summary>
    public IReadOnlyList<EventKitCodeLocationItemViewModel> Receivers { get => mReceivers; private set => SetProperty(ref mReceivers, value); }
    /// <summary>获取静态注销位置。</summary>
    public IReadOnlyList<EventKitCodeLocationItemViewModel> Unregisters { get => mUnregisters; private set => SetProperty(ref mUnregisters, value); }
    /// <summary>获取按源码文件聚合的发送位置。</summary>
    public IReadOnlyList<EventKitCodeLocationGroupViewModel> SenderGroups { get => mSenderGroups; private set => SetProperty(ref mSenderGroups, value); }
    /// <summary>获取按源码文件聚合的注册位置。</summary>
    public IReadOnlyList<EventKitCodeLocationGroupViewModel> ReceiverGroups { get => mReceiverGroups; private set => SetProperty(ref mReceiverGroups, value); }
    /// <summary>获取按源码文件聚合的注销位置。</summary>
    public IReadOnlyList<EventKitCodeLocationGroupViewModel> UnregisterGroups { get => mUnregisterGroups; private set => SetProperty(ref mUnregisterGroups, value); }
    /// <summary>获取关系行最多展示的前三个发送文件组。</summary>
    public IReadOnlyList<EventKitCodeLocationGroupViewModel> VisibleSenderGroups { get => mVisibleSenderGroups; private set => SetProperty(ref mVisibleSenderGroups, value); }
    /// <summary>获取关系行最多展示的前三个注册文件组。</summary>
    public IReadOnlyList<EventKitCodeLocationGroupViewModel> VisibleReceiverGroups { get => mVisibleReceiverGroups; private set => SetProperty(ref mVisibleReceiverGroups, value); }
    /// <summary>获取关系行最多展示的前三个注销文件组。</summary>
    public IReadOnlyList<EventKitCodeLocationGroupViewModel> VisibleUnregisterGroups { get => mVisibleUnregisterGroups; private set => SetProperty(ref mVisibleUnregisterGroups, value); }
    /// <summary>获取关系行用于快速定位的首个发送位置。</summary>
    public EventKitCodeLocationItemViewModel? SenderPreview => Senders.Count == 0 ? null : Senders[0];
    /// <summary>获取关系行用于快速定位的首个注册位置。</summary>
    public EventKitCodeLocationItemViewModel? ReceiverPreview => Receivers.Count == 0 ? null : Receivers[0];
    /// <summary>获取关系行用于快速定位的首个注销位置。</summary>
    public EventKitCodeLocationItemViewModel? UnregisterPreview => Unregisters.Count == 0 ? null : Unregisters[0];
    /// <summary>获取负载展示文本。</summary>
    public string PayloadDisplay => mPayloadDisplay;
    /// <summary>获取监听器数量文本。</summary>
    public string HandlerCountText => HandlerCount + " " + WorkbenchI18nService.Instance.GetString("String.EventKit.Status.HandlerCount");
    /// <summary>获取最后活动展示文本。</summary>
    public string LastActivityText => string.IsNullOrWhiteSpace(LastTime)
        ? WorkbenchI18nService.Instance.GetString("String.EventKit.Status.NoRuntimeActivity")
        : LastTime;
    /// <summary>获取发送位置数量文本。</summary>
    public string SenderCountText => FormatLocations(Senders.Count);
    /// <summary>获取注册位置数量文本。</summary>
    public string ReceiverCountText => FormatLocations(Receivers.Count);
    /// <summary>获取注销位置数量文本。</summary>
    public string UnregisterCountText => FormatLocations(Unregisters.Count);
    /// <summary>获取发送、注册和注销位置的紧凑计数。</summary>
    public string RelationCountText => string.Format(
        WorkbenchI18nService.Instance.GetString("String.EventKit.RelationCountTemplate", "发 {0} · 注 {1} · 销 {2}"),
        Senders.Count, Receivers.Count, Unregisters.Count);
    /// <summary>获取是否存在静态发送位置。</summary>
    public bool HasSenders => Senders.Count > 0;
    /// <summary>获取是否缺少静态发送位置。</summary>
    public bool HasNoSenders => !HasSenders;
    /// <summary>获取是否存在静态注册位置。</summary>
    public bool HasReceivers => Receivers.Count > 0;
    /// <summary>获取是否缺少静态注册位置。</summary>
    public bool HasNoReceivers => !HasReceivers;
    /// <summary>获取是否存在静态注销位置。</summary>
    public bool HasUnregisters => Unregisters.Count > 0;
    /// <summary>获取是否缺少静态注销位置。</summary>
    public bool HasNoUnregisters => !HasUnregisters;
    /// <summary>获取已有注册但未扫描到对应注销位置的生命周期缺口。</summary>
    public bool HasMissingUnregister => HasReceivers && HasNoUnregisters;
    /// <summary>获取发送位置是否超过关系行展示上限。</summary>
    public bool HasSenderOverflow => SenderGroups.Count > MAX_VISIBLE_CODE_FILES;
    /// <summary>获取注册位置是否超过关系行展示上限。</summary>
    public bool HasReceiverOverflow => ReceiverGroups.Count > MAX_VISIBLE_CODE_FILES;
    /// <summary>获取注销位置是否超过关系行展示上限。</summary>
    public bool HasUnregisterOverflow => UnregisterGroups.Count > MAX_VISIBLE_CODE_FILES;
    /// <summary>获取未直接展示的发送位置数量。</summary>
    public string SenderOverflowText => FormatOverflow(SenderGroups.Count);
    /// <summary>获取未直接展示的注册位置数量。</summary>
    public string ReceiverOverflowText => FormatOverflow(ReceiverGroups.Count);
    /// <summary>获取未直接展示的注销位置数量。</summary>
    public string UnregisterOverflowText => FormatOverflow(UnregisterGroups.Count);
    /// <summary>获取带标签的主要负载信息。</summary>
    public string PayloadSummaryText => WorkbenchI18nService.Instance.GetString("String.EventKit.Status.Parameter") + PayloadDisplay;
    /// <summary>获取静态发送与注册覆盖状态。</summary>
    public string FlowCoverageText => CreateFlowCoverageText();
    /// <summary>获取静态注册与注销调用点平衡状态。</summary>
    public string LifetimeBalanceText => CreateLifetimeBalanceText();
    /// <summary>获取当前事件是否没有 Runtime 监听器。</summary>
    public bool HasNoHandlers => HandlerCount == 0;
    /// <summary>获取是否为 Type 通道。</summary>
    public bool IsType => string.Equals(Channel, "Type", StringComparison.Ordinal);
    /// <summary>获取是否为 Enum 通道。</summary>
    public bool IsEnum => string.Equals(Channel, "Enum", StringComparison.Ordinal);
    /// <summary>获取是否为 String 通道。</summary>
    public bool IsString => string.Equals(Channel, "String", StringComparison.Ordinal);

    /// <summary>应用同身份的新 Runtime 帧并保留当前对象引用。</summary>
    internal void Apply(WorkbenchEventKitEvent item)
    {
        EventKey = item.EventKey;
        PayloadType = item.PayloadType;
        HandlerCount = item.HandlerCount;
        LastSequence = item.LastSequence;
        LastTime = item.LastTime;
        Deprecated = item.Deprecated;
        HasRuntime = true;
    }

    /// <summary>标记当前扫描身份暂时没有 Runtime 事实。</summary>
    internal void ClearRuntime()
    {
        HandlerCount = 0;
        LastSequence = 0L;
        LastTime = string.Empty;
        HasRuntime = false;
    }

    /// <summary>应用同身份静态关系并重建低频源码位置命令。</summary>
    internal void Apply(WorkbenchEventKitCodeRelation relation)
    {
        EventKey = relation.EventKey;
        PayloadType = relation.PayloadType;
        Deprecated = relation.Deprecated;
        Senders = CreateLocations(relation.Senders);
        Receivers = CreateLocations(relation.Receivers);
        Unregisters = CreateLocations(relation.Unregisters);
        SenderGroups = CreateLocationGroups(Senders);
        ReceiverGroups = CreateLocationGroups(Receivers);
        UnregisterGroups = CreateLocationGroups(Unregisters);
        VisibleSenderGroups = CreateVisibleGroups(SenderGroups);
        VisibleReceiverGroups = CreateVisibleGroups(ReceiverGroups);
        VisibleUnregisterGroups = CreateVisibleGroups(UnregisterGroups);
        HasStaticRelation = true;
        NotifyCodeProperties();
    }

    /// <summary>标记当前 Runtime 身份没有静态扫描关系。</summary>
    internal void ClearStaticRelation()
    {
        Senders = Array.Empty<EventKitCodeLocationItemViewModel>();
        Receivers = Array.Empty<EventKitCodeLocationItemViewModel>();
        Unregisters = Array.Empty<EventKitCodeLocationItemViewModel>();
        SenderGroups = Array.Empty<EventKitCodeLocationGroupViewModel>();
        ReceiverGroups = Array.Empty<EventKitCodeLocationGroupViewModel>();
        UnregisterGroups = Array.Empty<EventKitCodeLocationGroupViewModel>();
        VisibleSenderGroups = Array.Empty<EventKitCodeLocationGroupViewModel>();
        VisibleReceiverGroups = Array.Empty<EventKitCodeLocationGroupViewModel>();
        VisibleUnregisterGroups = Array.Empty<EventKitCodeLocationGroupViewModel>();
        HasStaticRelation = false;
        NotifyCodeProperties();
    }

    /// <summary>把 Application 位置包装为可点击的 UI 项。</summary>
    private EventKitCodeLocationItemViewModel[] CreateLocations(
        IReadOnlyList<WorkbenchEventKitCodeLocation> locations)
    {
        EventKitCodeLocationItemViewModel[] result = new EventKitCodeLocationItemViewModel[locations.Count];
        for (var index = 0; index < locations.Count; index++)
        {
            result[index] = new EventKitCodeLocationItemViewModel(locations[index], mOpenLocationAsync);
        }

        return result;
    }

    /// <summary>按文件路径聚合调用点，同文件多个行号复用一个文件卡片。</summary>
    private static IReadOnlyList<EventKitCodeLocationGroupViewModel> CreateLocationGroups(
        IReadOnlyList<EventKitCodeLocationItemViewModel> locations)
    {
        Dictionary<string, List<EventKitCodeLocationItemViewModel>> byPath =
            new(StringComparer.OrdinalIgnoreCase);
        List<string> orderedPaths = new();
        for (var index = 0; index < locations.Count; index++)
        {
            string path = locations[index].FilePath;
            if (!byPath.TryGetValue(path, out List<EventKitCodeLocationItemViewModel>? group))
            {
                group = new List<EventKitCodeLocationItemViewModel>();
                byPath.Add(path, group);
                orderedPaths.Add(path);
            }

            group.Add(locations[index]);
        }

        EventKitCodeLocationGroupViewModel[] result = new EventKitCodeLocationGroupViewModel[orderedPaths.Count];
        for (var index = 0; index < result.Length; index++)
        {
            string path = orderedPaths[index];
            result[index] = new EventKitCodeLocationGroupViewModel(path, byPath[path].ToArray());
        }

        return result;
    }

    /// <summary>截取关系行可直接展示的文件组，完整调用点仍供搜索与数量统计使用。</summary>
    private static IReadOnlyList<EventKitCodeLocationGroupViewModel> CreateVisibleGroups(
        IReadOnlyList<EventKitCodeLocationGroupViewModel> groups)
    {
        return groups.Count <= MAX_VISIBLE_CODE_FILES
            ? groups
            : groups.Take(MAX_VISIBLE_CODE_FILES).ToArray();
    }

    /// <summary>更新事件键并同步不含命名空间和外层声明类型的展示文本。</summary>
    private void SetEventKey(string value)
    {
        if (!SetProperty(ref mEventKey, value))
        {
            return;
        }

        mEventKeyDisplay = WorkbenchEventKitDisplayName.CreateEventKey(Channel, value);
        OnPropertyChanged(nameof(EventKeyDisplay));
    }

    /// <summary>更新负载并通知负载展示文本。</summary>
    private void SetPayloadType(string value)
    {
        if (SetProperty(ref mPayloadType, value))
        {
            mPayloadDisplay = WorkbenchEventKitDisplayName.CreatePayload(value, NoPayloadText);
            OnPropertyChanged(nameof(PayloadDisplay));
            OnPropertyChanged(nameof(PayloadSummaryText));
        }
    }

    /// <summary>根据静态调用点判断发送与注册是否同时存在。</summary>
    private string CreateFlowCoverageText()
    {
        if (!HasStaticRelation) return WorkbenchI18nService.Instance.GetString("String.EventKit.NoRelations");
        if (HasSenders && HasReceivers) return WorkbenchI18nService.Instance.GetString("String.EventKit.Status.FlowBoth");
        if (HasSenders) return WorkbenchI18nService.Instance.GetString("String.EventKit.Status.FlowSendOnly");
        if (HasReceivers) return WorkbenchI18nService.Instance.GetString("String.EventKit.Status.FlowReceiveOnly");
        return WorkbenchI18nService.Instance.GetString("String.EventKit.Status.FlowNone");
    }

    /// <summary>根据静态调用点比较注册与注销数量，不替代运行时生命周期判断。</summary>
    private string CreateLifetimeBalanceText()
    {
        if (!HasStaticRelation) return WorkbenchI18nService.Instance.GetString("String.EventKit.Status.BalanceUnknown");
        if (Receivers.Count == 0) return WorkbenchI18nService.Instance.GetString("String.EventKit.Status.BalanceNotApplicable");
        if (Receivers.Count == Unregisters.Count) return WorkbenchI18nService.Instance.GetString("String.EventKit.Status.BalanceEqual");
        if (Unregisters.Count == 0) return WorkbenchI18nService.Instance.GetString("String.EventKit.Status.BalanceMissing");
        return Receivers.Count > Unregisters.Count
            ? WorkbenchI18nService.Instance.GetString("String.EventKit.Status.BalanceMoreRegister")
            : WorkbenchI18nService.Instance.GetString("String.EventKit.Status.BalanceMoreUnregister");
    }

    /// <summary>语言切换时刷新事件行的数量、空状态和关系摘要。</summary>
    internal void RefreshLocalization()
    {
        mPayloadDisplay = WorkbenchEventKitDisplayName.CreatePayload(mPayloadType, NoPayloadText);
        OnPropertyChanged(nameof(PayloadDisplay));
        OnPropertyChanged(nameof(SenderCountText));
        OnPropertyChanged(nameof(ReceiverCountText));
        OnPropertyChanged(nameof(UnregisterCountText));
        OnPropertyChanged(nameof(RelationCountText));
        OnPropertyChanged(nameof(HandlerCountText));
        OnPropertyChanged(nameof(LastActivityText));
        OnPropertyChanged(nameof(PayloadSummaryText));
        OnPropertyChanged(nameof(SenderOverflowText));
        OnPropertyChanged(nameof(ReceiverOverflowText));
        OnPropertyChanged(nameof(UnregisterOverflowText));
        OnPropertyChanged(nameof(FlowCoverageText));
        OnPropertyChanged(nameof(LifetimeBalanceText));
    }

    /// <summary>把位置数量格式化为当前语言的计数文本。</summary>
    private string FormatLocations(int count) => string.Format(
        WorkbenchI18nService.Instance.GetString("String.EventKit.LocationsSuffixTemplate", "{0} 处"), count);

    /// <summary>把溢出文件数量格式化为当前语言的提示文本。</summary>
    private string FormatOverflow(int groupCount) => string.Format(
        WorkbenchI18nService.Instance.GetString("String.EventKit.OverflowTemplate", "还有 {0} 个文件"),
        Math.Max(0, groupCount - MAX_VISIBLE_CODE_FILES));

    /// <summary>无负载占位文本。</summary>
    private static string NoPayloadText =>
        WorkbenchI18nService.Instance.GetString("String.EventKit.NoPayload", "无负载");

    /// <summary>更新监听器数量并通知派生展示属性。</summary>
    private void SetHandlerCount(int value)
    {
        if (!SetProperty(ref mHandlerCount, value))
        {
            return;
        }

        OnPropertyChanged(nameof(HandlerCountText));
        OnPropertyChanged(nameof(HasNoHandlers));
    }

    /// <summary>更新最后活动时间并通知派生展示属性。</summary>
    private void SetLastTime(string value)
    {
        if (SetProperty(ref mLastTime, value))
        {
            OnPropertyChanged(nameof(LastActivityText));
        }
    }

    /// <summary>通知源码位置数量与空状态派生属性。</summary>
    private void NotifyCodeProperties()
    {
        OnPropertyChanged(nameof(SenderCountText));
        OnPropertyChanged(nameof(ReceiverCountText));
        OnPropertyChanged(nameof(UnregisterCountText));
        OnPropertyChanged(nameof(RelationCountText));
        OnPropertyChanged(nameof(SenderPreview));
        OnPropertyChanged(nameof(ReceiverPreview));
        OnPropertyChanged(nameof(UnregisterPreview));
        OnPropertyChanged(nameof(HasSenders));
        OnPropertyChanged(nameof(HasNoSenders));
        OnPropertyChanged(nameof(HasReceivers));
        OnPropertyChanged(nameof(HasNoReceivers));
        OnPropertyChanged(nameof(HasUnregisters));
        OnPropertyChanged(nameof(HasNoUnregisters));
        OnPropertyChanged(nameof(HasMissingUnregister));
        OnPropertyChanged(nameof(HasSenderOverflow));
        OnPropertyChanged(nameof(HasReceiverOverflow));
        OnPropertyChanged(nameof(HasUnregisterOverflow));
        OnPropertyChanged(nameof(SenderOverflowText));
        OnPropertyChanged(nameof(ReceiverOverflowText));
        OnPropertyChanged(nameof(UnregisterOverflowText));
        OnPropertyChanged(nameof(FlowCoverageText));
        OnPropertyChanged(nameof(LifetimeBalanceText));
    }
}
