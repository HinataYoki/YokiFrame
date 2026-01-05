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
        /// ResKit 资源句柄（用于引用计数管理）
        /// </summary>
        public ResHandler ResHandler;

        public AudioClipEntry(AudioClip clip, ResHandler resHandler = null)
        {
            Clip = clip;
            ResHandler = resHandler;
        }
    }
}
