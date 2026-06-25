#if !GODOT
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// AudioKit 的 Unity AudioSource 后端。资源来源可由测试脚本或项目启动代码显式注册。
    /// </summary>
    public sealed partial class UnityAudioKitBackend : IAudioBackend, IDisposable
    {
        private sealed class VoiceState
        {
            public int VoiceId;
            public string Path;
            public string Bus;
            public AudioClip Clip;
            public AudioSource Source;
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
        }

        private readonly Dictionary<string, AudioClip> mClips = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<AudioClip, IAudioResourceLoader> mClipLoaders = new();
        private readonly Dictionary<string, float> mBusVolumes = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<VoiceState> mVoices = new(32);
        private readonly Stack<AudioSource> mSourcePool = new(16);

        private IAudioResourceLoader mResourceLoader;
        private GameObject mRoot;
        private int mNextVoiceId;

        public UnityAudioKitBackend()
        {
            mBusVolumes[AudioBus.Master] = 1f;
            mBusVolumes[AudioBus.Music] = 1f;
            mBusVolumes[AudioBus.Sfx] = 1f;
            mBusVolumes[AudioBus.Voice] = 1f;
            mBusVolumes[AudioBus.Ambience] = 1f;
            mBusVolumes[AudioBus.UI] = 1f;
        }

        public string BackendName => "Unity.AudioSource";
        public int RegisteredClipCount => mClips.Count;

        public void RegisterClip(string path, AudioClip clip)
        {
            if (clip == null)
                return;

            RegisterAlias(path, clip);
            RegisterAlias(RemoveExtension(path), clip);
            RegisterAlias(clip.name, clip);
        }
    }
}
#endif
