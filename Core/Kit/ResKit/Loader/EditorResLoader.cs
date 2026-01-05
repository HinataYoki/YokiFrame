#if UNITY_EDITOR
using System;
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
    public class EditorResLoader : IResLoader
    {
        private readonly IResLoaderPool mPool;
        private Object mAsset;

        public EditorResLoader(IResLoaderPool pool) => mPool = pool;

        public T Load<T>(string path) where T : Object
        {
            // 支持多种路径格式
            var assetPath = NormalizePath(path);
            mAsset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            
            if (mAsset == null)
            {
                // 尝试不带扩展名的路径（兼容 Resources 风格）
                var guids = AssetDatabase.FindAssets(System.IO.Path.GetFileNameWithoutExtension(path));
                foreach (var guid in guids)
                {
                    var foundPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (foundPath.Contains(path.Replace("\\", "/")))
                    {
                        mAsset = AssetDatabase.LoadAssetAtPath<T>(foundPath);
                        if (mAsset != null) break;
                    }
                }
            }
            
            return mAsset as T;
        }

        public void LoadAsync<T>(string path, Action<T> onComplete) where T : Object
        {
            // 编辑器模式下异步加载直接同步完成
            var asset = Load<T>(path);
            onComplete?.Invoke(asset);
        }

        public void UnloadAndRecycle()
        {
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
