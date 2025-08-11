using System;
using UnityEngine;

namespace YokiFrame
{
    // 音频轨道抽象接口
    public interface IAudioTrack
    {
        Transform Transform { get; }
        AudioClip Clip { get; }
        bool IsPlaying { get; }
        float Volume { get; set; }

        event Action<IAudioTrack> OnStarted;
        event Action<IAudioTrack> OnCompleted;
        event Action<IAudioTrack> OnEnd;

        IAudioTrack Play(AudioClip clip, bool loop, float volume);
        void Stop();
        void Pause();
        void Resume();
        void End();
        void Dispose();
    }
}