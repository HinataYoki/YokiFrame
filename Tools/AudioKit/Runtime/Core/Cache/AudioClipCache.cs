using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 音频剪辑缓存（使用 string path 作为 key）
    /// </summary>
    internal sealed class AudioClipCache
    {
        private readonly Dictionary<string, AudioClipEntry> mCache = new();

        /// <summary>
        /// 缓存数量
        /// </summary>
        public int Count => mCache.Count;

        /// <summary>
        /// 尝试获取缓存的音频剪辑
        /// </summary>
        public bool TryGet(string path, out AudioClip clip)
        {
            if (mCache.TryGetValue(path, out var entry))
            {
                clip = entry.Clip;
                return true;
            }
            clip = null;
            return false;
        }

        /// <summary>
        /// 尝试获取缓存条目（包含 AudioLoader）
        /// </summary>
        public bool TryGetEntry(string path, out AudioClipEntry entry)
        {
            return mCache.TryGetValue(path, out entry);
        }

        /// <summary>
        /// 添加音频剪辑到缓存
        /// </summary>
        public void Add(string path, AudioClip clip, IAudioLoader audioLoader = null)
        {
            if (string.IsNullOrEmpty(path) || clip == null) return;
            mCache[path] = new AudioClipEntry(clip, audioLoader);
        }

        /// <summary>
        /// 从缓存移除音频剪辑
        /// </summary>
        public bool Remove(string path)
        {
            return mCache.Remove(path);
        }

        /// <summary>
        /// 检查是否包含指定路径的音频
        /// </summary>
        public bool Contains(string path)
        {
            return !string.IsNullOrEmpty(path) && mCache.ContainsKey(path);
        }

        /// <summary>
        /// 清空缓存（释放所有 AudioLoader）
        /// </summary>
        public void Clear()
        {
            foreach (var entry in mCache.Values)
            {
                entry.AudioLoader?.UnloadAndRecycle();
            }
            mCache.Clear();
        }

        /// <summary>
        /// 获取所有缓存的路径
        /// </summary>
        public void GetAllPaths(List<string> result)
        {
            result.Clear();
            foreach (var path in mCache.Keys)
            {
                result.Add(path);
            }
        }

        /// <summary>
        /// 获取所有缓存条目（用于遍历释放）
        /// </summary>
        public IEnumerable<KeyValuePair<string, AudioClipEntry>> GetAllEntries()
        {
            return mCache;
        }
    }
}
