using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 单轨道播放器 (背景音乐适用)
    /// </summary>
    public class SingleTrackPlayer : AudioPlayerBase
    {
        private IAudioTrack currentTrack;

        public override IAudioTrack Play(string clipPath, bool loop = false, float volume = 1.0f)
        {
            // 如果有正在播放的轨道，加入队列
            if (currentTrack != null && currentTrack.IsPlaying)
            {
                currentTrack.End();
            }

            // 获取新轨道并播放
            currentTrack = GetAvailableTrack();
            return StartTrack(currentTrack, clipPath, loop, volume);
        }

        private IAudioTrack StartTrack(IAudioTrack track, string clipPath, bool loop, float volume)
        {
            AudioClip clip = audioLoader.LoadAudioClip(clipPath);

            if (clip != null)
            {
                track.OnEnd += Track_OnEnd;
                track.Play(clip, loop, volume);
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