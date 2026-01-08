using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 音频剪辑缓存条目
    /// </summary>
    internal struct AudioClipEntry
    {
        /// <summary>
        /// 音频剪辑
        /// </summary>
        public AudioClip Clip;

        /// <summary>
        /// 音频加载器（用于资源生命周期管理）
        /// </summary>
        public IAudioLoader AudioLoader;

        /// <summary>
        /// ResKit 资源句柄（向后兼容，已弃用）
        /// </summary>
        [System.Obsolete("使用 AudioLoader 替代")]
        public ResHandler ResHandler;

        public AudioClipEntry(AudioClip clip, IAudioLoader audioLoader)
        {
            Clip = clip;
            AudioLoader = audioLoader;
#pragma warning disable CS0618
            ResHandler = null;
#pragma warning restore CS0618
        }

        /// <summary>
        /// 向后兼容构造函数
        /// </summary>
        [System.Obsolete("使用 AudioClipEntry(AudioClip, IAudioLoader) 替代")]
        public AudioClipEntry(AudioClip clip, ResHandler resHandler)
        {
            Clip = clip;
            AudioLoader = null;
#pragma warning disable CS0618
            ResHandler = resHandler;
#pragma warning restore CS0618
        }
    }
}
