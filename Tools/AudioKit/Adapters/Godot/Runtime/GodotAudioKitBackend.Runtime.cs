#if GODOT
using Godot;
using System;
using System.Collections.Generic;
using System.Threading;

namespace YokiFrame.Godot
{
    public sealed partial class GodotAudioKitBackend
    {
        /// <summary>设置逻辑总线有效音量并刷新 active voice。</summary>
        public void SetBusVolume(string bus, float volume)
        {
            mBusVolumes[bus] = Clamp01(volume);
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

                if (!voice.Loop && !IsPlaying(voice)) ReleaseVoiceAt(index);
            }
        }

// 与 IAudioBackend.GetActiveVoices 一致：仅 Godot Tools / 编辑器构建暴露诊断。
#if TOOLS
        /// <summary>按需复制 active voice 诊断状态。</summary>
        public void GetActiveVoices(List<AudioVoiceSnapshot> result)
        {
            result.Clear();
            for (var index = 0; index < mVoices.Count; index++) result.Add(CreateSnapshot(mVoices[index]));
        }

        /// <summary>把 Godot voice 投影为宿主无关诊断 DTO。</summary>
        private AudioVoiceSnapshot CreateSnapshot(VoiceState voice)
        {
            return new AudioVoiceSnapshot
            {
                VoiceId = voice.VoiceId,
                Path = voice.Path,
                Bus = voice.Bus,
                BackendName = BackendName,
                Loop = voice.Loop,
                IsPlaying = IsPlaying(voice),
                IsPaused = voice.Paused,
                Volume = GetCurrentLinearVolume(voice),
                Pitch = voice.Pitch,
                Duration = IsValid(voice.Stream) ? (float)voice.Stream.GetLength() : 0f,
                Elapsed = GetPlaybackPosition(voice),
                Is3D = voice.Is3D,
                Position = voice.Position,
                FollowTargetName = voice.FollowTarget != null ? voice.FollowTarget.Name : string.Empty,
                MinDistance = voice.MinDistance,
                MaxDistance = voice.MaxDistance,
                RolloffMode = voice.RolloffMode
            };
        }
#endif

        /// <summary>释放全部 voice、池、资源和根节点；重复调用保持幂等。</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref mDisposeState, 1) != 0) return;
            mDisposed = true;
            // 各资源阶段独立收口，避免单个宿主异常阻断其它 voice、缓存或根节点的释放。
            try
            {
                StopAll();
            }
            catch (Exception exception)
            {
                TryLogCleanupFailure("Godot StopAll failed during backend disposal", exception);
            }

            try
            {
                UnloadAll();
            }
            catch (Exception exception)
            {
                TryLogCleanupFailure("Godot UnloadAll failed during backend disposal", exception);
            }

            try
            {
                DestroyPooledPlayers();
            }
            catch (Exception exception)
            {
                TryLogCleanupFailure("Godot audio player pool disposal failed", exception);
            }

            try
            {
                DestroyNode(mRoot);
            }
            catch (Exception exception)
            {
                TryLogCleanupFailure("Godot audio root disposal failed", exception);
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
            float output = Clamp01(voice.BaseVolume * GetBusVolume(AudioBus.Master) * GetBusVolume(voice.Bus));
            if (voice.FadingOut && voice.FadeOutDuration > 0f)
            {
                output = voice.FadeOutStartVolume * (1f - Clamp01(voice.FadeOutElapsed / voice.FadeOutDuration));
            }
            else if (voice.FadingIn && voice.FadeInDuration > 0f)
            {
                output *= Clamp01(voice.FadeInElapsed / voice.FadeInDuration);
            }

            float volumeDb = LinearToDb(output);
            if (IsValid(voice.Player2D)) voice.Player2D.VolumeDb = volumeDb;
            if (IsValid(voice.Player3D)) voice.Player3D.VolumeDb = volumeDb;
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
