using UnityEngine;

namespace YokiFrame
{
    // 资源加载接口 (可自定义实现)
    public interface IAudioLoader
    {
        AudioClip LoadAudioClip(string path);
    }
}