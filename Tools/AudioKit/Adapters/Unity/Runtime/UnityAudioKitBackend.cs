#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using NumericsVector3 = System.Numerics.Vector3;

namespace YokiFrame.Unity
{
    /// <summary>使用 Unity AudioSource 实现 AudioKit 默认播放后端。</summary>
    public sealed partial class UnityAudioKitBackend : IAudioBackend, IAudioBackendAsyncCleanup
    {
        private const int MAX_ACTIVE_VOICES = 128;
        private const int MAX_POOLED_SOURCES = 32;

        private sealed class CachedClip
        {
            internal AudioClip Clip;
            internal IAudioResourceLoader Loader;
        }

        private sealed class PooledAudioSource : IDisposable
        {
            internal AudioSource Source;

            /// <summary>创建一个持有 Unity AudioSource 的 PoolKit 元素。</summary>
            internal PooledAudioSource(AudioSource source)
            {
                Source = source;
            }

            /// <summary>池容量溢出或池释放时销毁 Unity AudioSource 宿主对象。</summary>
            public void Dispose()
            {
                if (Source == null)
                {
                    return;
                }

                DestroyObject(Source.gameObject);
                Source = null;
            }
        }

        private sealed class VoiceState
        {
            internal int VoiceId;
            internal string Path;
            internal string Bus;
            internal AudioClip Clip;
            internal PooledAudioSource SourceLease;
            internal AudioSource Source => SourceLease == null ? null : SourceLease.Source;
            internal float BaseVolume;
            internal float Pitch;
            internal float FadeInDuration;
            internal float FadeInElapsed;
            internal float FadeOutDuration;
            internal float FadeOutElapsed;
            internal float FadeOutStartVolume;
            internal bool FadingIn;
            internal bool FadingOut;
            internal bool Paused;
            internal bool Loop;
            internal bool Is3D;
            internal NumericsVector3 Position;
            internal IAudioFollowTarget FollowTarget;
            internal float MinDistance;
            internal float MaxDistance;
            internal AudioRolloffMode RolloffMode;
        }

        private readonly Dictionary<string, CachedClip> mClips = new(StringComparer.OrdinalIgnoreCase);
        private readonly object mClipLock = new();
        private readonly Dictionary<string, float> mBusVolumes = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<VoiceState> mVoices = new(32);
        private readonly ObjectPool<PooledAudioSource> mSourcePool;
        private readonly SynchronizationContext mUnityContext;
        private readonly int mUnityThreadId;
        private GameObject mRoot;
        private int mNextVoiceId;
        private volatile bool mDisposed;
        private int mDisposeState;

        /// <summary>创建后端并捕获首次业务调用所在的 Unity 主线程上下文。</summary>
        public UnityAudioKitBackend()
        {
            mUnityContext = SynchronizationContext.Current;
            mUnityThreadId = Thread.CurrentThread.ManagedThreadId;
            mSourcePool = PoolKit.Create(
                CreateSourceLease,
                null,
                ResetSourceLease,
                new PoolOptions(0, MAX_POOLED_SOURCES));
            InitializeBusVolumes();
        }

        /// <summary>获取稳定 Unity 后端名称。</summary>
        public string BackendName => "Unity.AudioSource";

        /// <summary>获取 Unity AudioSource 后端真实支持的播放语义。</summary>
        public AudioBackendCapabilities Capabilities => AudioBackendCapabilities.All;

        /// <summary>初始化全部默认逻辑总线音量。</summary>
        private void InitializeBusVolumes()
        {
            mBusVolumes[AudioBus.Master] = 1f;
            mBusVolumes[AudioBus.Music] = 1f;
            mBusVolumes[AudioBus.Sfx] = 1f;
            mBusVolumes[AudioBus.Voice] = 1f;
            mBusVolumes[AudioBus.Ambience] = 1f;
            mBusVolumes[AudioBus.UI] = 1f;
        }
    }
}
#endif
