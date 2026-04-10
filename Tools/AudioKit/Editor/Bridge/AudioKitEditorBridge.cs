#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// AudioKit 的编辑器监控桥接入口。
    /// 负责在 PlayMode 中订阅音频监控服务，并把播放状态同步到 <see cref="EditorDataBridge"/>。
    /// </summary>
    [InitializeOnLoad]
    public static class AudioKitEditorBridge
    {
        private static readonly Bridge sBridge;

        /// <summary>
        /// 初始化 AudioKit 编辑器桥接入口。
        /// </summary>
        static AudioKitEditorBridge()
        {
            sBridge = new Bridge();
        }

        /// <summary>
        /// AudioKit 的具体桥接实现。
        /// </summary>
        private sealed class Bridge : PlayModeEditorBridgeBase
        {
            private bool mIsSubscribed;

            /// <summary>
            /// 进入 PlayMode 后挂接音频播放监控回调。
            /// </summary>
            protected override void OnEnteredPlayMode()
            {
                if (mIsSubscribed)
                {
                    return;
                }

                mIsSubscribed = true;
                AudioMonitorService.OnAudioPlayed += OnAudioPlayed;
                AudioMonitorService.OnAudioStopped += OnAudioStopped;
            }

            /// <summary>
            /// 退出 PlayMode 前卸载音频播放监控回调。
            /// </summary>
            protected override void OnExitingPlayMode()
            {
                if (!mIsSubscribed)
                {
                    return;
                }

                mIsSubscribed = false;
                AudioMonitorService.OnAudioPlayed -= OnAudioPlayed;
                AudioMonitorService.OnAudioStopped -= OnAudioStopped;
            }

            /// <summary>
            /// 推送音频开始播放事件。
            /// </summary>
            private static void OnAudioPlayed(string path, int channelId, float volume, float pitch, float duration)
            {
                EditorDataBridge.NotifyDataChanged(
                    DataChannels.AUDIO_PLAY_STARTED,
                    (path, channelId, volume, pitch, duration));
            }

            /// <summary>
            /// 推送音频停止播放事件。
            /// </summary>
            private static void OnAudioStopped(string path, int channelId)
            {
                EditorDataBridge.NotifyDataChanged(
                    DataChannels.AUDIO_PLAY_STOPPED,
                    (path, channelId));
            }
        }
    }
}
#endif
