#if !GODOT && YOKIFRAME_FMOD_SUPPORT
using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// AudioKit 的 Unity FMOD Studio 后端。FMOD 使用事件路径，不走 AudioClip 资源加载。
    /// </summary>
    public sealed class FmodAudioKitBackend : IAudioBackend, IDisposable
    {
        private sealed class VoiceState
        {
            public int VoiceId;
            public string Path;
            public string Bus;
            public FMOD.Studio.EventInstance Instance;
            public FMOD.Studio.EventDescription Description;
            public float BaseVolume;
            public float Pitch;
            public float FadeInDuration;
            public float FadeInElapsed;
            public bool IsFadingIn;
            public float FadeOutDuration;
            public float FadeOutElapsed;
            public float FadeOutStartVolume;
            public bool IsFadingOut;
            public float StartedAt;
            public bool Loop;
            public bool Is3D;
            public YokiVector3 Position;
            public IEngineObject FollowTarget;
            public float MinDistance;
            public float MaxDistance;
            public AudioRolloffMode RolloffMode;
            public int DurationMs;
            public bool IsPaused;
        }

        private readonly Dictionary<string, FMOD.Studio.EventDescription> mDescriptions = new Dictionary<string, FMOD.Studio.EventDescription>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> mBusVolumes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly List<VoiceState> mVoices = new List<VoiceState>(32);
        private readonly List<VoiceState> mVoicesToRemove = new List<VoiceState>(16);
        private int mNextVoiceId;
        private bool mDisposed;

        public FmodAudioKitBackend()
        {
            mBusVolumes[AudioBus.Master] = 1f;
            mBusVolumes[AudioBus.Music] = 1f;
            mBusVolumes[AudioBus.Sfx] = 1f;
            mBusVolumes[AudioBus.Voice] = 1f;
            mBusVolumes[AudioBus.Ambience] = 1f;
            mBusVolumes[AudioBus.UI] = 1f;
        }

        public string BackendName => "Unity.FMOD";

        public AudioVoiceDebugInfo Play(string path, AudioPlayOptions options)
        {
            if (mDisposed)
                return null;

            var normalizedPath = Normalize(path);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                LogKit.Warning("[AudioKit/FMOD] 播放路径为空。");
                return null;
            }

            FMOD.Studio.EventDescription description;
            if (!TryGetEventDescription(normalizedPath, out description))
            {
                LogKit.Warning("[AudioKit/FMOD] 找不到 FMOD 事件: " + normalizedPath);
                return null;
            }

            FMOD.Studio.EventInstance instance;
            var result = description.createInstance(out instance);
            if (result != FMOD.RESULT.OK || !instance.isValid())
            {
                LogKit.Warning("[AudioKit/FMOD] 创建事件实例失败: " + normalizedPath + " " + result);
                return null;
            }

            var voice = CreateVoice(normalizedPath, description, instance, options);
            ApplySpatialOptions(voice);
            instance.setPitch(voice.Pitch);
            ApplyVoiceVolume(voice);

            result = instance.start();
            if (result != FMOD.RESULT.OK)
            {
                instance.release();
                LogKit.Warning("[AudioKit/FMOD] 启动事件失败: " + normalizedPath + " " + result);
                return null;
            }

            mVoices.Add(voice);
            return BuildDebugInfo(voice);
        }

        public void PlayAsync(string path, AudioPlayOptions options, Action<AudioVoiceDebugInfo> onComplete)
        {
            var info = Play(path, options);
            if (onComplete != null)
                onComplete(info);
        }

        public bool Stop(int voiceId)
        {
            for (var i = mVoices.Count - 1; i >= 0; i--)
            {
                if (mVoices[i].VoiceId != voiceId)
                    continue;

                ReleaseVoiceAt(i, FMOD.Studio.STOP_MODE.IMMEDIATE);
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
                ReleaseVoiceAt(i, FMOD.Studio.STOP_MODE.IMMEDIATE);
        }

        public void StopBus(string bus)
        {
            var normalizedBus = NormalizeBus(bus);
            for (var i = mVoices.Count - 1; i >= 0; i--)
            {
                if (string.Equals(mVoices[i].Bus, normalizedBus, StringComparison.OrdinalIgnoreCase))
                    ReleaseVoiceAt(i, FMOD.Studio.STOP_MODE.IMMEDIATE);
            }
        }

        public void PauseAll()
        {
            for (var i = 0; i < mVoices.Count; i++)
            {
                var voice = mVoices[i];
                if (!voice.Instance.isValid())
                    continue;

                voice.Instance.setPaused(true);
                voice.IsPaused = true;
            }
        }

        public void ResumeAll()
        {
            for (var i = 0; i < mVoices.Count; i++)
            {
                var voice = mVoices[i];
                if (!voice.Instance.isValid())
                    continue;

                voice.Instance.setPaused(false);
                voice.IsPaused = false;
            }
        }

        public void Preload(string path)
        {
            var normalizedPath = Normalize(path);
            if (string.IsNullOrEmpty(normalizedPath))
                return;

            FMOD.Studio.EventDescription description;
            if (TryGetEventDescription(normalizedPath, out description))
                description.loadSampleData();
        }

        public void PreloadAsync(string path, Action onComplete)
        {
            Preload(path);
            if (onComplete != null)
                onComplete();
        }

        public void Unload(string path)
        {
            var normalizedPath = Normalize(path);
            if (string.IsNullOrEmpty(normalizedPath))
                return;

            FMOD.Studio.EventDescription description;
            if (mDescriptions.TryGetValue(normalizedPath, out description) && description.isValid())
                description.unloadSampleData();

            mDescriptions.Remove(normalizedPath);
        }

        public void UnloadAll()
        {
            foreach (var pair in mDescriptions)
            {
                if (pair.Value.isValid())
                    pair.Value.unloadSampleData();
            }

            mDescriptions.Clear();
        }

        public void SetResourceProvider(IResourceProvider provider)
        {
            // FMOD 后端使用 Studio 事件路径，不消费 AudioClip 资源提供器。
        }

        public void SetBusVolume(string bus, float volume)
        {
            mBusVolumes[NormalizeBus(bus)] = Mathf.Clamp01(volume);
            UpdateActiveVolumes();
        }

        public float GetBusVolume(string bus)
        {
            float volume;
            return mBusVolumes.TryGetValue(NormalizeBus(bus), out volume) ? volume : 1f;
        }

        public void Update(float deltaTime)
        {
            if (mDisposed)
                return;

            mVoicesToRemove.Clear();
            for (var i = 0; i < mVoices.Count; i++)
            {
                var voice = mVoices[i];
                UpdateFollowTarget(voice);
                UpdateFadeIn(voice, deltaTime);
                if (UpdateFadeOut(voice, deltaTime))
                {
                    mVoicesToRemove.Add(voice);
                    continue;
                }

                if (IsStopped(voice) && !voice.IsPaused)
                    mVoicesToRemove.Add(voice);
            }

            for (var i = 0; i < mVoicesToRemove.Count; i++)
                ReleaseVoice(mVoicesToRemove[i], FMOD.Studio.STOP_MODE.IMMEDIATE);
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
            if (mDisposed)
                return;

            mDisposed = true;
            StopAll();
            UnloadAll();
        }

        private bool TryGetEventDescription(string path, out FMOD.Studio.EventDescription description)
        {
            if (mDescriptions.TryGetValue(path, out description) && description.isValid())
                return true;

            try
            {
                description = RuntimeManager.GetEventDescription(path);
            }
            catch (EventNotFoundException)
            {
                description = default(FMOD.Studio.EventDescription);
                return false;
            }
            catch (Exception exception)
            {
                LogKit.Warning("[AudioKit/FMOD] 获取事件描述失败: " + path + " " + exception.Message);
                description = default(FMOD.Studio.EventDescription);
                return false;
            }

            if (!description.isValid())
                return false;

            mDescriptions[path] = description;
            return true;
        }

        private VoiceState CreateVoice(string path, FMOD.Studio.EventDescription description, FMOD.Studio.EventInstance instance, AudioPlayOptions options)
        {
            var voice = new VoiceState
            {
                VoiceId = ++mNextVoiceId,
                Path = path,
                Bus = NormalizeBus(options.Bus),
                Description = description,
                Instance = instance,
                BaseVolume = Mathf.Clamp01(options.Volume),
                Pitch = options.Pitch <= 0f ? 1f : Mathf.Clamp(options.Pitch, 0.01f, 3f),
                FadeInDuration = Mathf.Max(0f, options.FadeInDuration),
                FadeOutDuration = Mathf.Max(0f, options.FadeOutDuration),
                StartedAt = Time.time,
                Loop = options.Loop,
                Is3D = options.Is3D || options.FollowTarget != null,
                Position = options.FollowTarget != null ? options.FollowTarget.Position : options.Position,
                FollowTarget = options.FollowTarget,
                MinDistance = options.MinDistance <= 0f ? 1f : options.MinDistance,
                MaxDistance = options.MaxDistance <= 0f ? 500f : options.MaxDistance,
                RolloffMode = options.RolloffMode
            };

            if (voice.MaxDistance < voice.MinDistance)
                voice.MaxDistance = voice.MinDistance;

            voice.IsFadingIn = voice.FadeInDuration > 0f;
            description.getLength(out voice.DurationMs);
            return voice;
        }

        private void ReleaseVoiceAt(int index, FMOD.Studio.STOP_MODE stopMode)
        {
            var voice = mVoices[index];
            mVoices.RemoveAt(index);
            ReleaseInstance(voice, stopMode);
        }

        private void ReleaseVoice(VoiceState voice, FMOD.Studio.STOP_MODE stopMode)
        {
            if (voice == null)
                return;

            if (mVoices.Remove(voice))
                ReleaseInstance(voice, stopMode);
        }

        private static void ReleaseInstance(VoiceState voice, FMOD.Studio.STOP_MODE stopMode)
        {
            if (voice.Instance.isValid())
            {
                voice.Instance.stop(stopMode);
                voice.Instance.release();
                voice.Instance.clearHandle();
            }
        }

        private void UpdateActiveVolumes()
        {
            for (var i = 0; i < mVoices.Count; i++)
                ApplyVoiceVolume(mVoices[i]);
        }

        private float CalculateOutputVolume(VoiceState voice)
        {
            return Mathf.Clamp01(voice.BaseVolume * GetBusVolume(AudioBus.Master) * GetBusVolume(voice.Bus));
        }

        private void ApplyVoiceVolume(VoiceState voice)
        {
            if (voice == null || !voice.Instance.isValid())
                return;

            if (voice.IsFadingOut && voice.FadeOutDuration > 0f)
            {
                var progress = Mathf.Clamp01(voice.FadeOutElapsed / voice.FadeOutDuration);
                voice.Instance.setVolume(voice.FadeOutStartVolume * (1f - progress));
                return;
            }

            var targetVolume = CalculateOutputVolume(voice);
            if (voice.IsFadingIn && voice.FadeInDuration > 0f)
            {
                var progress = Mathf.Clamp01(voice.FadeInElapsed / voice.FadeInDuration);
                voice.Instance.setVolume(targetVolume * progress);
                return;
            }

            voice.Instance.setVolume(targetVolume);
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
            if (voice == null || !voice.Instance.isValid())
                return;

            voice.FadeOutDuration = Mathf.Max(0f, fadeDuration);
            voice.FadeOutElapsed = 0f;
            voice.IsFadingOut = voice.FadeOutDuration > 0f;
            voice.IsFadingIn = false;
            voice.Instance.getVolume(out voice.FadeOutStartVolume);
            ApplyVoiceVolume(voice);
        }

        private bool UpdateFadeOut(VoiceState voice, float deltaTime)
        {
            if (voice == null || !voice.IsFadingOut)
                return false;

            if (deltaTime > 0f)
                voice.FadeOutElapsed += deltaTime;

            if (voice.FadeOutElapsed >= voice.FadeOutDuration)
                return true;

            ApplyVoiceVolume(voice);
            return false;
        }

        private static bool IsStopped(VoiceState voice)
        {
            if (voice == null || !voice.Instance.isValid())
                return true;

            FMOD.Studio.PLAYBACK_STATE state;
            voice.Instance.getPlaybackState(out state);
            return state == FMOD.Studio.PLAYBACK_STATE.STOPPED;
        }

        private static void ApplySpatialOptions(VoiceState voice)
        {
            if (voice == null || !voice.Instance.isValid())
                return;

            if (!voice.Is3D)
                return;

            voice.Instance.set3DAttributes(To3DAttributes(voice));
        }

        private static void UpdateFollowTarget(VoiceState voice)
        {
            if (voice == null || !voice.Is3D || voice.FollowTarget == null || !voice.Instance.isValid())
                return;

            voice.Position = voice.FollowTarget.Position;
            voice.Instance.set3DAttributes(To3DAttributes(voice));
        }

        private static FMOD.ATTRIBUTES_3D To3DAttributes(VoiceState voice)
        {
            if (voice.FollowTarget is UnityEngineObject unityObject && unityObject.GameObject != null)
                return RuntimeUtils.To3DAttributes(unityObject.GameObject.transform);

            return RuntimeUtils.To3DAttributes(ToUnityVector3(GetCurrentPosition(voice)));
        }

        private AudioVoiceDebugInfo BuildDebugInfo(VoiceState voice)
        {
            var elapsed = 0f;
            var volume = 0f;
            if (voice.Instance.isValid())
            {
                int timelineMs;
                if (voice.Instance.getTimelinePosition(out timelineMs) == FMOD.RESULT.OK)
                    elapsed = timelineMs / 1000f;

                voice.Instance.getVolume(out volume);
            }

            return new AudioVoiceDebugInfo
            {
                VoiceId = voice.VoiceId,
                Path = voice.Path,
                ClipName = voice.Path,
                Bus = voice.Bus,
                BackendName = BackendName,
                Loop = voice.Loop,
                IsPlaying = !IsStopped(voice) && !voice.IsPaused,
                Volume = volume,
                Pitch = voice.Pitch,
                FadeOutDuration = voice.FadeOutDuration,
                StartedAt = voice.StartedAt,
                Duration = voice.DurationMs / 1000f,
                Elapsed = elapsed,
                Is3D = voice.Is3D,
                Position = GetCurrentPosition(voice),
                HasFollowTarget = voice.FollowTarget != null,
                FollowTargetName = voice.FollowTarget != null ? voice.FollowTarget.Name : string.Empty,
                MinDistance = voice.MinDistance,
                MaxDistance = voice.MaxDistance,
                RolloffMode = voice.RolloffMode
            };
        }

        private static YokiVector3 GetCurrentPosition(VoiceState voice)
        {
            if (voice.FollowTarget != null)
                return voice.FollowTarget.Position;

            return voice.Position;
        }

        private static Vector3 ToUnityVector3(YokiVector3 position)
        {
            return new Vector3(position.X, position.Y, position.Z);
        }

        private static string NormalizeBus(string bus)
        {
            return string.IsNullOrEmpty(bus) ? AudioBus.Sfx : bus;
        }

        private static string Normalize(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
#endif
