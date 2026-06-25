#if !GODOT
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame.Unity
{
    public sealed partial class UnityAudioKitBackend
    {
        public bool Stop(int voiceId)
        {
            for (var i = mVoices.Count - 1; i >= 0; i--)
            {
                if (mVoices[i].VoiceId != voiceId)
                    continue;

                ReleaseVoiceAt(i);
                return true;
            }

            return false;
        }

        public bool StopWithFade(int voiceId, float fadeDuration)
        {
            if (fadeDuration <= 0f)
                return Stop(voiceId);

            for (var i = mVoices.Count - 1; i >= 0; i--)
            {
                var voice = mVoices[i];
                if (voice.VoiceId != voiceId)
                    continue;

                BeginFadeOut(voice, fadeDuration);
                return true;
            }

            return false;
        }

        public void StopAll()
        {
            for (var i = mVoices.Count - 1; i >= 0; i--)
                ReleaseVoiceAt(i);
        }

        public void StopBus(string bus)
        {
            var normalizedBus = string.IsNullOrEmpty(bus) ? AudioBus.Sfx : bus;
            for (var i = mVoices.Count - 1; i >= 0; i--)
            {
                if (string.Equals(mVoices[i].Bus, normalizedBus, StringComparison.OrdinalIgnoreCase))
                    ReleaseVoiceAt(i);
            }
        }

        public void PauseAll()
        {
            for (var i = 0; i < mVoices.Count; i++)
            {
                var source = mVoices[i].Source;
                if (source != null)
                    source.Pause();
            }
        }

        public void ResumeAll()
        {
            for (var i = 0; i < mVoices.Count; i++)
            {
                var source = mVoices[i].Source;
                if (source != null)
                    source.UnPause();
            }
        }
    }
}
#endif
