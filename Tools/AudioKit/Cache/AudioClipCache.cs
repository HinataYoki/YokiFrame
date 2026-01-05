using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 音频剪辑缓存
    /// </summary>
    internal sealed class AudioClipCache
    {
        private readonly Dictionary<int, AudioClip> mCache = new();

        /// <summary>
        /// 缓存数量
        /// </summary>
        public int Count => mCache.Count;

        /// <summary>
        /// 尝试获取缓存的音频剪辑
        /// </summary>
        public bool TryGet(int audioId, out AudioClip clip)
        {
            return mCache.TryGetValue(audioId, out clip);
        }

        /// <summary>
        /// 添加音频剪辑到缓存
        /// </summary>
        public void Add(int audioId, AudioClip clip)
        {
            if (clip == null) return;
            mCache[audioId] = clip;
        }

        /// <summary>
        /// 从缓存移除音频剪辑
        /// </summary>
        public bool Remove(int audioId)
        {
            if (mCache.TryGetValue(audioId, out var clip))
            {
                mCache.Remove(audioId);
                // 注意：不在这里卸载资源，由调用方决定是否卸载
                return true;
            }
            return false;
        }

        /// <summary>
        /// 检查是否包含指定音频
        /// </summary>
        public bool Contains(int audioId)
        {
            return mCache.ContainsKey(audioId);
        }

        /// <summary>
        /// 清空缓存
        /// </summary>
        public void Clear()
        {
            mCache.Clear();
        }

        /// <summary>
        /// 获取所有缓存的音频 ID
        /// </summary>
        public void GetAllIds(List<int> result)
        {
            result.Clear();
            foreach (var id in mCache.Keys)
            {
                result.Add(id);
            }
        }
    }
}
