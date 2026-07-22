using YokiFrame.Tooling.Application.Models.AudioKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 AudioKit Bus 的只读分组与筛选投影。</summary>
public sealed partial class AudioKitPageViewModel
{
    private readonly List<AudioBusChannelViewModel> mAllBusChannels = new();

    /// <summary>按稳定键更新 Master 与逻辑 Bus，避免遥测刷新重建未变化的视觉树。</summary>
    private void RebuildBusChannels(WorkbenchAudioKitState state, string selectedKey)
    {
        Dictionary<string, AudioBusChannelViewModel> existing = new(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < mAllBusChannels.Count; index++)
        {
            AudioBusChannelViewModel channel = mAllBusChannels[index];
            existing[channel.Key] = channel;
        }

        mAllBusChannels.Clear();
        Dictionary<string, List<WorkbenchAudioVoice>> voicesByBus = GroupVoicesByBus(state.Voices);
        IReadOnlyList<WorkbenchAudioHistoryEntry> playbackHistory = GetPlaybackHistory(state.History);
        Dictionary<string, List<WorkbenchAudioHistoryEntry>> historyByBus = GroupHistoryByBus(playbackHistory);
        WorkbenchAudioBus? masterBus = state.Buses.FirstOrDefault(static bus => bus.IsMaster);
        mAllBusChannels.Add(CreateMasterBusChannel(state, masterBus, playbackHistory, existing));
        for (var index = 0; index < state.Buses.Count; index++)
        {
            WorkbenchAudioBus bus = state.Buses[index];
            if (!bus.IsMaster)
            {
                mAllBusChannels.Add(CreateBusChannel(bus, existing, voicesByBus, historyByBus));
            }
        }

        RefreshBusFilter(selectedKey);
    }

    /// <summary>创建或更新 Master 观察卡片，并聚合全部 voice 与历史。</summary>
    private static AudioBusChannelViewModel CreateMasterBusChannel(
        WorkbenchAudioKitState state,
        WorkbenchAudioBus? masterBus,
        IReadOnlyList<WorkbenchAudioHistoryEntry> playbackHistory,
        IReadOnlyDictionary<string, AudioBusChannelViewModel> existing)
    {
        WorkbenchAudioBus bus = masterBus ?? new WorkbenchAudioBus(
            "Master", state.Master.Volume, state.Master.EffectiveVolume,
            state.Master.Muted, true, state.Master.ActiveVoiceCount);
        string key = "bus:" + bus.Name;
        if (!existing.TryGetValue(key, out AudioBusChannelViewModel? channel))
        {
            return new AudioBusChannelViewModel(
                key, "MASTER", "主输出", bus, true,
                state.Master.ActiveVoiceCount, state.Voices, playbackHistory);
        }

        channel.ApplyRuntimeState(bus, state.Master.ActiveVoiceCount, state.Voices, playbackHistory);
        return channel;
    }

    /// <summary>创建或更新单个逻辑 Bus 的观察卡片。</summary>
    private static AudioBusChannelViewModel CreateBusChannel(
        WorkbenchAudioBus bus,
        IReadOnlyDictionary<string, AudioBusChannelViewModel> existing,
        IReadOnlyDictionary<string, List<WorkbenchAudioVoice>> voicesByBus,
        IReadOnlyDictionary<string, List<WorkbenchAudioHistoryEntry>> historyByBus)
    {
        IReadOnlyList<WorkbenchAudioVoice> voices = voicesByBus.TryGetValue(bus.Name, out List<WorkbenchAudioVoice>? groupedVoices)
            ? groupedVoices
            : Array.Empty<WorkbenchAudioVoice>();
        IReadOnlyList<WorkbenchAudioHistoryEntry> history = historyByBus.TryGetValue(
            bus.Name,
            out List<WorkbenchAudioHistoryEntry>? groupedHistory)
                ? groupedHistory
                : Array.Empty<WorkbenchAudioHistoryEntry>();
        string key = "bus:" + bus.Name;
        if (!existing.TryGetValue(key, out AudioBusChannelViewModel? channel))
        {
            return new AudioBusChannelViewModel(
                key, bus.Name, CreateBusSubtitle(bus), bus, false,
                bus.ActiveVoiceCount, voices, history);
        }

        channel.ApplyRuntimeState(bus, bus.ActiveVoiceCount, voices, history);
        return channel;
    }

    /// <summary>单次遍历当前 voice，按大小写不敏感的逻辑 Bus 分组。</summary>
    private static Dictionary<string, List<WorkbenchAudioVoice>> GroupVoicesByBus(
        IReadOnlyList<WorkbenchAudioVoice> voices)
    {
        Dictionary<string, List<WorkbenchAudioVoice>> groups = new(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < voices.Count; index++)
        {
            WorkbenchAudioVoice voice = voices[index];
            if (!groups.TryGetValue(voice.Bus, out List<WorkbenchAudioVoice>? group))
            {
                group = new();
                groups.Add(voice.Bus, group);
            }

            group.Add(voice);
        }

        return groups;
    }

    /// <summary>单次遍历当前历史，按大小写不敏感的逻辑 Bus 分组。</summary>
    private static Dictionary<string, List<WorkbenchAudioHistoryEntry>> GroupHistoryByBus(
        IReadOnlyList<WorkbenchAudioHistoryEntry> history)
    {
        Dictionary<string, List<WorkbenchAudioHistoryEntry>> groups = new(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < history.Count; index++)
        {
            WorkbenchAudioHistoryEntry entry = history[index];
            if (!groups.TryGetValue(entry.Bus, out List<WorkbenchAudioHistoryEntry>? group))
            {
                group = new();
                groups.Add(entry.Bus, group);
            }

            group.Add(entry);
        }

        return groups;
    }

    /// <summary>仅保留带路径和 Bus 的成功播放记录，隔离 Runtime 控制诊断。</summary>
    private static IReadOnlyList<WorkbenchAudioHistoryEntry> GetPlaybackHistory(
        IReadOnlyList<WorkbenchAudioHistoryEntry> history)
    {
        for (var index = 0; index < history.Count; index++)
        {
            if (!IsPlaybackHistory(history[index])) return FilterPlaybackHistory(history);
        }

        return history;
    }

    /// <summary>在控制记录混入 Runtime 有界历史时创建紧凑的播放记录列表。</summary>
    private static IReadOnlyList<WorkbenchAudioHistoryEntry> FilterPlaybackHistory(
        IReadOnlyList<WorkbenchAudioHistoryEntry> history)
    {
        List<WorkbenchAudioHistoryEntry> playback = new(history.Count);
        for (var index = 0; index < history.Count; index++)
        {
            WorkbenchAudioHistoryEntry entry = history[index];
            if (IsPlaybackHistory(entry)) playback.Add(entry);
        }

        return playback;
    }

    /// <summary>判断记录是否为当前观察页可归属到 Bus 的成功播放事件。</summary>
    private static bool IsPlaybackHistory(WorkbenchAudioHistoryEntry entry)
    {
        return string.Equals(entry.EventType, "play_started", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(entry.Path)
            && !string.IsNullOrWhiteSpace(entry.Bus);
    }

    /// <summary>根据 Bus 来源生成稳定、可扫描的副标题。</summary>
    private static string CreateBusSubtitle(WorkbenchAudioBus bus)
    {
        if (bus.IsMaster) return "主输出";
        if (bus.IsBuiltIn) return "内置总线";
        return bus.IsRegistered ? "已注册自定义" : "动态发现";
    }

    /// <summary>按当前搜索与活跃条件重建可见 Bus 卡片。</summary>
    private void RefreshBusFilter()
    {
        RefreshBusFilter(SelectedBusChannel?.Key ?? "bus:Master");
    }

    /// <summary>按指定稳定键重建可见 Bus 卡片，同时恢复仍然可见的选择。</summary>
    private void RefreshBusFilter(string selectedKey)
    {
        List<AudioBusChannelViewModel> desired = new(mAllBusChannels.Count);
        for (var index = 0; index < mAllBusChannels.Count; index++)
        {
            AudioBusChannelViewModel channel = mAllBusChannels[index];
            if (MatchesBusFilter(channel)) desired.Add(channel);
        }

        SynchronizeVisibleBusChannels(desired);
        SelectedBusChannel = desired.FirstOrDefault(channel => channel.Key == selectedKey)
            ?? desired.FirstOrDefault();
    }

    /// <summary>原地同步可见 Bus 顺序，避免相同 Bus 在遥测刷新时重建视觉树。</summary>
    private void SynchronizeVisibleBusChannels(IReadOnlyList<AudioBusChannelViewModel> desired)
    {
        for (var index = 0; index < desired.Count; index++)
        {
            AudioBusChannelViewModel channel = desired[index];
            if (index < BusChannels.Count && ReferenceEquals(BusChannels[index], channel)) continue;
            int existingIndex = BusChannels.IndexOf(channel);
            if (existingIndex >= 0) BusChannels.Move(existingIndex, index);
            else BusChannels.Insert(index, channel);
        }

        while (BusChannels.Count > desired.Count) BusChannels.RemoveAt(BusChannels.Count - 1);
    }

    /// <summary>保留 Master，并对普通 Bus 应用名称和活跃 voice 条件。</summary>
    private bool MatchesBusFilter(AudioBusChannelViewModel channel)
    {
        if (channel.IsMaster) return true;
        if (ShowActiveBusesOnly && channel.ActiveVoiceCount == 0) return false;
        if (!MatchesBusScope(channel)) return false;
        return string.IsNullOrWhiteSpace(BusSearchText)
            || channel.Name.Contains(BusSearchText.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>判断普通 Bus 是否属于用户选择的来源范围。</summary>
    private bool MatchesBusScope(AudioBusChannelViewModel channel)
    {
        if (string.Equals(SelectedBusScope, "内置", StringComparison.Ordinal)) return channel.IsBuiltIn;
        if (string.Equals(SelectedBusScope, "已注册", StringComparison.Ordinal))
            return channel.IsRegistered && !channel.IsBuiltIn;
        if (string.Equals(SelectedBusScope, "动态", StringComparison.Ordinal)) return channel.IsDynamic;
        return true;
    }
}
