#if !GODOT
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame.Unity
{
    public sealed partial class UnityAudioKitBackend
    {
        public void SetBusVolume(string bus, float volume)
        {
            mBusVolumes[string.IsNullOrEmpty(bus) ? AudioBus.Sfx : bus] = Mathf.Clamp01(volume);
            UpdateActiveVolumes();
        }

        public float GetBusVolume(string bus)
        {
            var normalizedBus = string.IsNullOrEmpty(bus) ? AudioBus.Sfx : bus;
            if (mBusVolumes.TryGetValue(normalizedBus, out var volume))
                return volume;

            return 1f;
        }

        public void Update(float deltaTime)
        {
            for (var i = mVoices.Count - 1; i >= 0; i--)
            {
                var voice = mVoices[i];
                UpdateFollowTarget(voice);
                UpdateFadeIn(voice, deltaTime);
                if (UpdateFadeOut(voice, deltaTime))
                {
                    ReleaseVoiceAt(i);
                    continue;
                }

                if (voice.Source != null && (voice.Loop || voice.Source.isPlaying))
                    continue;

                ReleaseVoiceAt(i);
            }
        }

        public void GetActiveVoices(List<AudioVoiceDebugInfo> result)
        {
            if (result == null)
                return;

            Update(0f);
            result.Clear();
            for (var i = 0; i < mVoices.Count; i++)
                result.Add(BuildDebugInfo(mVoices[i]));
        }

        public void Dispose()
        {
            StopAll();
            while (mSourcePool.Count > 0)
            {
                var source = mSourcePool.Pop();
                if (source != null)
                    DestroyObject(source.gameObject);
            }

            UnloadAll();

            if (mRoot != null)
                DestroyObject(mRoot);
        }
        private AudioSource RentSource()
        {
            EnsureRoot();
            while (mSourcePool.Count > 0)
            {
                var pooled = mSourcePool.Pop();
                if (pooled != null)
                {
                    pooled.gameObject.SetActive(true);
                    return pooled;
                }
            }

            var go = new GameObject("AudioKitVoice");
            go.transform.SetParent(mRoot.transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }

        private void ReturnSource(AudioSource source)
        {
            if (source == null)
                return;

            source.Stop();
            source.clip = null;
            source.loop = false;
            source.pitch = 1f;
            source.volume = 1f;
            source.spatialBlend = 0f;
            source.minDistance = 1f;
            source.maxDistance = 500f;
            source.rolloffMode = UnityEngine.AudioRolloffMode.Logarithmic;
            source.transform.localPosition = Vector3.zero;
            source.gameObject.name = "AudioKitVoice";
            source.gameObject.SetActive(false);
            mSourcePool.Push(source);
        }

        private void ReleaseVoiceAt(int index)
        {
            var voice = mVoices[index];
            mVoices.RemoveAt(index);
            ReturnSource(voice.Source);
        }

        private void UpdateActiveVolumes()
        {
            for (var i = 0; i < mVoices.Count; i++)
            {
                var voice = mVoices[i];
                if (voice.Source != null)
                    ApplyVoiceVolume(voice);
            }
        }

        private float CalculateOutputVolume(VoiceState voice)
        {
            return Mathf.Clamp01(voice.BaseVolume * GetBusVolume(AudioBus.Master) * GetBusVolume(voice.Bus));
        }

        private void ApplyVoiceVolume(VoiceState voice)
        {
            if (voice == null || voice.Source == null)
                return;

            if (voice.IsFadingOut && voice.FadeOutDuration > 0f)
            {
                var progress = Mathf.Clamp01(voice.FadeOutElapsed / voice.FadeOutDuration);
                voice.Source.volume = voice.FadeOutStartVolume * (1f - progress);
                return;
            }

            var targetVolume = CalculateOutputVolume(voice);
            if (voice.IsFadingIn && voice.FadeInDuration > 0f)
            {
                var progress = Mathf.Clamp01(voice.FadeInElapsed / voice.FadeInDuration);
                voice.Source.volume = targetVolume * progress;
                return;
            }

            voice.Source.volume = targetVolume;
        }

        private void UpdateFadeIn(VoiceState voice, float deltaTime)
        {
            if (voice == null || !voice.IsFadingIn)
                return;

            if (deltaTime > 0f)
                voice.FadeInElapsed += deltaTime;

            if (voice.FadeInElapsed >= voice.FadeInDuration)
            {
                voice.FadeInElapsed = voice.FadeInDuration;
                voice.IsFadingIn = false;
            }

            ApplyVoiceVolume(voice);
        }

        private void BeginFadeOut(VoiceState voice, float fadeDuration)
        {
            if (voice == null || voice.Source == null)
                return;

            voice.FadeOutDuration = Mathf.Max(0f, fadeDuration);
            voice.FadeOutElapsed = 0f;
            voice.FadeOutStartVolume = voice.Source.volume;
            voice.IsFadingOut = voice.FadeOutDuration > 0f;
            voice.IsFadingIn = false;
            ApplyVoiceVolume(voice);
        }

        private bool UpdateFadeOut(VoiceState voice, float deltaTime)
        {
            if (voice == null || !voice.IsFadingOut)
                return false;

            if (deltaTime > 0f)
                voice.FadeOutElapsed += deltaTime;

            if (voice.FadeOutElapsed >= voice.FadeOutDuration)
            {
                if (voice.Source != null)
                    voice.Source.Stop();

                return true;
            }

            ApplyVoiceVolume(voice);
            return false;
        }
    }
}
#endif
