#if GODOT
using Godot;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame.Godot
{
    public sealed partial class GodotAudioKitBackend
    {
        /// <summary>同步解析 AudioStream 并在 Godot 主线程创建 voice。</summary>
        public int Play(string path, AudioPlayOptions options)
        {
            EnsureUsable();
            AudioStream stream = ResolveStream(path);
            if (stream == null)
            {
                LogKit.Warning("[AudioKit] Godot audio resource was not found: " + path);
                return 0;
            }

            return PlayResolved(path, stream, options);
        }

        /// <summary>异步加载 AudioStream，并切回 Godot 主线程启动播放。</summary>
        public async Task<int> PlayAsync(string path, AudioPlayOptions options, CancellationToken token)
        {
            EnsureUsable();
            AudioStream stream = await ResolveStreamAsync(path, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (stream == null) return 0;
            return await InvokeOnGodotThreadAsync(() => PlayResolved(path, stream, options), token).ConfigureAwait(false);
        }

        /// <summary>创建 2D 或 3D Player、应用参数并开始播放。</summary>
        private int PlayResolved(string path, AudioStream source, AudioPlayOptions options)
        {
            EnsureUsable();
            EnsureGodotThread();
            if (mVoices.Count >= MAX_ACTIVE_VOICES)
            {
                LogKit.Warning("[AudioKit] Godot active voice limit reached: " + MAX_ACTIVE_VOICES);
                return 0;
            }

            AudioStream stream = CreatePlaybackStream(source, options.Loop);
            VoiceState voice = CreateVoice(path, stream, options);
            if (voice.Is3D) ConfigurePlayer3D(voice);
            else ConfigurePlayer2D(voice);
            mVoices.Add(voice);
            return voice.VoiceId;
        }

        /// <summary>从规范化播放参数创建内部 voice 状态。</summary>
        private VoiceState CreateVoice(string path, AudioStream stream, AudioPlayOptions options)
        {
            return new VoiceState
            {
                VoiceId = NextVoiceId(),
                Path = path,
                Bus = options.Bus,
                Stream = stream,
                BaseVolume = options.Volume,
                Pitch = options.Pitch,
                FadeInDuration = options.FadeInDuration,
                FadeOutDuration = options.FadeOutDuration,
                FadingIn = options.FadeInDuration > 0f,
                Loop = options.Loop,
                StartedAt = GetNow(),
                Is3D = options.Is3D,
                Position = options.Position,
                FollowTarget = options.FollowTarget,
                MinDistance = options.MinDistance,
                MaxDistance = options.MaxDistance,
                RolloffMode = options.RolloffMode
            };
        }

        /// <summary>创建独立播放流并对常见格式真实应用循环设置。</summary>
        private static AudioStream CreatePlaybackStream(AudioStream source, bool loop)
        {
            AudioStream stream = source.Duplicate() as AudioStream ?? source;
            if (stream is AudioStreamOggVorbis ogg) ogg.Loop = loop;
            else if (stream is AudioStreamMP3 mp3) mp3.Loop = loop;
            else if (stream is AudioStreamWav wav)
            {
                wav.LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled;
            }

            return stream;
        }

        /// <summary>配置并启动 2D Player。</summary>
        private void ConfigurePlayer2D(VoiceState voice)
        {
            voice.Player2DLease = RentPlayer2D();
            AudioStreamPlayer player = voice.Player2D;
            player.Stream = voice.Stream;
            player.Bus = ResolveGodotBus(voice.Bus);
            player.PitchScale = voice.Pitch;
            ApplyVoiceVolume(voice);
            player.Play();
        }

        /// <summary>配置并启动 3D Player。</summary>
        private void ConfigurePlayer3D(VoiceState voice)
        {
            voice.Player3DLease = RentPlayer3D();
            AudioStreamPlayer3D player = voice.Player3D;
            player.Stream = voice.Stream;
            player.Bus = ResolveGodotBus(voice.Bus);
            player.PitchScale = voice.Pitch;
            player.UnitSize = voice.MinDistance;
            player.MaxDistance = voice.MaxDistance;
            ApplyGodotRolloff(player, voice.RolloffMode);
            UpdateFollowTarget(voice);
            ApplyVoiceVolume(voice);
            player.Play();
        }

        /// <summary>生成始终为正的后端局部 voice id。</summary>
        private int NextVoiceId()
        {
            mNextVoiceId++;
            if (mNextVoiceId <= 0) mNextVoiceId = 1;
            return mNextVoiceId;
        }

        /// <summary>确保后端尚未释放。</summary>
        private void EnsureUsable()
        {
            if (mDisposed) throw new ObjectDisposedException(nameof(GodotAudioKitBackend));
        }
    }
}
#endif
