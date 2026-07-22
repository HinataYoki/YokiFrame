namespace YokiFrame.Tooling.Application.Models.AudioKit;

/// <summary>提供 Workbench 可直接绑定的 AudioKit 强类型状态。</summary>
public sealed class WorkbenchAudioKitState
{
    /// <summary>创建完整 AudioKit 状态；只允许 Application parser 构造。</summary>
    internal WorkbenchAudioKitState(
        WorkbenchAudioKitDataSource source,
        long version,
        WorkbenchAudioBackend backend,
        WorkbenchAudioMaster master,
        IReadOnlyList<WorkbenchAudioBus> buses,
        IReadOnlyList<WorkbenchAudioVoice> voices,
        IReadOnlyList<WorkbenchAudioHistoryEntry> history,
        int busTotal,
        int voiceTotal,
        long historyTotal,
        bool busesTruncated,
        bool voicesTruncated,
        bool historyTruncated)
    {
        DataSource = source;
        Version = version;
        Backend = backend;
        Master = master;
        Buses = buses;
        Voices = voices;
        History = history;
        BusTotal = busTotal;
        VoiceTotal = voiceTotal;
        HistoryTotal = historyTotal;
        BusesTruncated = busesTruncated;
        VoicesTruncated = voicesTruncated;
        HistoryTruncated = historyTruncated;
    }

    private WorkbenchAudioKitDataSource DataSource { get; }

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
    /// <summary>获取本地观察更新时间。</summary>
    public DateTimeOffset UpdatedAtUtc => DataSource.UpdatedAtUtc;
    /// <summary>获取回落、过期或解析失败原因。</summary>
    public string StaleReason => DataSource.StaleReason;
    /// <summary>获取来源证据路径。</summary>
    public IReadOnlyList<string> EvidencePaths => DataSource.EvidencePaths;
    /// <summary>获取 Runtime 诊断版本。</summary>
    public long Version { get; }
    /// <summary>获取后端和资源 Loader 状态。</summary>
    public WorkbenchAudioBackend Backend { get; }
    /// <summary>获取 Master 混音状态。</summary>
    public WorkbenchAudioMaster Master { get; }
    /// <summary>获取有界逻辑总线列表。</summary>
    public IReadOnlyList<WorkbenchAudioBus> Buses { get; }
    /// <summary>获取有界 active voice 列表。</summary>
    public IReadOnlyList<WorkbenchAudioVoice> Voices { get; }
    /// <summary>获取最新优先的有界诊断历史；页面只投影其中的播放记录。</summary>
    public IReadOnlyList<WorkbenchAudioHistoryEntry> History { get; }
    /// <summary>获取 Runtime 总线总量。</summary>
    public int BusTotal { get; }
    /// <summary>获取 Runtime active voice 总量。</summary>
    public int VoiceTotal { get; }
    /// <summary>获取当前会话累计历史总量。</summary>
    public long HistoryTotal { get; }
    /// <summary>获取总线列表是否裁剪。</summary>
    public bool BusesTruncated { get; }
    /// <summary>获取 voice 列表是否裁剪。</summary>
    public bool VoicesTruncated { get; }
    /// <summary>获取历史是否因容量或 payload 预算裁剪。</summary>
    public bool HistoryTruncated { get; }
}

/// <summary>描述当前 AudioKit 后端能力和资源 Loader。</summary>
public sealed record WorkbenchAudioBackend(
    string Name,
    int Capabilities,
    string CapabilityNames,
    string ResourceLoader);

/// <summary>描述 Master 配置、有效音量和活动数量。</summary>
public sealed record WorkbenchAudioMaster(
    float Volume,
    float EffectiveVolume,
    bool Muted,
    int ActiveVoiceCount);

/// <summary>描述一个逻辑总线的混音状态。</summary>
public sealed record WorkbenchAudioBus(
    string Name,
    float Volume,
    float EffectiveVolume,
    bool Muted,
    bool IsMaster,
    int ActiveVoiceCount,
    bool IsBuiltIn = false,
    bool IsRegistered = false);

/// <summary>描述一个 active voice 的控制句柄、播放和空间状态。</summary>
public sealed record WorkbenchAudioVoice(
    long BackendGeneration,
    int VoiceId,
    string Path,
    string Bus,
    string BackendName,
    bool Loop,
    bool Playing,
    bool Paused,
    float Volume,
    float Pitch,
    float Duration,
    float Elapsed,
    bool Is3D,
    WorkbenchAudioPosition Position,
    string FollowTarget,
    float MinDistance,
    float MaxDistance,
    string RolloffMode);

/// <summary>描述宿主无关的三维位置。</summary>
public sealed record WorkbenchAudioPosition(float X, float Y, float Z);

/// <summary>描述一条播放或混音控制历史。</summary>
public sealed record WorkbenchAudioHistoryEntry(
    long Sequence,
    string EventType,
    long BackendGeneration,
    int VoiceId,
    string Path,
    string Bus,
    float Volume,
    string TimestampUtc);
