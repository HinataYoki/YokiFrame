#if GODOT
using Godot;
using System;
using System.Collections.Generic;
using YokiFrame;

namespace YokiFrame.Godot
{
    /// <summary>
    /// AudioKit 的 Godot AudioStreamPlayer 后端。
    /// </summary>
    public sealed class GodotAudioKitBackend : IAudioBackend, IDisposable
    {
        private sealed class VoiceState
        {
            public int VoiceId;
            public string Path;
            public string Bus;
            public AudioStream Stream;
            public AudioStreamPlayer Player2D;
            public AudioStreamPlayer3D Player3D;
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

        private readonly Dictionary<string, AudioStream> mStreams = new Dictionary<string, AudioStream>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<AudioStream, IAudioResourceLoader> mStreamLoaders = new Dictionary<AudioStream, IAudioResourceLoader>();
        private readonly Dictionary<string, float> mBusVolumes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly List<VoiceState> mVoices = new List<VoiceState>(32);
        private readonly Stack<AudioStreamPlayer> mPlayer2DPool = new Stack<AudioStreamPlayer>(16);
        private readonly Stack<AudioStreamPlayer3D> mPlayer3DPool = new Stack<AudioStreamPlayer3D>(16);

        private IAudioResourceLoader mResourceLoader;
        private Node mRoot;
        private int mNextVoiceId;

        public GodotAudioKitBackend()
        {
            mBusVolumes[AudioBus.Master] = 1f;
            mBusVolumes[AudioBus.Music] = 1f;
            mBusVolumes[AudioBus.Sfx] = 1f;
            mBusVolumes[AudioBus.Voice] = 1f;
            mBusVolumes[AudioBus.Ambience] = 1f;
            mBusVolumes[AudioBus.UI] = 1f;
        }

        public string BackendName
        {
            get { return "Godot.AudioStreamPlayer"; }
        }

        public AudioVoiceDebugInfo Play(string path, AudioPlayOptions options)
        {
            var stream = ResolveStream(path);
            if (stream == null)
            {
                LogKit.Warning("[AudioKit] 找不到 Godot 音频资源: " + path);
                return null;
            }

            return PlayResolvedStream(path, stream, options);
        }

        public async void PlayAsync(string path, AudioPlayOptions options, Action<AudioVoiceDebugInfo> onComplete)
        {
            try
            {
                var stream = await ResolveStreamAsync(path);
                if (stream == null)
                {
                    LogKit.Warning("[AudioKit] 异步加载 Godot 音频资源失败: " + path);
                    if (onComplete != null)
                        onComplete(null);
                    return;
                }

                var info = PlayResolvedStream(path, stream, options);
                if (onComplete != null)
                    onComplete(info);
            }
            catch (Exception exception)
            {
                LogKit.Warning("[AudioKit] Godot 异步播放失败: " + path + " " + exception.Message);
                if (onComplete != null)
                    onComplete(null);
            }
        }

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
                var voice = mVoices[i];
                if (voice.Player2D != null)
                    voice.Player2D.StreamPaused = true;
                if (voice.Player3D != null)
                    voice.Player3D.StreamPaused = true;
            }
        }

        public void ResumeAll()
        {
            for (var i = 0; i < mVoices.Count; i++)
            {
                var voice = mVoices[i];
                if (voice.Player2D != null)
                    voice.Player2D.StreamPaused = false;
                if (voice.Player3D != null)
                    voice.Player3D.StreamPaused = false;
            }
        }

        public void Preload(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            var key = Normalize(path);
            if (mStreams.ContainsKey(key))
                return;

            var stream = LoadStream(key);
            if (stream == null)
                LogKit.Warning("[AudioKit] 预加载 Godot 音频资源失败: " + path);
        }

        public void PreloadAsync(string path, Action onComplete)
        {
            Preload(path);
            if (onComplete != null)
                onComplete();
        }

        public void Unload(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            var key = Normalize(path);
            AudioStream stream;
            if (!mStreams.TryGetValue(key, out stream) || stream == null)
            {
                mStreams.Remove(key);
                mStreams.Remove(RemoveExtension(key));
                return;
            }

            ReleaseProviderStream(stream);
            RemoveStreamAliases(stream);
        }

        public void UnloadAll()
        {
            ReleaseAllProviderStreams();
            mStreams.Clear();
        }

        public void SetResourceProvider(IResourceProvider provider)
        {
            mResourceLoader = provider != null ? new ResourceProviderAudioResourceLoader(provider) : null;
        }

        public void SetBusVolume(string bus, float volume)
        {
            mBusVolumes[string.IsNullOrEmpty(bus) ? AudioBus.Sfx : bus] = Clamp01(volume);
            UpdateActiveVolumes();
        }

        public float GetBusVolume(string bus)
        {
            var normalizedBus = string.IsNullOrEmpty(bus) ? AudioBus.Sfx : bus;
            float volume;
            return mBusVolumes.TryGetValue(normalizedBus, out volume) ? volume : 1f;
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

                if (voice.Loop || IsVoicePlaying(voice))
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
            while (mPlayer2DPool.Count > 0)
            {
                var player = mPlayer2DPool.Pop();
                DestroyNode(player);
            }

            while (mPlayer3DPool.Count > 0)
            {
                var player = mPlayer3DPool.Pop();
                DestroyNode(player);
            }

            UnloadAll();
            DestroyNode(mRoot);
            mRoot = null;
        }

        private AudioVoiceDebugInfo PlayResolvedStream(string path, AudioStream stream, AudioPlayOptions options)
        {
            var is3D = options.Is3D || options.FollowTarget != null;
            var voice = new VoiceState
            {
                VoiceId = ++mNextVoiceId,
                Path = Normalize(path),
                Bus = string.IsNullOrEmpty(options.Bus) ? AudioBus.Sfx : options.Bus,
                Stream = stream,
                BaseVolume = Clamp01(options.Volume),
                Pitch = options.Pitch <= 0f ? 1f : options.Pitch,
                FadeInDuration = Math.Max(0f, options.FadeInDuration),
                FadeOutDuration = Math.Max(0f, options.FadeOutDuration),
                StartedAt = GetNow(),
                Loop = options.Loop,
                Is3D = is3D,
                Position = options.FollowTarget != null ? options.FollowTarget.Position : options.Position,
                FollowTarget = options.FollowTarget,
                MinDistance = options.MinDistance <= 0f ? 1f : options.MinDistance,
                MaxDistance = options.MaxDistance <= 0f ? 500f : options.MaxDistance,
                RolloffMode = options.RolloffMode
            };
            if (voice.MaxDistance < voice.MinDistance)
                voice.MaxDistance = voice.MinDistance;

            voice.IsFadingIn = voice.FadeInDuration > 0f;
            if (voice.Is3D)
            {
                voice.Player3D = RentPlayer3D();
                ConfigurePlayer3D(voice.Player3D, voice);
                voice.Player3D.Play();
            }
            else
            {
                voice.Player2D = RentPlayer2D();
                ConfigurePlayer2D(voice.Player2D, voice);
                voice.Player2D.Play();
            }

            mVoices.Add(voice);
            return BuildDebugInfo(voice);
        }

        private AudioStream ResolveStream(string path)
        {
            var key = Normalize(path);
            AudioStream stream;
            if (mStreams.TryGetValue(key, out stream) && stream != null)
                return stream;

            return LoadStream(key);
        }

        private async System.Threading.Tasks.Task<AudioStream> ResolveStreamAsync(string path)
        {
            var key = Normalize(path);
            AudioStream cachedStream;
            if (mStreams.TryGetValue(key, out cachedStream) && cachedStream != null)
                return cachedStream;

            var loader = GetEffectiveResourceLoader();
            if (loader != null)
            {
                var loadedStream = await loader.LoadAsync<AudioStream>(key);
                if (loadedStream != null)
                {
                    RegisterStream(key, loadedStream);
                    mStreamLoaders[loadedStream] = loader;
                    return loadedStream;
                }
            }

            var godotStream = ResourceLoader.Load<AudioStream>(key);
            if (godotStream != null)
            {
                RegisterStream(key, godotStream);
                return godotStream;
            }

            return null;
        }

        private AudioStream LoadStream(string key)
        {
            var loader = GetEffectiveResourceLoader();
            if (loader != null)
            {
                var loadedStream = loader.Load<AudioStream>(key);
                if (loadedStream != null)
                {
                    RegisterStream(key, loadedStream);
                    mStreamLoaders[loadedStream] = loader;
                    return loadedStream;
                }
            }

            var godotStream = ResourceLoader.Load<AudioStream>(key);
            if (godotStream != null)
            {
                RegisterStream(key, godotStream);
                return godotStream;
            }

            return null;
        }

        private IAudioResourceLoader GetEffectiveResourceLoader()
        {
            return mResourceLoader ?? AudioKit.GetResourceLoader();
        }

        private void RegisterStream(string path, AudioStream stream)
        {
            if (stream == null)
                return;

            RegisterAlias(path, stream);
            RegisterAlias(RemoveExtension(path), stream);
            RegisterAlias(stream.ResourcePath, stream);
        }

        private void RegisterAlias(string path, AudioStream stream)
        {
            var key = Normalize(path);
            if (string.IsNullOrEmpty(key) || stream == null)
                return;

            mStreams[key] = stream;
        }

        private void RemoveStreamAliases(AudioStream stream)
        {
            var keys = new List<string>();
            foreach (var pair in mStreams)
            {
                if (ReferenceEquals(pair.Value, stream))
                    keys.Add(pair.Key);
            }

            for (var i = 0; i < keys.Count; i++)
                mStreams.Remove(keys[i]);
        }

        private void ReleaseProviderStream(AudioStream stream)
        {
            if (stream == null)
                return;

            IAudioResourceLoader loader;
            if (!mStreamLoaders.TryGetValue(stream, out loader))
                return;

            mStreamLoaders.Remove(stream);
            loader.Release(stream);
        }

        private void ReleaseAllProviderStreams()
        {
            var streams = new List<AudioStream>(mStreamLoaders.Keys);
            for (var i = 0; i < streams.Count; i++)
                ReleaseProviderStream(streams[i]);
        }

        private AudioStreamPlayer RentPlayer2D()
        {
            EnsureRoot();
            while (mPlayer2DPool.Count > 0)
            {
                var pooled = mPlayer2DPool.Pop();
                if (IsInstanceValid(pooled))
                {
                    pooled.ProcessMode = Node.ProcessModeEnum.Inherit;
                    return pooled;
                }
            }

            var player = new AudioStreamPlayer();
            player.Name = "AudioKitVoice";
            mRoot.AddChild(player);
            return player;
        }

        private AudioStreamPlayer3D RentPlayer3D()
        {
            EnsureRoot();
            while (mPlayer3DPool.Count > 0)
            {
                var pooled = mPlayer3DPool.Pop();
                if (IsInstanceValid(pooled))
                {
                    pooled.ProcessMode = Node.ProcessModeEnum.Inherit;
                    return pooled;
                }
            }

            var player = new AudioStreamPlayer3D();
            player.Name = "AudioKitVoice3D";
            mRoot.AddChild(player);
            return player;
        }

        private void ReturnPlayer2D(AudioStreamPlayer player)
        {
            if (!IsInstanceValid(player))
                return;

            player.Stop();
            player.Stream = null;
            player.Bus = AudioBus.Master;
            player.VolumeDb = 0f;
            player.PitchScale = 1f;
            player.StreamPaused = false;
            player.Name = "AudioKitVoice";
            player.ProcessMode = Node.ProcessModeEnum.Disabled;
            mPlayer2DPool.Push(player);
        }

        private void ReturnPlayer3D(AudioStreamPlayer3D player)
        {
            if (!IsInstanceValid(player))
                return;

            player.Stop();
            player.Stream = null;
            player.Bus = AudioBus.Master;
            player.VolumeDb = 0f;
            player.PitchScale = 1f;
            player.StreamPaused = false;
            player.GlobalPosition = Vector3.Zero;
            player.UnitSize = 1f;
            player.MaxDistance = 0f;
            player.Name = "AudioKitVoice3D";
            player.ProcessMode = Node.ProcessModeEnum.Disabled;
            mPlayer3DPool.Push(player);
        }

        private void ReleaseVoiceAt(int index)
        {
            var voice = mVoices[index];
            mVoices.RemoveAt(index);
            if (voice.Player2D != null)
                ReturnPlayer2D(voice.Player2D);
            if (voice.Player3D != null)
                ReturnPlayer3D(voice.Player3D);
        }

        private void ConfigurePlayer2D(AudioStreamPlayer player, VoiceState voice)
        {
            player.Stream = voice.Stream;
            player.Bus = voice.Bus;
            player.PitchScale = voice.Pitch;
            player.StreamPaused = false;
            ApplyVoiceVolume(voice);
        }

        private void ConfigurePlayer3D(AudioStreamPlayer3D player, VoiceState voice)
        {
            player.Stream = voice.Stream;
            player.Bus = voice.Bus;
            player.PitchScale = voice.Pitch;
            player.StreamPaused = false;
            player.UnitSize = voice.MinDistance;
            player.MaxDistance = voice.MaxDistance;
            player.GlobalPosition = ToGodotVector3(GetCurrentPosition(voice));
            ApplyVoiceVolume(voice);
        }

        private void UpdateActiveVolumes()
        {
            for (var i = 0; i < mVoices.Count; i++)
                ApplyVoiceVolume(mVoices[i]);
        }

        private float CalculateOutputVolume(VoiceState voice)
        {
            return Clamp01(voice.BaseVolume * GetBusVolume(AudioBus.Master) * GetBusVolume(voice.Bus));
        }

        private void ApplyVoiceVolume(VoiceState voice)
        {
            if (voice == null)
                return;

            var linearVolume = CalculateOutputVolume(voice);
            if (voice.IsFadingOut && voice.FadeOutDuration > 0f)
            {
                var progress = Clamp01(voice.FadeOutElapsed / voice.FadeOutDuration);
                linearVolume = voice.FadeOutStartVolume * (1f - progress);
            }
            else if (voice.IsFadingIn && voice.FadeInDuration > 0f)
            {
                var progress = Clamp01(voice.FadeInElapsed / voice.FadeInDuration);
                linearVolume *= progress;
            }

            var volumeDb = LinearToDb(linearVolume);
            if (voice.Player2D != null)
                voice.Player2D.VolumeDb = volumeDb;
            if (voice.Player3D != null)
                voice.Player3D.VolumeDb = volumeDb;
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
            if (voice == null)
                return;

            voice.FadeOutDuration = Math.Max(0f, fadeDuration);
            voice.FadeOutElapsed = 0f;
            voice.FadeOutStartVolume = GetCurrentLinearVolume(voice);
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
                StopVoicePlayer(voice);
                return true;
            }

            ApplyVoiceVolume(voice);
            return false;
        }

        private AudioVoiceDebugInfo BuildDebugInfo(VoiceState voice)
        {
            var position = GetCurrentPosition(voice);
            return new AudioVoiceDebugInfo
            {
                VoiceId = voice.VoiceId,
                Path = voice.Path,
                ClipName = GetStreamName(voice.Stream),
                Bus = voice.Bus,
                BackendName = BackendName,
                Loop = voice.Loop,
                IsPlaying = IsVoicePlaying(voice),
                Volume = GetCurrentLinearVolume(voice),
                Pitch = voice.Pitch,
                FadeOutDuration = voice.FadeOutDuration,
                StartedAt = voice.StartedAt,
                Duration = GetStreamLength(voice.Stream),
                Elapsed = GetPlaybackPosition(voice),
                Is3D = voice.Is3D,
                Position = position,
                HasFollowTarget = voice.FollowTarget != null,
                FollowTargetName = voice.FollowTarget != null ? voice.FollowTarget.Name : string.Empty,
                MinDistance = voice.MinDistance,
                MaxDistance = voice.MaxDistance,
                RolloffMode = voice.RolloffMode
            };
        }

        private void UpdateFollowTarget(VoiceState voice)
        {
            if (voice == null || !voice.Is3D || voice.FollowTarget == null)
                return;

            var position = voice.FollowTarget.Position;
            voice.Position = position;
            if (voice.Player3D != null)
                voice.Player3D.GlobalPosition = ToGodotVector3(position);
        }

        private static YokiVector3 GetCurrentPosition(VoiceState voice)
        {
            if (voice.FollowTarget != null)
                return voice.FollowTarget.Position;

            return voice.Position;
        }

        private static float GetCurrentLinearVolume(VoiceState voice)
        {
            if (voice.Player2D != null)
                return DbToLinear(voice.Player2D.VolumeDb);
            if (voice.Player3D != null)
                return DbToLinear(voice.Player3D.VolumeDb);

            return 0f;
        }

        private static bool IsVoicePlaying(VoiceState voice)
        {
            if (voice.Player2D != null)
                return voice.Player2D.Playing;
            if (voice.Player3D != null)
                return voice.Player3D.Playing;

            return false;
        }

        private static void StopVoicePlayer(VoiceState voice)
        {
            if (voice.Player2D != null)
                voice.Player2D.Stop();
            if (voice.Player3D != null)
                voice.Player3D.Stop();
        }

        private static float GetPlaybackPosition(VoiceState voice)
        {
            if (voice.Player2D != null)
                return Math.Max(0f, (float)voice.Player2D.GetPlaybackPosition());
            if (voice.Player3D != null)
                return Math.Max(0f, (float)voice.Player3D.GetPlaybackPosition());

            return Math.Max(0f, GetNow() - voice.StartedAt);
        }

        private static float GetStreamLength(AudioStream stream)
        {
            return stream != null ? Math.Max(0f, (float)stream.GetLength()) : 0f;
        }

        private static string GetStreamName(AudioStream stream)
        {
            if (stream == null)
                return string.Empty;
            if (!string.IsNullOrEmpty(stream.ResourceName))
                return stream.ResourceName;
            if (!string.IsNullOrEmpty(stream.ResourcePath))
                return stream.ResourcePath;

            return stream.GetType().Name;
        }

        private void EnsureRoot()
        {
            if (IsInstanceValid(mRoot))
                return;

            var tree = Engine.GetMainLoop() as SceneTree;
            mRoot = new Node { Name = "YokiFrameAudioKit" };
            if (tree != null && tree.Root != null)
                tree.Root.AddChild(mRoot);
        }

        private static void DestroyNode(Node node)
        {
            if (!IsInstanceValid(node))
                return;

            node.QueueFree();
        }

        private static bool IsInstanceValid(GodotObject target)
        {
            return target != null && GodotObject.IsInstanceValid(target);
        }

        private static string Normalize(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        private static string RemoveExtension(string path)
        {
            var normalized = Normalize(path);
            var index = normalized.LastIndexOf('.');
            return index > 0 ? normalized.Substring(0, index) : normalized;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;

            return value;
        }

        private static float LinearToDb(float linear)
        {
            if (linear <= 0.0001f)
                return -80f;

            return 20f * (float)Math.Log10(linear);
        }

        private static float DbToLinear(float db)
        {
            if (db <= -80f)
                return 0f;

            return (float)Math.Pow(10f, db / 20f);
        }

        private static Vector3 ToGodotVector3(YokiVector3 position)
        {
            return new Vector3(position.X, position.Y, position.Z);
        }

        private static float GetNow()
        {
            return Time.GetTicksMsec() / 1000.0f;
        }
    }
}
#endif
