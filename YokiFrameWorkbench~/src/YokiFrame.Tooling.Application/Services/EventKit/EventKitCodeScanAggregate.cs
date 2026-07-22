using YokiFrame.Tooling.Application.Models.EventKit.Scan;

namespace YokiFrame.Tooling.Application.Services.EventKit;

/// <summary>在单次扫描期间聚合一个 EventKit 事件的三类调用点。</summary>
internal sealed class EventKitCodeScanAggregate
{
    private readonly HashSet<string> mLocationKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<WorkbenchEventKitCodeLocation> mSenders = new();
    private readonly List<WorkbenchEventKitCodeLocation> mReceivers = new();
    private readonly List<WorkbenchEventKitCodeLocation> mUnregisters = new();

    /// <summary>创建指定事件身份的可变聚合器。</summary>
    internal EventKitCodeScanAggregate(string channel, string eventKey, string payloadType)
    {
        Channel = channel;
        EventKey = eventKey;
        PayloadType = payloadType;
    }

    internal string Channel { get; }
    internal string EventKey { get; }
    internal string PayloadType { get; }
    internal int SendCount => mSenders.Count;
    internal int RegisterCount => mReceivers.Count;

    /// <summary>按调用类型追加去重后的源码位置。</summary>
    internal void Add(EventKitCodeUsageKind kind, WorkbenchEventKitCodeLocation location)
    {
        string key = kind + "::" + location.FilePath + "::" + location.Line;
        if (!mLocationKeys.Add(key))
        {
            return;
        }

        GetTarget(kind).Add(location);
    }

    /// <summary>把另一个身份推断结果合并到当前确定身份。</summary>
    internal void Merge(EventKitCodeScanAggregate source)
    {
        CopyLocations(source.mSenders, EventKitCodeUsageKind.Send);
        CopyLocations(source.mReceivers, EventKitCodeUsageKind.Register);
        CopyLocations(source.mUnregisters, EventKitCodeUsageKind.Unregister);
    }

    /// <summary>冻结并按路径、行号排序为 Application read model。</summary>
    internal WorkbenchEventKitCodeRelation Build()
    {
        return new WorkbenchEventKitCodeRelation(
            Channel,
            EventKey,
            PayloadType,
            Sort(mSenders),
            Sort(mReceivers),
            Sort(mUnregisters));
    }

    /// <summary>按调用类型返回对应聚合集合。</summary>
    private List<WorkbenchEventKitCodeLocation> GetTarget(EventKitCodeUsageKind kind)
    {
        return kind switch
        {
            EventKitCodeUsageKind.Send => mSenders,
            EventKitCodeUsageKind.Register => mReceivers,
            _ => mUnregisters
        };
    }

    /// <summary>复制一组位置并复用统一去重规则。</summary>
    private void CopyLocations(IReadOnlyList<WorkbenchEventKitCodeLocation> locations, EventKitCodeUsageKind kind)
    {
        for (var index = 0; index < locations.Count; index++)
        {
            Add(kind, locations[index]);
        }
    }

    /// <summary>返回稳定排序后的独立数组。</summary>
    private static WorkbenchEventKitCodeLocation[] Sort(List<WorkbenchEventKitCodeLocation> locations)
    {
        return locations
            .OrderBy(static location => location.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static location => location.Line)
            .ToArray();
    }
}

/// <summary>标识静态扫描识别出的 EventKit 调用类型。</summary>
internal enum EventKitCodeUsageKind
{
    Send,
    Register,
    Unregister
}
