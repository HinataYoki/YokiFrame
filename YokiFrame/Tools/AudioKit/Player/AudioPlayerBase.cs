using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 音频播放器抽象基类
    /// </summary>
    public abstract class AudioPlayerBase
    {
        protected IAudioLoader audioLoader;
        protected HashSet<IAudioTrack> activeTracks = new();

        public AudioType PlayerType { get; protected set; }

        // 初始化时注入加载器
        public virtual void Initialize(IAudioLoader loader)
        {
            audioLoader = loader;
        }

        // 播放音频核心方法
        public abstract IAudioTrack Play(string clipPath, bool loop = false, float volume = 1.0f);

        // 停止所有轨道
        public virtual void StopAll()
        {
            foreach (var track in activeTracks)
            {
                track.Stop();
                ReturnTrackToPool(track);
            }
            activeTracks.Clear();
        }

        // 暂停所有轨道
        public virtual void PauseAll()
        {
            foreach (var track in activeTracks)
            {
                track.Pause();
            }
        }

        // 恢复所有轨道
        public virtual void ResumeAll()
        {
            foreach (var track in activeTracks)
            {
                track.Resume();
            }
        }

        // 轨道工厂方法 (可重写)
        protected virtual IAudioTrack CreateTrack()
        {
            return new GameObject($"AudioTrack_{PlayerType}").AddComponent<AudioTrack>();
        }

        // 获取轨道 (对象池模式)
        protected IAudioTrack GetAvailableTrack()
        {
            return AudioKit.TrackPool.Get();
        }

        // 回收轨道
        protected void ReturnTrackToPool(IAudioTrack track)
        {
            track.Dispose();
            AudioKit.TrackPool.Release(track);
        }

        // 轨道完成回调
        protected virtual void HandleTrackCompleted(IAudioTrack track)
        {
            activeTracks.Remove(track);
            ReturnTrackToPool(track);
        }
    }
}