#if UNITY_2022_3_OR_NEWER
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace YokiFrame.Unity
{
    public sealed partial class UnityAudioKitBackend
    {
        /// <summary>同步解析 AudioClip 并在 Unity 主线程创建 voice。</summary>
        public int Play(string path, AudioPlayOptions options)
        {
            EnsureUsable();
            AudioClip clip = ResolveClip(path);
            if (clip == null)
            {
                LogKit.Warning("[AudioKit] Unity audio resource was not found: " + path);
                return 0;
            }

            return PlayResolved(path, clip, options);
        }

        /// <summary>异步加载 AudioClip，并切回创建后端的 Unity 主线程启动播放。</summary>
        public async Task<int> PlayAsync(string path, AudioPlayOptions options, CancellationToken token)
        {
            EnsureUsable();
            AudioClip clip = await ResolveClipAsync(path, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (clip == null)
            {
                LogKit.Warning("[AudioKit] Unity audio resource was not found: " + path);
                return 0;
            }

            return await InvokeOnUnityThreadAsync(() => PlayResolved(path, clip, options), token).ConfigureAwait(false);
        }

        /// <summary>配置池化 AudioSource、登记 voice 并开始播放。</summary>
        private int PlayResolved(string path, AudioClip clip, AudioPlayOptions options)
        {
            EnsureUsable();
            EnsureUnityThread();
            if (mVoices.Count >= MAX_ACTIVE_VOICES)
            {
                LogKit.Warning("[AudioKit] Unity active voice limit reached: " + MAX_ACTIVE_VOICES);
                return 0;
            }

            PooledAudioSource sourceLease = RentSource();
            VoiceState voice = CreateVoice(path, clip, sourceLease, options);
            ConfigureSource(voice);
            sourceLease.Source.Play();
            mVoices.Add(voice);
            return voice.VoiceId;
        }

        /// <summary>从规范化播放参数创建内部 voice 状态。</summary>
        private VoiceState CreateVoice(
            string path,
            AudioClip clip,
            PooledAudioSource sourceLease,
            AudioPlayOptions options)
        {
            VoiceState voice = new()
            {
                VoiceId = NextVoiceId(),
                Path = path,
                Bus = options.Bus,
                Clip = clip,
                SourceLease = sourceLease,
                BaseVolume = options.Volume,
                Pitch = options.Pitch,
                FadeInDuration = options.FadeInDuration,
                FadeOutDuration = options.FadeOutDuration,
                FadingIn = options.FadeInDuration > 0f,
                Loop = options.Loop,
                Is3D = options.Is3D,
                Position = options.Position,
                FollowTarget = options.FollowTarget,
                MinDistance = options.MinDistance,
                MaxDistance = options.MaxDistance,
                RolloffMode = options.RolloffMode
            };
            return voice;
        }

        /// <summary>把跨宿主 voice 参数映射到 Unity AudioSource。</summary>
        private void ConfigureSource(VoiceState voice)
        {
            AudioSource source = voice.Source;
            source.clip = voice.Clip;
            source.loop = voice.Loop;
            source.pitch = voice.Pitch;
            source.spatialBlend = voice.Is3D ? 1f : 0f;
            source.minDistance = voice.MinDistance;
            source.maxDistance = voice.MaxDistance;
            source.rolloffMode = ToUnityRolloffMode(voice.RolloffMode);
            UpdateFollowTarget(voice);
            ApplyVoiceVolume(voice);
        }

        /// <summary>生成始终为正的后端局部 voice id。</summary>
        private int NextVoiceId()
        {
            mNextVoiceId++;
            if (mNextVoiceId <= 0) mNextVoiceId = 1;
            return mNextVoiceId;
        }

        /// <summary>把跨宿主衰减枚举转换为 Unity 枚举。</summary>
        private static UnityEngine.AudioRolloffMode ToUnityRolloffMode(AudioRolloffMode mode)
        {
            if (mode == AudioRolloffMode.Linear) return UnityEngine.AudioRolloffMode.Linear;
            if (mode == AudioRolloffMode.Custom) return UnityEngine.AudioRolloffMode.Custom;
            return UnityEngine.AudioRolloffMode.Logarithmic;
        }

        /// <summary>确保后端尚未释放。</summary>
        private void EnsureUsable()
        {
            if (mDisposed) throw new ObjectDisposedException(nameof(UnityAudioKitBackend));
        }
    }
}
#endif
