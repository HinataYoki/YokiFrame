using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 增量代码生成器 - 仅在内容变化时写入文件
    /// </summary>
    public static class IncrementalCodeGenerator
    {
        #region 常量

        /// <summary>
        /// 哈希缓存文件名
        /// </summary>
        private const string HASH_CACHE_FILE = "Library/UIKitCodeGenHashes.json";

        #endregion

        #region 缓存

        /// <summary>
        /// 文件路径到哈希值的缓存
        /// </summary>
        private static Dictionary<string, string> sHashCache;

        /// <summary>
        /// 缓存是否已加载
        /// </summary>
        private static bool sCacheLoaded;

        /// <summary>
        /// 缓存是否有修改
        /// </summary>
        private static bool sCacheDirty;

        #endregion

        #region 公共方法

        /// <summary>
        /// 计算字符串内容的 SHA256 哈希值
        /// </summary>
        /// <param name="content">内容字符串</param>
        /// <returns>哈希值（十六进制字符串）</returns>
        public static string ComputeHash(string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            using var sha256 = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            byte[] hash = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// 增量写入文件 - 仅在内容变化时写入
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="content">文件内容</param>
        /// <param name="forceWrite">是否强制写入（忽略哈希比较）</param>
        /// <returns>是否实际写入了文件</returns>
        public static bool WriteFileIncremental(string filePath, string content, bool forceWrite = false)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            // 计算新内容的哈希
            string newHash = ComputeHash(content);

            // 检查是否需要写入
            if (!forceWrite && !NeedsUpdate(filePath, newHash))
            {
                return false;
            }

            // 确保目录存在
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 写入文件
            File.WriteAllText(filePath, content, Encoding.UTF8);

            // 更新缓存
            UpdateHashCache(filePath, newHash);

            return true;
        }

        /// <summary>
        /// 检查文件是否需要更新
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="newHash">新内容的哈希值</param>
        /// <returns>是否需要更新</returns>
        public static bool NeedsUpdate(string filePath, string newHash)
        {
            // 文件不存在，需要创建
            if (!File.Exists(filePath))
                return true;

            // 获取缓存的哈希值
            string cachedHash = GetCachedHash(filePath);

            // 缓存中没有，需要更新
            if (string.IsNullOrEmpty(cachedHash))
                return true;

            // 比较哈希值
            return cachedHash != newHash;
        }

        /// <summary>
        /// 检查文件内容是否与给定内容相同
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="content">要比较的内容</param>
        /// <returns>内容是否相同</returns>
        public static bool ContentEquals(string filePath, string content)
        {
            if (!File.Exists(filePath))
                return false;

            string existingContent = File.ReadAllText(filePath, Encoding.UTF8);
            return existingContent == content;
        }

        /// <summary>
        /// 获取文件的缓存哈希值
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>缓存的哈希值，如果不存在则返回 null</returns>
        public static string GetCachedHash(string filePath)
        {
            EnsureCacheLoaded();

            string normalizedPath = NormalizePath(filePath);
            return sHashCache.TryGetValue(normalizedPath, out var hash) ? hash : null;
        }

        /// <summary>
        /// 更新文件的哈希缓存
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="hash">哈希值</param>
        public static void UpdateHashCache(string filePath, string hash)
        {
            EnsureCacheLoaded();

            string normalizedPath = NormalizePath(filePath);
            sHashCache[normalizedPath] = hash;
            sCacheDirty = true;
        }

        /// <summary>
        /// 从缓存中移除文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public static void RemoveFromCache(string filePath)
        {
            EnsureCacheLoaded();

            string normalizedPath = NormalizePath(filePath);
            if (sHashCache.Remove(normalizedPath))
            {
                sCacheDirty = true;
            }
        }

        /// <summary>
        /// 保存哈希缓存到文件
        /// </summary>
        public static void SaveCache()
        {
            if (!sCacheDirty || sHashCache == null)
                return;

            try
            {
                var wrapper = new HashCacheWrapper { Entries = new List<HashCacheEntry>() };
                foreach (var kvp in sHashCache)
                {
                    wrapper.Entries.Add(new HashCacheEntry { Path = kvp.Key, Hash = kvp.Value });
                }

                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(HASH_CACHE_FILE, json, Encoding.UTF8);
                sCacheDirty = false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[IncrementalCodeGenerator] 保存哈希缓存失败: {e.Message}");
            }
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public static void ClearCache()
        {
            sHashCache?.Clear();
            sCacheDirty = true;

            if (File.Exists(HASH_CACHE_FILE))
            {
                File.Delete(HASH_CACHE_FILE);
            }
        }

        /// <summary>
        /// 重新计算文件的哈希并更新缓存
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public static void RefreshFileHash(string filePath)
        {
            if (!File.Exists(filePath))
            {
                RemoveFromCache(filePath);
                return;
            }

            string content = File.ReadAllText(filePath, Encoding.UTF8);
            string hash = ComputeHash(content);
            UpdateHashCache(filePath, hash);
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 确保缓存已加载
        /// </summary>
        private static void EnsureCacheLoaded()
        {
            if (sCacheLoaded && sHashCache != null)
                return;

            sHashCache = new Dictionary<string, string>(64);
            sCacheLoaded = true;

            if (!File.Exists(HASH_CACHE_FILE))
                return;

            try
            {
                string json = File.ReadAllText(HASH_CACHE_FILE, Encoding.UTF8);
                var wrapper = JsonUtility.FromJson<HashCacheWrapper>(json);

                if (wrapper?.Entries != null)
                {
                    foreach (var entry in wrapper.Entries)
                    {
                        if (!string.IsNullOrEmpty(entry.Path) && !string.IsNullOrEmpty(entry.Hash))
                        {
                            sHashCache[entry.Path] = entry.Hash;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[IncrementalCodeGenerator] 加载哈希缓存失败: {e.Message}");
            }
        }

        /// <summary>
        /// 规范化文件路径
        /// </summary>
        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return path.Replace('\\', '/');
        }

        #endregion

        #region 序列化类型

        /// <summary>
        /// 哈希缓存包装器（用于 JSON 序列化）
        /// </summary>
        [Serializable]
        private class HashCacheWrapper
        {
            public List<HashCacheEntry> Entries;
        }

        /// <summary>
        /// 哈希缓存条目
        /// </summary>
        [Serializable]
        private class HashCacheEntry
        {
            public string Path;
            public string Hash;
        }

        #endregion

        #region 编辑器回调

        /// <summary>
        /// 编辑器退出时保存缓存
        /// </summary>
        [InitializeOnLoadMethod]
        private static void RegisterEditorCallbacks()
        {
            EditorApplication.quitting += SaveCache;
            AssemblyReloadEvents.beforeAssemblyReload += SaveCache;
        }

        #endregion
    }
}
