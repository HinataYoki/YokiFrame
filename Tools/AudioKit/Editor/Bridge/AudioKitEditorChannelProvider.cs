#if UNITY_EDITOR
using System.Collections.Generic;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// Editor communication metadata for AudioKit runtime monitoring.
    /// </summary>
    internal sealed class AudioKitEditorChannelProvider : IEditorChannelProvider
    {
        public IEnumerable<EditorChannelDefinition> GetChannels()
        {
            yield return new EditorChannelDefinition
            {
                Kit = "AudioKit",
                Channel = DataChannels.AUDIO_PLAY_STARTED,
                DisplayName = "Audio Play Started",
                PayloadType = "(string path, int channelId, float volume, float pitch, float duration)",
                Description = "Published when audio playback starts and forwarded to the editor monitor.",
                SupportsThrottle = true
            };

            yield return new EditorChannelDefinition
            {
                Kit = "AudioKit",
                Channel = DataChannels.AUDIO_PLAY_STOPPED,
                DisplayName = "Audio Play Stopped",
                PayloadType = "(string path, int channelId)",
                Description = "Published when audio playback stops and forwarded to the editor monitor.",
                SupportsThrottle = true
            };

            yield return new EditorChannelDefinition
            {
                Kit = "AudioKit",
                Channel = DataChannels.AUDIO_VOLUME_CHANGED,
                DisplayName = "Audio Volume Changed",
                PayloadType = "(int channel, float volume)",
                Description = "Published when channel volume changes and can drive editor-side mixer monitoring.",
                SupportsThrottle = true
            };
        }
    }
}
#endif
