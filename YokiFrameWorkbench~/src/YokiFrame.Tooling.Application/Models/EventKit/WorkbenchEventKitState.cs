namespace YokiFrame.Tooling.Application.Models.EventKit;

/// <summary>
/// 提供 Workbench 可直接绑定的 EventKit 强类型状态。
/// </summary>
public sealed class WorkbenchEventKitState
{
    /// <summary>创建完整 EventKit 状态；只允许 Application parser 构造。</summary>
    internal WorkbenchEventKitState(
        WorkbenchEventKitDataSource dataSource,
        long version,
        long sequence,
        int typeEventCount,
        int enumEventCount,
        int stringEventCount,
        int totalEventCount,
        int totalHandlerCount,
        int recentActivityCount,
        IReadOnlyList<WorkbenchEventKitEvent> events,
        IReadOnlyList<WorkbenchEventKitActivity> recentActivities)
    {
        DataSource = dataSource;
        Version = version;
        Sequence = sequence;
        TypeEventCount = typeEventCount;
        EnumEventCount = enumEventCount;
        StringEventCount = stringEventCount;
        TotalEventCount = totalEventCount;
        TotalHandlerCount = totalHandlerCount;
        RecentActivityCount = recentActivityCount;
        Events = events;
        RecentActivities = recentActivities;
    }

    private WorkbenchEventKitDataSource DataSource { get; }
    /// <summary>获取目标 engine。</summary>
    public string EngineId => DataSource.EngineId;
    /// <summary>获取宿主 session。</summary>
    public string SessionId => DataSource.SessionId;
    /// <summary>获取宿主 generation。</summary>
    public long Generation => DataSource.Generation;
    /// <summary>获取宿主模式。</summary>
    public string Mode => DataSource.Mode;
    /// <summary>获取 telemetry 或 snapshot 来源。</summary>
    public string Source => DataSource.Source;
    /// <summary>获取本地观察到的更新时间。</summary>
    public DateTimeOffset UpdatedAtUtc => DataSource.UpdatedAtUtc;
    /// <summary>获取回落、过期或解析失败原因。</summary>
    public string StaleReason => DataSource.StaleReason;
    /// <summary>获取来源证据路径。</summary>
    public IReadOnlyList<string> EvidencePaths => DataSource.EvidencePaths;
    /// <summary>获取未经裁剪的 EventKit payload。</summary>
    public string RawPayloadJson => DataSource.RawPayloadJson;
    /// <summary>获取 Runtime 诊断版本。</summary>
    public long Version { get; }
    /// <summary>获取最后一条活动 sequence。</summary>
    public long Sequence { get; }
    /// <summary>获取 Type 事件数量。</summary>
    public int TypeEventCount { get; }
    /// <summary>获取 Enum 事件数量。</summary>
    public int EnumEventCount { get; }
    /// <summary>获取 String 事件数量。</summary>
    public int StringEventCount { get; }
    /// <summary>获取事件总量。</summary>
    public int TotalEventCount { get; }
    /// <summary>获取活动监听器总量。</summary>
    public int TotalHandlerCount { get; }
    /// <summary>获取有界活动数量。</summary>
    public int RecentActivityCount { get; }
    /// <summary>获取 Runtime 事件列表。</summary>
    public IReadOnlyList<WorkbenchEventKitEvent> Events { get; }
    /// <summary>获取从旧到新排列的有界活动历史。</summary>
    public IReadOnlyList<WorkbenchEventKitActivity> RecentActivities { get; }
}

/// <summary>
/// 描述一个 Runtime 事件注册或纯发送事实。
/// </summary>
public sealed class WorkbenchEventKitEvent
{
    /// <summary>创建事件 read model，并生成稳定选择身份。</summary>
    internal WorkbenchEventKitEvent(
        string channel,
        string eventKey,
        string payloadType,
        int handlerCount,
        long lastSequence,
        string lastTime,
        bool deprecated)
    {
        Channel = channel;
        EventKey = eventKey;
        PayloadType = payloadType;
        HandlerCount = handlerCount;
        LastSequence = lastSequence;
        LastTime = lastTime;
        Deprecated = deprecated;
        Identity = channel + "::" + eventKey + "::" + payloadType;
    }

    /// <summary>获取稳定事件身份。</summary>
    public string Identity { get; }
    /// <summary>获取 Runtime 通道。</summary>
    public string Channel { get; }
    /// <summary>获取事件键。</summary>
    public string EventKey { get; }
    /// <summary>获取负载类型。</summary>
    public string PayloadType { get; }
    /// <summary>获取当前监听器数量。</summary>
    public int HandlerCount { get; }
    /// <summary>获取最后活动 sequence。</summary>
    public long LastSequence { get; }
    /// <summary>获取最后活动时间文本。</summary>
    public string LastTime { get; }
    /// <summary>获取是否为不建议新增的 String 事件。</summary>
    public bool Deprecated { get; }
    /// <summary>获取是否存在活动监听器。</summary>
    public bool HasHandlers => HandlerCount > 0;
    /// <summary>获取是否没有活动监听器。</summary>
    public bool HasNoHandlers => HandlerCount == 0;
    /// <summary>获取负载展示文本。</summary>
    public string PayloadDisplay => string.IsNullOrWhiteSpace(PayloadType) ? "无参数" : PayloadType;
    /// <summary>获取监听器数量展示文本。</summary>
    public string HandlerCountText => HandlerCount + " 个监听器";
}

/// <summary>
/// 描述一条带全局 sequence 的 EventKit 活动。
/// </summary>
public sealed class WorkbenchEventKitActivity
{
    /// <summary>创建 EventKit 活动 read model。</summary>
    internal WorkbenchEventKitActivity(
        long sequence,
        string kind,
        string channel,
        string eventKey,
        string payloadType,
        string handler,
        string time)
    {
        Sequence = sequence;
        Kind = kind;
        Channel = channel;
        EventKey = eventKey;
        PayloadType = payloadType;
        Handler = handler;
        Time = time;
        Identity = channel + "::" + eventKey + "::" + payloadType;
    }

    /// <summary>获取全局活动 sequence。</summary>
    public long Sequence { get; }
    /// <summary>获取 channel/key/payload 组成的事件身份。</summary>
    public string Identity { get; }
    /// <summary>获取 register、send、unregister 或 clear 类型。</summary>
    public string Kind { get; }
    /// <summary>获取 Runtime 通道。</summary>
    public string Channel { get; }
    /// <summary>获取事件键。</summary>
    public string EventKey { get; }
    /// <summary>获取负载类型。</summary>
    public string PayloadType { get; }
    /// <summary>获取注册或注销处理方法名称。</summary>
    public string Handler { get; }
    /// <summary>获取 Runtime 记录时间文本。</summary>
    public string Time { get; }
    /// <summary>获取活动是否包含处理方法。</summary>
    public bool HasHandler => !string.IsNullOrWhiteSpace(Handler);
    /// <summary>获取时间线优先展示的处理方法或负载。</summary>
    public string Detail => WorkbenchEventKitDisplayName.CreateActivityDetail(Handler, PayloadType);
}
