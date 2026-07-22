#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace YokiFrame.Unity
{
    public sealed partial class UnityAudioKitBackend
    {
        /// <summary>设置逻辑总线有效音量并刷新 active voice。</summary>
        public void SetBusVolume(string bus, float volume)
        {
            mBusVolumes[bus] = Mathf.Clamp01(volume);
            for (var index = 0; index < mVoices.Count; index++) ApplyVoiceVolume(mVoices[index]);
        }

        /// <summary>获取后端保存的逻辑总线有效音量。</summary>
        public float GetBusVolume(string bus) => mBusVolumes.TryGetValue(bus, out float volume) ? volume : 1f;

        /// <summary>推进跟随、淡变和自然结束回收；暂停 voice 不参与结束判断。</summary>
        public void Update(float deltaTime)
        {
            for (var index = mVoices.Count - 1; index >= 0; index--)
            {
                VoiceState voice = mVoices[index];
                if (voice.Paused) continue;
                UpdateFollowTarget(voice);
                UpdateFadeIn(voice, deltaTime);
                if (UpdateFadeOut(voice, deltaTime))
                {
                    ReleaseVoiceAt(index);
                    continue;
                }

                if (!voice.Loop && (voice.Source == null || !voice.Source.isPlaying)) ReleaseVoiceAt(index);
            }
        }

#if UNITY_EDITOR
        /// <summary>按需复制 active voice 诊断状态。</summary>
        public void GetActiveVoices(List<AudioVoiceSnapshot> result)
        {
            result.Clear();
            for (var index = 0; index < mVoices.Count; index++) result.Add(CreateSnapshot(mVoices[index]));
        }

        /// <summary>把 Unity voice 状态投影为宿主无关诊断 DTO。</summary>
        private AudioVoiceSnapshot CreateSnapshot(VoiceState voice)
        {
            float elapsed = Mathf.Max(0f, Time.time - voice.StartedAt - voice.TotalPausedTime);
            return new AudioVoiceSnapshot
            {
                VoiceId = voice.VoiceId,
                Path = voice.Path,
                Bus = voice.Bus,
                BackendName = BackendName,
                Loop = voice.Loop,
                IsPlaying = voice.Source != null && voice.Source.isPlaying,
                IsPaused = voice.Paused,
                Volume = voice.Source != null ? voice.Source.volume : 0f,
                Pitch = voice.Pitch,
                Duration = voice.Clip != null ? voice.Clip.length : 0f,
                Elapsed = elapsed,
                Is3D = voice.Is3D,
                Position = voice.Position,
                FollowTargetName = voice.FollowTarget != null ? voice.FollowTarget.Name : string.Empty,
                MinDistance = voice.MinDistance,
                MaxDistance = voice.MaxDistance,
                RolloffMode = voice.RolloffMode
            };
        }
#endif

        /// <summary>释放全部 voice、池、资源和根对象；重复调用保持幂等。</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref mDisposeState, 1) != 0) return;
            mDisposed = true;
            // 各资源阶段独立收口，避免单个宿主异常阻断其它 voice、缓存或根对象的释放。
            try
            {
                StopAll();
            }
            catch (Exception exception)
            {
                TryLogCleanupFailure("Unity StopAll failed during backend disposal", exception);
            }

            try
            {
                UnloadAll();
            }
            catch (Exception exception)
            {
                TryLogCleanupFailure("Unity UnloadAll failed during backend disposal", exception);
            }

            try
            {
                mSourcePool.Dispose();
            }
            catch (Exception exception)
            {
                TryLogCleanupFailure("Unity AudioSource pool disposal failed", exception);
            }

            try
            {
                if (mRoot != null) DestroyObject(mRoot);
            }
            catch (Exception exception)
            {
                TryLogCleanupFailure("Unity audio root disposal failed", exception);
            }
            finally
            {
                mRoot = null;
            }
        }

        /// <summary>尽力记录宿主清理异常；日志后端异常不能覆盖原错误或中断其余释放步骤。</summary>
        private static void TryLogCleanupFailure(string message, Exception exception)
        {
            try
            {
                LogKit.Error("[AudioKit] " + message + ": " + exception);
            }
            catch (Exception)
            {
                // Dispose/Release 必须保持 best-effort 完整清理，不能依赖日志后端仍可用。
            }
        }

        /// <summary>根据 Master、Bus 与淡变状态刷新单个 voice 音量。</summary>
        private void ApplyVoiceVolume(VoiceState voice)
        {
            if (voice == null || voice.Source == null) return;
            float target = Mathf.Clamp01(voice.BaseVolume * GetBusVolume(AudioBus.Master) * GetBusVolume(voice.Bus));
            if (voice.FadingOut && voice.FadeOutDuration > 0f)
            {
                float progress = Mathf.Clamp01(voice.FadeOutElapsed / voice.FadeOutDuration);
                voice.Source.volume = voice.FadeOutStartVolume * (1f - progress);
            }
            else if (voice.FadingIn && voice.FadeInDuration > 0f)
            {
                voice.Source.volume = target * Mathf.Clamp01(voice.FadeInElapsed / voice.FadeInDuration);
            }
            else
            {
                voice.Source.volume = target;
            }
        }

        /// <summary>推进淡入并在完成后恢复稳定目标音量。</summary>
        private void UpdateFadeIn(VoiceState voice, float deltaTime)
        {
            if (!voice.FadingIn) return;
            voice.FadeInElapsed += deltaTime;
            if (voice.FadeInElapsed >= voice.FadeInDuration) voice.FadingIn = false;
            ApplyVoiceVolume(voice);
        }

        /// <summary>推进淡出并报告是否应回收 voice。</summary>
        private bool UpdateFadeOut(VoiceState voice, float deltaTime)
        {
            if (!voice.FadingOut) return false;
            voice.FadeOutElapsed += deltaTime;
            if (voice.FadeOutElapsed >= voice.FadeOutDuration) return true;
            ApplyVoiceVolume(voice);
            return false;
        }
    }
}
#endif
