using YokiFrame.Tooling.Application.Models.AudioKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>把一个 Master 或逻辑 Bus 投影为只读的播放观察卡片。</summary>
public sealed class AudioBusChannelViewModel : ViewModelBase
{
    private WorkbenchAudioBus? mBus;
    private int mActiveVoiceCount;
    private IReadOnlyList<WorkbenchAudioVoice> mVoices;
    private IReadOnlyList<WorkbenchAudioHistoryEntry> mHistory;

    /// <summary>创建一个只属于 Avalonia 页面的 Bus 观察投影。</summary>
    internal AudioBusChannelViewModel(
        string key,
        string name,
        string subtitle,
        WorkbenchAudioBus? bus,
        bool isMaster,
        int activeVoiceCount,
        IReadOnlyList<WorkbenchAudioVoice> voices,
        IReadOnlyList<WorkbenchAudioHistoryEntry> history)
    {
        Key = key;
        Name = name;
        Subtitle = subtitle;
        IsMaster = isMaster;
        mBus = bus;
        mActiveVoiceCount = activeVoiceCount;
        mVoices = voices;
        mHistory = history;
    }

    /// <summary>获取刷新间保持选择所使用的稳定键。</summary>
    public string Key { get; }
    /// <summary>获取 Bus 显示名称。</summary>
    public string Name { get; }
    /// <summary>获取 Bus 来源说明。</summary>
    public string Subtitle { get; }
    /// <summary>获取该卡片是否为 Master。</summary>
    public bool IsMaster { get; }
    /// <summary>获取该卡片是否代表框架内置 Bus。</summary>
    public bool IsBuiltIn => IsMaster || mBus?.IsBuiltIn == true;
    /// <summary>获取该卡片是否来自默认集合或项目显式注册。</summary>
    public bool IsRegistered => IsMaster || mBus?.IsRegistered == true;
    /// <summary>获取该卡片是否仅由运行时动态发现。</summary>
    public bool IsDynamic => !IsBuiltIn && !IsRegistered;
    /// <summary>获取该 Bus 的 active voice 数量。</summary>
    public int ActiveVoiceCount { get => mActiveVoiceCount; private set => SetProperty(ref mActiveVoiceCount, value); }
    /// <summary>获取该 Bus 当前正在播放的 voice。</summary>
    public IReadOnlyList<WorkbenchAudioVoice> Voices { get => mVoices; private set => SetProperty(ref mVoices, value); }
    /// <summary>获取该 Bus 的近期播放历史。</summary>
    public IReadOnlyList<WorkbenchAudioHistoryEntry> History { get => mHistory; private set => SetProperty(ref mHistory, value); }
    /// <summary>获取该 Bus 是否没有 active voice。</summary>
    public bool IsVoiceEmpty => Voices.Count == 0;
    /// <summary>获取该 Bus 是否没有近期历史。</summary>
    public bool IsHistoryEmpty => History.Count == 0;

    /// <summary>更新 Runtime 观察事实，不创建音量草稿或任何运行时操作。</summary>
    internal void ApplyRuntimeState(
        WorkbenchAudioBus bus,
        int activeVoiceCount,
        IReadOnlyList<WorkbenchAudioVoice> voices,
        IReadOnlyList<WorkbenchAudioHistoryEntry> history)
    {
        mBus = bus;
        ActiveVoiceCount = activeVoiceCount;
        Voices = voices;
        History = history;
        OnPropertyChanged(nameof(IsBuiltIn));
        OnPropertyChanged(nameof(IsRegistered));
        OnPropertyChanged(nameof(IsDynamic));
        OnPropertyChanged(nameof(IsVoiceEmpty));
        OnPropertyChanged(nameof(IsHistoryEmpty));
    }
}
