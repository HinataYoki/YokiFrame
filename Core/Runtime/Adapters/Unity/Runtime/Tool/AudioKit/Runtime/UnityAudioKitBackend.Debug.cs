#if !GODOT
using System;
using UnityEngine;

namespace YokiFrame.Unity
{
    public sealed partial class UnityAudioKitBackend
    {
        private AudioVoiceDebugInfo BuildDebugInfo(VoiceState voice)
        {
            var clip = voice.Clip;
            var position = GetCurrentPosition(voice);
            return new AudioVoiceDebugInfo
            {
                VoiceId = voice.VoiceId,
                Path = voice.Path,
                ClipName = clip != null ? clip.name : string.Empty,
                Bus = voice.Bus,
                BackendName = BackendName,
                Loop = voice.Loop,
                IsPlaying = voice.Source != null && voice.Source.isPlaying,
                Volume = voice.Source != null ? voice.Source.volume : 0f,
                Pitch = voice.Pitch,
                FadeOutDuration = voice.FadeOutDuration,
                StartedAt = voice.StartedAt,
                Duration = clip != null ? clip.length : 0f,
                Elapsed = Mathf.Max(0f, Time.time - voice.StartedAt),
                Is3D = voice.Is3D,
                Position = position,
                HasFollowTarget = voice.FollowTarget != null,
                FollowTargetName = voice.FollowTarget != null ? voice.FollowTarget.Name : string.Empty,
                MinDistance = voice.MinDistance,
                MaxDistance = voice.MaxDistance,
                RolloffMode = voice.RolloffMode
            };
        }

        private static void ApplySpatialOptions(AudioSource source, VoiceState voice)
        {
            if (source == null)
                return;

            if (!voice.Is3D)
            {
                source.spatialBlend = 0f;
                source.transform.localPosition = Vector3.zero;
                return;
            }

            source.spatialBlend = 1f;
            source.minDistance = voice.MinDistance;
            source.maxDistance = voice.MaxDistance;
            source.rolloffMode = ToUnityRolloffMode(voice.RolloffMode);
            var position = GetCurrentPosition(voice);
            voice.Position = position;
            source.transform.position = position.ToUnityVector3();
        }

        private static void UpdateFollowTarget(VoiceState voice)
        {
            if (voice == null || !voice.Is3D || voice.FollowTarget == null)
                return;

            var position = voice.FollowTarget.Position;
            voice.Position = position;
            if (voice.Source != null)
                voice.Source.transform.position = position.ToUnityVector3();
        }

        private static YokiVector3 GetCurrentPosition(VoiceState voice)
        {
            if (voice.FollowTarget != null)
                return voice.FollowTarget.Position;

            return voice.Position;
        }

        private static UnityEngine.AudioRolloffMode ToUnityRolloffMode(YokiFrame.AudioRolloffMode mode)
        {
            switch (mode)
            {
                case YokiFrame.AudioRolloffMode.Linear:
                    return UnityEngine.AudioRolloffMode.Linear;
                case YokiFrame.AudioRolloffMode.Custom:
                    return UnityEngine.AudioRolloffMode.Custom;
                default:
                    return UnityEngine.AudioRolloffMode.Logarithmic;
            }
        }
    }
}
#endif
