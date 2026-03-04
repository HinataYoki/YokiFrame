using System;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// 子资源加载结果（轻量 struct，零额外 GC）
    /// </summary>
    /// <typeparam name="T">子资源类型</typeparam>
    public readonly struct SubAssetsResult<T> where T : Object
    {
        /// <summary>
        /// 主资源对象
        /// </summary>
        public readonly T MainAsset;

        /// <summary>
        /// 所有子资源对象
        /// </summary>
        public readonly T[] AllSubAssets;

        public SubAssetsResult(T mainAsset, T[] allSubAssets)
        {
            MainAsset = mainAsset;
            AllSubAssets = allSubAssets ?? Array.Empty<T>();
        }

        /// <summary>
        /// 按名称查找子资源
        /// </summary>
        /// <param name="name">子资源名称</param>
        /// <returns>匹配的子资源，未找到返回 null</returns>
        public T GetSubAsset(string name)
        {
            if (AllSubAssets is null) return null;

            foreach (var asset in AllSubAssets)
            {
                if (asset != default && asset.name == name)
                    return asset;
            }
            return null;
        }

        /// <summary>
        /// 是否有效（主资源非空）
        /// </summary>
        public bool IsValid => MainAsset != default;
    }

    /// <summary>
    /// 子资源加载器接口 — 加载包含子对象的资源（如 SpriteAtlas → Sprite）
    /// </summary>
    /// <remarks>
    /// 典型场景：加载 SpriteAtlas 并获取其中的精灵对象。
    /// 由现有 IResLoader 实现类可选实现（组合扩展），ResKit 门面通过 is 模式匹配检测支持性。
    /// </remarks>
    public interface ISubAssetsLoader
    {
        /// <summary>
        /// 同步加载子资源
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <typeparam name="T">子资源类型</typeparam>
        /// <returns>包含主资源和所有子资源的结果</returns>
        SubAssetsResult<T> LoadSub<T>(string path) where T : Object;

        /// <summary>
        /// 异步加载子资源
        /// </summary>
        void LoadSubAsync<T>(string path, Action<SubAssetsResult<T>> onComplete) where T : Object;
    }
}
