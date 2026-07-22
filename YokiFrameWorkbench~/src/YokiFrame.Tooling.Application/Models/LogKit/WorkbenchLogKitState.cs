namespace YokiFrame.Tooling.Application.Models.LogKit;

/// <summary>描述 LogKit 当前运行统计。</summary>
public sealed record WorkbenchLogKitStats
{
    /// <summary>创建运行统计；仅由 Application parser 使用。</summary>
    internal WorkbenchLogKitStats(
        string loggerName, bool hasLogger, bool enabled, string minimumLevel,
        int historyCount, int droppedCount)
    {
        LoggerName = loggerName;
        HasLogger = hasLogger;
        Enabled = enabled;
        MinimumLevel = minimumLevel;
        HistoryCount = historyCount;
        DroppedCount = droppedCount;
    }

    /// <summary>获取日志后端名称。</summary>
    public string LoggerName { get; }
    /// <summary>获取是否已安装日志后端。</summary>
    public bool HasLogger { get; }
    /// <summary>获取当前会话是否启用 LogKit。</summary>
    public bool Enabled { get; }
    /// <summary>获取当前会话最低等级。</summary>
    public string MinimumLevel { get; }
    /// <summary>获取 Core 内存历史数量。</summary>
    public int HistoryCount { get; }
    /// <summary>获取 Core 内存历史淘汰数量。</summary>
    public int DroppedCount { get; }
}

/// <summary>描述宿主当前声明的 LogKit 能力。</summary>
public sealed record WorkbenchLogKitCapabilities
{
    /// <summary>创建能力快照；仅由 Application parser 使用。</summary>
    internal WorkbenchLogKitCapabilities(
        bool settingsApply, bool filePreview, bool fileWriter,
        bool playerImGui, bool encryption)
    {
        SettingsApply = settingsApply;
        FilePreview = filePreview;
        FileWriter = fileWriter;
        PlayerImGui = playerImGui;
        Encryption = encryption;
    }

    /// <summary>获取当前会话是否支持应用设置。</summary>
    public bool SettingsApply { get; }
    /// <summary>获取宿主是否支持文件预览。</summary>
    public bool FilePreview { get; }
    /// <summary>获取宿主是否实现文件写入器。</summary>
    public bool FileWriter { get; }
    /// <summary>获取宿主是否实现 Player IMGUI。</summary>
    public bool PlayerImGui { get; }
    /// <summary>获取宿主是否实现可信加密。</summary>
    public bool Encryption { get; }
}

/// <summary>描述一条有界内存日志。</summary>
public sealed record WorkbenchLogKitHistoryEntry
{
    /// <summary>创建历史条目；仅由 Application parser 使用。</summary>
    internal WorkbenchLogKitHistoryEntry(
        string level, string message, string context, string exceptionType,
        string exceptionMessage, string stackTrace, string timestampUtc)
    {
        Level = level;
        Message = message;
        Context = context;
        ExceptionType = exceptionType;
        ExceptionMessage = exceptionMessage;
        StackTrace = stackTrace;
        TimestampUtc = timestampUtc;
    }

    /// <summary>获取日志等级。</summary>
    public string Level { get; }
    /// <summary>获取消息。</summary>
    public string Message { get; }
    /// <summary>获取宿主上下文。</summary>
    public string Context { get; }
    /// <summary>获取异常类型。</summary>
    public string ExceptionType { get; }
    /// <summary>获取异常消息。</summary>
    public string ExceptionMessage { get; }
    /// <summary>获取调用点或异常堆栈。</summary>
    public string StackTrace { get; }
    /// <summary>获取 UTC 时间文本。</summary>
    public string TimestampUtc { get; }
}

/// <summary>描述 Runtime 返回的内存历史窗口。</summary>
public sealed record WorkbenchLogKitHistory
{
    /// <summary>创建内存历史窗口；仅由 Application parser 使用。</summary>
    internal WorkbenchLogKitHistory(
        IReadOnlyList<WorkbenchLogKitHistoryEntry> entries,
        int count, int totalCount, int droppedCount, bool truncated)
    {
        Entries = entries;
        Count = count;
        TotalCount = totalCount;
        DroppedCount = droppedCount;
        Truncated = truncated;
    }

    /// <summary>获取本帧携带的有界条目。</summary>
    public IReadOnlyList<WorkbenchLogKitHistoryEntry> Entries { get; }
    /// <summary>获取本帧条目数量。</summary>
    public int Count { get; }
    /// <summary>获取 Runtime 当前总历史数量。</summary>
    public int TotalCount { get; }
    /// <summary>获取 Runtime 历史淘汰数量。</summary>
    public int DroppedCount { get; }
    /// <summary>获取本帧是否裁剪过历史。</summary>
    public bool Truncated { get; }
}

/// <summary>描述一个 Editor 或 Player 日志文件。</summary>
public sealed record WorkbenchLogKitFileMetadata
{
    /// <summary>创建文件元数据；仅由 Application parser 使用。</summary>
    internal WorkbenchLogKitFileMetadata(
        string kind, string path, string fileName, bool exists,
        long sizeBytes, string modifiedUtc)
    {
        Kind = kind;
        Path = path;
        FileName = fileName;
        Exists = exists;
        SizeBytes = sizeBytes;
        ModifiedUtc = modifiedUtc;
    }

    /// <summary>获取 editor 或 player 来源。</summary>
    public string Kind { get; }
    /// <summary>获取解析后的绝对路径。</summary>
    public string Path { get; }
    /// <summary>获取文件名。</summary>
    public string FileName { get; }
    /// <summary>获取文件是否存在。</summary>
    public bool Exists { get; }
    /// <summary>获取文件大小。</summary>
    public long SizeBytes { get; }
    /// <summary>获取最后修改 UTC 文本。</summary>
    public string ModifiedUtc { get; }
}

/// <summary>描述 Runtime 当前解析出的日志目录和文件。</summary>
public sealed record WorkbenchLogKitFiles
{
    /// <summary>创建文件集合；仅由 Application parser 使用。</summary>
    internal WorkbenchLogKitFiles(
        string directory,
        WorkbenchLogKitFileMetadata editor,
        WorkbenchLogKitFileMetadata player)
    {
        Directory = directory;
        Editor = editor;
        Player = player;
    }

    /// <summary>获取解析后的日志目录。</summary>
    public string Directory { get; }
    /// <summary>获取 Editor 文件元数据。</summary>
    public WorkbenchLogKitFileMetadata Editor { get; }
    /// <summary>获取 Player 文件元数据。</summary>
    public WorkbenchLogKitFileMetadata Player { get; }
}

/// <summary>提供 Workbench 可直接绑定的 LogKit 强类型状态。</summary>
public sealed class WorkbenchLogKitState
{
    /// <summary>创建完整 LogKit 状态；只允许 Application parser 构造。</summary>
    internal WorkbenchLogKitState(
        WorkbenchLogKitDataSource dataSource,
        int schemaVersion,
        long diagnosticVersion,
        long settingsVersion,
        WorkbenchLogKitSettings settings,
        WorkbenchLogKitStats stats,
        WorkbenchLogKitCapabilities capabilities,
        WorkbenchLogKitFiles files,
        WorkbenchLogKitHistory history)
    {
        DataSource = dataSource;
        SchemaVersion = schemaVersion;
        DiagnosticVersion = diagnosticVersion;
        SettingsVersion = settingsVersion;
        Settings = settings;
        Stats = stats;
        Capabilities = capabilities;
        Files = files;
        History = history;
    }

    private WorkbenchLogKitDataSource DataSource { get; }
    /// <summary>获取状态 schema 版本。</summary>
    public int SchemaVersion { get; }
    /// <summary>获取 Runtime 日志诊断版本。</summary>
    public long DiagnosticVersion { get; }
    /// <summary>获取 Runtime 设置版本。</summary>
    public long SettingsVersion { get; }
    /// <summary>获取目标 engine。</summary>
    public string EngineId => DataSource.EngineId;
    /// <summary>获取宿主 session。</summary>
    public string SessionId => DataSource.SessionId;
    /// <summary>获取宿主 generation。</summary>
    public long Generation => DataSource.Generation;
    /// <summary>获取宿主模式。</summary>
    public string Mode => DataSource.Mode;
    /// <summary>获取 telemetry、snapshot 或 command 来源。</summary>
    public string Source => DataSource.Source;
    /// <summary>获取命令实际传输；周期状态为空。</summary>
    public string Transport => DataSource.Transport;
    /// <summary>获取本地观察到的更新时间。</summary>
    public DateTimeOffset UpdatedAtUtc => DataSource.UpdatedAtUtc;
    /// <summary>获取回落、过期或解析失败原因。</summary>
    public string StaleReason => DataSource.StaleReason;
    /// <summary>获取来源证据路径。</summary>
    public IReadOnlyList<string> EvidencePaths => DataSource.EvidencePaths;
    /// <summary>获取未经裁剪的 LogKit payload。</summary>
    public string RawPayloadJson => DataSource.RawPayloadJson;
    /// <summary>获取当前 Runtime 有效设置。</summary>
    public WorkbenchLogKitSettings Settings { get; }
    /// <summary>获取当前 Runtime 统计。</summary>
    public WorkbenchLogKitStats Stats { get; }
    /// <summary>获取当前宿主能力。</summary>
    public WorkbenchLogKitCapabilities Capabilities { get; }
    /// <summary>获取当前日志文件元数据。</summary>
    public WorkbenchLogKitFiles Files { get; }
    /// <summary>获取有界内存日志窗口。</summary>
    public WorkbenchLogKitHistory History { get; }
}
