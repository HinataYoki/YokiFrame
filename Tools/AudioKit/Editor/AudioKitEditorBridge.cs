#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// AudioKit 编辑器桥接 - 订阅运行时事件并转发到 EditorDataBridge
    /// 实现响应式编辑器更新，运行时零侵入
    /// </summary>
    [InitializeOnLoad]
    public static class AudioKitEditorBridge
    {
        private static bool sIsSubscribed;

        static AudioKitEditorBridge()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    Subscribe();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    Unsubscribe();
                    break;
            }
        }

        private static void Subscribe()
        {
            if (sIsSubscribed) return;
            sIsSubscribed = true;

            AudioMonitorService.OnAudioPlayed += OnAudioPlayed;
            AudioMonitorService.OnAudioStopped += OnAudioStopped;
        }

        private static void Unsubscribe()
        {
            if (!sIsSubscribed) return;
            sIsSubscribed = false;

            AudioMonitorService.OnAudioPlayed -= OnAudioPlayed;
            AudioMonitorService.OnAudioStopped -= OnAudioStopped;
        }

        #region 事件处理

        private static void OnAudioPlayed(string path, int channelId, float volume, float pitch, float duration)
        {
            EditorDataBridge.NotifyDataChanged(
                DataChannels.AUDIO_PLAY_STARTED,
                (path, channelId, volume, pitch, duration));
        }

        private static void OnAudioStopped(string path, int channelId)
        {
            EditorDataBridge.NotifyDataChanged(
                DataChannels.AUDIO_PLAY_STOPPED,
                (path, channelId));
        }

        #endregion
    }
}
#endif
