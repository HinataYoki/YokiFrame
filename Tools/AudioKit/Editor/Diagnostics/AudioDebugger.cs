#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// AudioKit 调试器。
    /// 提供运行时音频监控与播放历史记录。
    /// </summary>
    public static class AudioDebugger
    {
        private static readonly List<AudioPlayRecord> sPlayHistory = new();
        private static readonly List<AudioPlayRecord> sCachedCurrentPlaying = new();
        private static readonly List<IAudioHandle> sCachedHandles = new();
        private static int sMaxHistoryCount = 100;
        private static bool sIsRecording = true;
        private static bool sIsSubscribed;

        /// <summary>
        /// 音频播放记录。
        /// </summary>
        public struct AudioPlayRecord
        {
            public string Path;
            public int ChannelId;
            public float Volume;
            public float Pitch;
            public float StartTime;
            public float Duration;
            public bool IsPlaying;
            public bool IsPaused;
            public float CurrentTime;
            public float Progress;
        }

        /// <summary>
        /// 初始化调试器并订阅事件。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EnsureSubscribed();
        }

        /// <summary>
        /// 处理 PlayMode 状态切换。
        /// </summary>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                sIsSubscribed = false;
                EnsureSubscribed();
                sPlayHistory.Clear();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                sPlayHistory.Clear();
            }
        }

        /// <summary>
        /// 确保已经订阅音频播放事件。
        /// </summary>
        private static void EnsureSubscribed()
        {
            if (sIsSubscribed)
            {
                return;
            }

            AudioMonitorService.OnAudioPlayed -= RecordPlay;
            AudioMonitorService.OnAudioPlayed += RecordPlay;
            sIsSubscribed = true;
        }

        /// <summary>
        /// 是否启用播放记录。
        /// </summary>
        public static bool IsRecording
        {
            get => sIsRecording;
            set => sIsRecording = value;
        }

        /// <summary>
        /// 最大历史记录数量。
        /// </summary>
        public static int MaxHistoryCount
        {
            get => sMaxHistoryCount;
            set => sMaxHistoryCount = Mathf.Max(10, value);
        }

        /// <summary>
        /// 获取播放历史。
        /// </summary>
        public static List<AudioPlayRecord> GetPlayHistory()
        {
            return sPlayHistory;
        }

        /// <summary>
        /// 获取当前正在播放的音频。
        /// </summary>
        public static List<AudioPlayRecord> GetCurrentPlaying()
        {
            sCachedCurrentPlaying.Clear();

            if (!Application.isPlaying)
            {
                return sCachedCurrentPlaying;
            }

            EnsureSubscribed();

            sCachedHandles.Clear();
            AudioKit.GetAllPlayingHandles(sCachedHandles);

            foreach (var handle in sCachedHandles)
            {
                if (handle == null)
                {
                    continue;
                }

                sCachedCurrentPlaying.Add(new AudioPlayRecord
                {
                    Path = handle.Path ?? "Unknown",
                    ChannelId = handle.ChannelId,
                    Volume = handle.Volume,
                    Pitch = handle.Pitch,
                    StartTime = Time.time - handle.Time,
                    Duration = handle.Duration,
                    IsPlaying = handle.IsPlaying,
                    IsPaused = handle.IsPaused,
                    CurrentTime = handle.Time,
                    Progress = handle.Duration > 0 ? handle.Time / handle.Duration : 0f
                });
            }

            return sCachedCurrentPlaying;
        }

        /// <summary>
        /// 记录音频播放事件。
        /// </summary>
        private static void RecordPlay(string path, int channelId, float volume, float pitch, float duration)
        {
            if (!sIsRecording)
            {
                return;
            }

            var record = new AudioPlayRecord
            {
                Path = path ?? "Unknown",
                ChannelId = channelId,
                Volume = volume,
                Pitch = pitch,
                StartTime = Time.time,
                Duration = duration,
                IsPlaying = true,
                IsPaused = false,
                CurrentTime = 0f,
                Progress = 0f
            };

            sPlayHistory.Insert(0, record);

            while (sPlayHistory.Count > sMaxHistoryCount)
            {
                sPlayHistory.RemoveAt(sPlayHistory.Count - 1);
            }
        }

        /// <summary>
        /// 清空播放历史。
        /// </summary>
        public static void ClearHistory()
        {
            sPlayHistory.Clear();
        }

        /// <summary>
        /// 获取通道统计信息。
        /// </summary>
        public static Dictionary<int, ChannelStats> GetChannelStats()
        {
            var stats = new Dictionary<int, ChannelStats>();
            var currentPlaying = GetCurrentPlaying();

            foreach (var record in currentPlaying)
            {
                if (!stats.TryGetValue(record.ChannelId, out var channelStats))
                {
                    channelStats = new ChannelStats
                    {
                        ChannelId = record.ChannelId,
                        PlayingCount = 0,
                        PausedCount = 0,
                        Volume = AudioKit.GetChannelVolume(record.ChannelId)
                    };
                    stats[record.ChannelId] = channelStats;
                }

                if (record.IsPlaying && !record.IsPaused)
                {
                    channelStats.PlayingCount++;
                }
                else if (record.IsPaused)
                {
                    channelStats.PausedCount++;
                }

                stats[record.ChannelId] = channelStats;
            }

            return stats;
        }

        /// <summary>
        /// 通道统计信息。
        /// </summary>
        public struct ChannelStats
        {
            public int ChannelId;
            public int PlayingCount;
            public int PausedCount;
            public float Volume;
        }
    }
}
#endif
