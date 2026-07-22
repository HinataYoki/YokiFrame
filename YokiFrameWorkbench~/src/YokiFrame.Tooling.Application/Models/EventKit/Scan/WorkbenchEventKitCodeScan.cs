namespace YokiFrame.Tooling.Application.Models.EventKit.Scan;

/// <summary>描述一次 EventKit C# 静态关系扫描的强类型结果。</summary>
public sealed class WorkbenchEventKitCodeScan
{
    /// <summary>创建完成的静态关系扫描结果。</summary>
    public WorkbenchEventKitCodeScan(
        string projectRoot,
        bool excludeEditor,
        int scannedFileCount,
        int matchedFileCount,
        TimeSpan elapsed,
        IReadOnlyList<WorkbenchEventKitCodeRelation> relations)
    {
        ProjectRoot = projectRoot;
        ExcludeEditor = excludeEditor;
        ScannedFileCount = scannedFileCount;
        MatchedFileCount = matchedFileCount;
        Elapsed = elapsed;
        Relations = relations;
    }

    /// <summary>获取扫描使用的规范化项目根。</summary>
    public string ProjectRoot { get; }
    /// <summary>获取是否排除了 Editor 目录。</summary>
    public bool ExcludeEditor { get; }
    /// <summary>获取成功解析的 C# 文件数量。</summary>
    public int ScannedFileCount { get; }
    /// <summary>获取包含 EventKit 调用点的文件数量。</summary>
    public int MatchedFileCount { get; }
    /// <summary>获取扫描耗时。</summary>
    public TimeSpan Elapsed { get; }
    /// <summary>获取按稳定事件身份聚合的静态关系。</summary>
    public IReadOnlyList<WorkbenchEventKitCodeRelation> Relations { get; }
    /// <summary>获取发送调用点总数。</summary>
    public int SendCount => Relations.Sum(static relation => relation.SendCount);
    /// <summary>获取注册调用点总数。</summary>
    public int RegisterCount => Relations.Sum(static relation => relation.RegisterCount);
    /// <summary>获取注销调用点总数。</summary>
    public int UnregisterCount => Relations.Sum(static relation => relation.UnregisterCount);
}

/// <summary>描述一个事件身份的发送、注册与注销源码关系。</summary>
public sealed class WorkbenchEventKitCodeRelation
{
    /// <summary>创建一个不可变源码关系。</summary>
    public WorkbenchEventKitCodeRelation(
        string channel,
        string eventKey,
        string payloadType,
        IReadOnlyList<WorkbenchEventKitCodeLocation> senders,
        IReadOnlyList<WorkbenchEventKitCodeLocation> receivers,
        IReadOnlyList<WorkbenchEventKitCodeLocation> unregisters)
    {
        Channel = channel;
        EventKey = eventKey;
        PayloadType = payloadType;
        Identity = CreateIdentity(channel, eventKey, payloadType);
        Senders = senders;
        Receivers = receivers;
        Unregisters = unregisters;
    }

    /// <summary>获取稳定的 channel/key/payload 身份。</summary>
    public string Identity { get; }
    /// <summary>获取 Type、Enum 或 String 通道。</summary>
    public string Channel { get; }
    /// <summary>获取事件键。</summary>
    public string EventKey { get; }
    /// <summary>获取负载类型；无参数时为空。</summary>
    public string PayloadType { get; }
    /// <summary>获取发送调用点。</summary>
    public IReadOnlyList<WorkbenchEventKitCodeLocation> Senders { get; }
    /// <summary>获取注册调用点。</summary>
    public IReadOnlyList<WorkbenchEventKitCodeLocation> Receivers { get; }
    /// <summary>获取注销调用点。</summary>
    public IReadOnlyList<WorkbenchEventKitCodeLocation> Unregisters { get; }
    /// <summary>获取发送调用点数量。</summary>
    public int SendCount => Senders.Count;
    /// <summary>获取注册调用点数量。</summary>
    public int RegisterCount => Receivers.Count;
    /// <summary>获取注销调用点数量。</summary>
    public int UnregisterCount => Unregisters.Count;
    /// <summary>获取是否为不建议新增的 String 通道。</summary>
    public bool Deprecated => string.Equals(Channel, "String", StringComparison.Ordinal);

    /// <summary>创建跨扫描与 Runtime 共用的稳定身份。</summary>
    public static string CreateIdentity(string channel, string eventKey, string payloadType)
    {
        return channel + "::" + eventKey + "::" + payloadType;
    }
}

/// <summary>描述一个受项目根约束的 C# 文件行号。</summary>
public sealed class WorkbenchEventKitCodeLocation
{
    /// <summary>创建一个项目相对源码位置。</summary>
    public WorkbenchEventKitCodeLocation(string filePath, int line)
    {
        FilePath = filePath;
        Line = Math.Max(1, line);
        FileName = Path.GetFileName(filePath);
        Display = FileName + ":" + Line;
    }

    /// <summary>获取使用正斜杠的项目相对路径。</summary>
    public string FilePath { get; }
    /// <summary>获取从一开始的源码行号。</summary>
    public int Line { get; }
    /// <summary>获取不含目录的文件名。</summary>
    public string FileName { get; }
    /// <summary>获取紧凑的文件名与行号文本。</summary>
    public string Display { get; }
}
