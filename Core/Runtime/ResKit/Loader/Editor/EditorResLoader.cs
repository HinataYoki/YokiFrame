#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// 编辑器资源加载池（使用 AssetDatabase）
    /// 支持直接从 Assets 路径加载资源，无需放在 Resources 文件夹
    /// </summary>
    public class EditorResLoaderPool : AbstractResLoaderPool
    {
        protected override IResLoader CreateLoader() => new EditorResLoader(this);
    }

    /// <summary>
    /// 编辑器资源加载器（使用 AssetDatabase.LoadAssetAtPath）
    /// </summary>
    public class EditorResLoader : IResLoader, IAllAssetsLoader, ISubAssetsLoader
    {
        private readonly IResLoaderPool mPool;
        private Object mAsset;

        public EditorResLoader(IResLoaderPool pool) => mPool = pool;

        public T Load<T>(string path) where T : Object
        {
            // 支持多种路径格式
            var assetPath = NormalizePath(path);
            mAsset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            
            if (mAsset == default)
            {
                // 尝试不带扩展名的路径（兼容 Resources 风格）
                var guids = AssetDatabase.FindAssets(System.IO.Path.GetFileNameWithoutExtension(path));
                foreach (var guid in guids)
                {
                    var foundPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (foundPath.Contains(path.Replace("\\", "/")))
                    {
                        mAsset = AssetDatabase.LoadAssetAtPath<T>(foundPath);
                        if (mAsset != default) break;
                    }
                }
            }
            
            // 追踪资源加载
            ResLoadTracker.OnLoad(this, path, typeof(T), mAsset);
            
            return mAsset as T;
        }

        public void LoadAsync<T>(string path, Action<T> onComplete) where T : Object
        {
            // 编辑器模式下异步加载直接同步完成
            var asset = Load<T>(path);
            onComplete?.Invoke(asset);
        }

        #region IAllAssetsLoader

        public T[] LoadAll<T>(string path) where T : Object
        {
            var assetPath = NormalizePath(path);
            var allObjects = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var result = new List<T>();

            foreach (var obj in allObjects)
            {
                if (obj is T typed)
                    result.Add(typed);
            }
            return result.ToArray();
        }

        public void LoadAllAsync<T>(string path, Action<T[]> onComplete) where T : Object
        {
            var result = LoadAll<T>(path);
            onComplete?.Invoke(result);
        }

        #endregion

        #region ISubAssetsLoader

        public SubAssetsResult<T> LoadSub<T>(string path) where T : Object
        {
            var assetPath = NormalizePath(path);
            var main = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            var representations = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            var subList = new List<T>();

            foreach (var obj in representations)
            {
                if (obj is T typed)
                    subList.Add(typed);
            }
            return new SubAssetsResult<T>(main, subList.ToArray());
        }

        public void LoadSubAsync<T>(string path, Action<SubAssetsResult<T>> onComplete) where T : Object
        {
            var result = LoadSub<T>(path);
            onComplete?.Invoke(result);
        }

        #endregion

        public void UnloadAndRecycle()
        {
            // 追踪资源卸载
            ResLoadTracker.OnUnload(this);
            mAsset = null;
            mPool.Recycle(this);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            
            // 统一使用正斜杠
            path = path.Replace("\\", "/");
            
            // 如果已经是 Assets/ 开头，直接返回
            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
            
            // 否则添加 Assets/ 前缀
            return "Assets/" + path;
        }
    }
}
#endif

