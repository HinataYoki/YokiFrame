using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 多轨道播放器 (音效适用)
    /// </summary>
    public class MultiTrackPlayer : AudioPlayerBase
    {
        public override IAudioTrack Play(string clipPath, bool loop = false, float volume = 1.0f)
        {
            var track = GetAvailableTrack();
            AudioClip clip = audioLoader.LoadAudioClip(clipPath);
            if (clip != null)
            {
                track.Play(clip, loop, volume);
                track.OnEnd += Track_OnEnd;
                activeTracks.Add(track);
            }
            else
            {
                Debug.LogError($"Audio clip not found: {clipPath}");
                ReturnTrackToPool(track);
                return default;
            }
            return track;
        }

        private void Track_OnEnd(IAudioTrack track)
        {
            activeTracks.Remove(track);
            ReturnTrackToPool(track);
        }
    }
}