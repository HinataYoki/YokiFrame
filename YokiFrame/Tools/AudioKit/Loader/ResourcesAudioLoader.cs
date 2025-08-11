using UnityEngine;

namespace YokiFrame
{
    // 默认Resources加载器
    public class ResourcesAudioLoader : IAudioLoader
    {
        public AudioClip LoadAudioClip(string path)
        {
            return Resources.Load<AudioClip>(path);
        }
    }
}