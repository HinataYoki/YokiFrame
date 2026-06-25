#if !GODOT
using System;
using UnityEngine;

namespace YokiFrame.Unity
{
    public sealed partial class UnityAudioKitBackend
    {
        public AudioVoiceDebugInfo Play(string path, AudioPlayOptions options)
        {
            var clip = ResolveClip(path);
            if (clip == null)
            {
                LogKit.Warning("[AudioKit] 找不到音频资源: " + path);
                return null;
            }

            return PlayResolvedClip(path, clip, options);
        }

        public async void PlayAsync(string path, AudioPlayOptions options, Action<AudioVoiceDebugInfo> onComplete)
        {
            try
            {
                var clip = await ResolveClipAsync(path);
                if (clip == null)
                {
                    LogKit.Warning("[AudioKit] 找不到音频资源: " + path);
                    if (onComplete != null)
                        onComplete(null);
                    return;
                }

                var info = PlayResolvedClip(path, clip, options);
                if (onComplete != null)
                    onComplete(info);
            }
            catch (Exception exception)
            {
                LogKit.Warning("[AudioKit] 异步播放失败: " + path + " " + exception.Message);
                if (onComplete != null)
                    onComplete(null);
            }
        }

        private AudioVoiceDebugInfo PlayResolvedClip(string path, AudioClip clip, AudioPlayOptions options)
        {
            var source = RentSource();
            var voice = new VoiceState
            {
                VoiceId = ++mNextVoiceId,
                Path = Normalize(path),
                Bus = string.IsNullOrEmpty(options.Bus) ? AudioBus.Sfx : options.Bus,
                Clip = clip,
                Source = source,
                BaseVolume = Mathf.Clamp01(options.Volume),
                Pitch = options.Pitch <= 0f ? 1f : options.Pitch,
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
            source.clip = clip;
            source.loop = voice.Loop;
            source.pitch = voice.Pitch;
            ApplyVoiceVolume(voice);
            ApplySpatialOptions(source, voice);
            source.Play();

            mVoices.Add(voice);
            return BuildDebugInfo(voice);
        }
    }
}
#endif
