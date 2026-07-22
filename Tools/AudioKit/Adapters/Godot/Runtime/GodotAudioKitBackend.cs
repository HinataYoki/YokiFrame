#if GODOT
using Godot;
using System;
using System.Collections.Generic;
using System.Threading;
using NumericsVector3 = System.Numerics.Vector3;

namespace YokiFrame.Godot
{
    /// <summary>使用 Godot AudioStreamPlayer 实现 AudioKit 默认播放后端。</summary>
    public sealed partial class GodotAudioKitBackend : IAudioBackend, IAudioBackendAsyncCleanup
    {
        private const int MAX_ACTIVE_VOICES = 128;
        private const int MAX_POOLED_PLAYERS = 32;

        private sealed class CachedStream
        {
            internal AudioStream Stream;
            internal IAudioResourceLoader Loader;
        }

        private sealed class PooledAudioPlayer2D : IDisposable
        {
            internal AudioStreamPlayer Player;

            /// <summary>创建持有 Godot 二维播放器节点的 PoolKit 租约。</summary>
            internal PooledAudioPlayer2D(AudioStreamPlayer player)
            {
                Player = player;
            }

            /// <summary>在池容量溢出或后端释放时销毁仍有效的 Godot 节点。</summary>
            public void Dispose()
            {
                if (IsValid(Player)) DestroyNode(Player);
                Player = null;
            }
        }

        private sealed class PooledAudioPlayer3D : IDisposable
        {
            internal AudioStreamPlayer3D Player;

            /// <summary>创建持有 Godot 三维播放器节点的 PoolKit 租约。</summary>
            internal PooledAudioPlayer3D(AudioStreamPlayer3D player)
            {
                Player = player;
            }

            /// <summary>在池容量溢出或后端释放时销毁仍有效的 Godot 节点。</summary>
            public void Dispose()
            {
                if (IsValid(Player)) DestroyNode(Player);
                Player = null;
            }
        }

        private sealed class VoiceState
        {
            internal int VoiceId;
            internal string Path;
            internal string Bus;
            internal AudioStream Stream;
            internal PooledAudioPlayer2D Player2DLease;
            internal PooledAudioPlayer3D Player3DLease;
            internal AudioStreamPlayer Player2D => Player2DLease == null ? null : Player2DLease.Player;
            internal AudioStreamPlayer3D Player3D => Player3DLease == null ? null : Player3DLease.Player;
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
            internal float StartedAt;
            internal bool Is3D;
            internal NumericsVector3 Position;
            internal IAudioFollowTarget FollowTarget;
            internal float MinDistance;
            internal float MaxDistance;
            internal AudioRolloffMode RolloffMode;
        }

        private readonly Dictionary<string, CachedStream> mStreams = new(StringComparer.OrdinalIgnoreCase);
        private readonly object mStreamLock = new();
        private readonly Dictionary<string, float> mBusVolumes = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<VoiceState> mVoices = new(32);
        private readonly ObjectPool<PooledAudioPlayer2D> mPlayer2DPool;
        private readonly ObjectPool<PooledAudioPlayer3D> mPlayer3DPool;
        private readonly SynchronizationContext mGodotContext;
        private readonly int mGodotThreadId;
        private Node mRoot;
        private int mNextVoiceId;
        private volatile bool mDisposed;
        private int mDisposeState;

        /// <summary>创建后端并捕获 Godot 主线程上下文。</summary>
        public GodotAudioKitBackend()
        {
            mGodotContext = SynchronizationContext.Current;
            mGodotThreadId = Thread.CurrentThread.ManagedThreadId;
            mPlayer2DPool = PoolKit.Create(
                CreatePlayer2DLease,
                ActivatePlayer2DLease,
                ResetPlayer2DLease,
                new PoolOptions(0, MAX_POOLED_PLAYERS));
            mPlayer3DPool = PoolKit.Create(
                CreatePlayer3DLease,
                ActivatePlayer3DLease,
                ResetPlayer3DLease,
                new PoolOptions(0, MAX_POOLED_PLAYERS));
            InitializeBusVolumes();
        }

        /// <summary>获取稳定 Godot 后端名称。</summary>
        public string BackendName => "Godot.AudioStreamPlayer";

        /// <summary>声明 Godot 默认后端支持异步、循环、空间、跟随和预加载；Custom Rolloff 不作统一承诺。</summary>
        public AudioBackendCapabilities Capabilities => AudioBackendCapabilities.All & ~AudioBackendCapabilities.RolloffOverride;

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
