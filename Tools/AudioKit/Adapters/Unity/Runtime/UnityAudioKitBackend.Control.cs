#if UNITY_2022_3_OR_NEWER
using System;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame.Unity
{
    public sealed partial class UnityAudioKitBackend
    {
        /// <summary>立即释放指定 voice。</summary>
        public bool Stop(int voiceId)
        {
            int index = FindVoiceIndex(voiceId);
            if (index < 0) return false;
            ReleaseVoiceAt(index);
            return true;
        }

        /// <summary>把异步启动失败后的 voice 清理封送回 Unity 主线程；后端已释放时视为已清理。</summary>
        Task IAudioBackendAsyncCleanup.StopVoiceAsync(int voiceId)
        {
            if (voiceId <= 0 || mDisposed) return Task.CompletedTask;
            return InvokeOnUnityThreadAsync(() =>
            {
                if (!mDisposed) Stop(voiceId);
                return true;
            }, CancellationToken.None);
        }

        /// <summary>立即停止或开始指定时长淡出。</summary>
        public bool StopWithFade(int voiceId, float fadeDuration)
        {
            int index = FindVoiceIndex(voiceId);
            if (index < 0) return false;
            if (fadeDuration <= 0f)
            {
                ReleaseVoiceAt(index);
                return true;
            }

            VoiceState voice = mVoices[index];
            voice.FadeOutDuration = fadeDuration;
            voice.FadeOutElapsed = 0f;
            voice.FadeOutStartVolume = voice.Source != null ? voice.Source.volume : 0f;
            voice.FadingOut = true;
            voice.FadingIn = false;
            return true;
        }

        /// <summary>立即停止并回收全部 active voice。</summary>
        public void StopAll()
        {
            for (var index = mVoices.Count - 1; index >= 0; index--) ReleaseVoiceAt(index);
        }

        /// <summary>停止指定逻辑总线的全部 voice。</summary>
        public void StopBus(string bus)
        {
            for (var index = mVoices.Count - 1; index >= 0; index--)
            {
                if (string.Equals(mVoices[index].Bus, bus, StringComparison.OrdinalIgnoreCase)) ReleaseVoiceAt(index);
            }
        }

        /// <summary>暂停全部 active voice 并标记暂停态，避免更新阶段误回收。</summary>
        public void PauseAll()
        {
            for (var index = 0; index < mVoices.Count; index++)
            {
                VoiceState voice = mVoices[index];
                if (voice.Paused || voice.Source == null) continue;
                voice.Source.Pause();
                voice.Paused = true;
            }
        }

        /// <summary>恢复全部暂停 voice。</summary>
        public void ResumeAll()
        {
            for (var index = 0; index < mVoices.Count; index++)
            {
                VoiceState voice = mVoices[index];
                if (!voice.Paused || voice.Source == null) continue;
                voice.Source.UnPause();
                voice.Paused = false;
            }
        }

        /// <summary>按 voice id 查找 active 列表索引。</summary>
        private int FindVoiceIndex(int voiceId)
        {
            for (var index = 0; index < mVoices.Count; index++)
            {
                if (mVoices[index].VoiceId == voiceId) return index;
            }

            return -1;
        }
    }
}
#endif
