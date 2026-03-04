using System;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// 批量资源加载器接口 — 一次性加载多个指定类型的资源
    /// </summary>
    /// <remarks>
    /// ⚠ 各后端 path 参数含义不同：
    ///   • YooAsset：path 为资源包内任意资源地址，用于定位所属资源包（Bundle），加载该 Bundle 内所有匹配类型的资源。
    ///   • Resources：path 为 Resources 文件夹下的路径，加载该文件夹（或文件）下所有匹配类型的资产（含子对象）。
    ///   • Editor（AssetDatabase）：path 为 Assets/ 路径，加载该路径下的所有资源对象。
    /// 
    /// 由现有 IResLoader 实现类可选实现（组合扩展），ResKit 门面通过 is 模式匹配检测支持性。
    /// </remarks>
    public interface IAllAssetsLoader
    {
        /// <summary>
        /// 同步加载所有指定类型的资源
        /// </summary>
        /// <param name="path">资源路径（含义因后端而异，详见接口备注）</param>
        /// <typeparam name="T">资源类型</typeparam>
        /// <returns>所有匹配类型的资源数组，失败时返回空数组</returns>
        T[] LoadAll<T>(string path) where T : Object;

        /// <summary>
        /// 异步加载所有指定类型的资源
        /// </summary>
        void LoadAllAsync<T>(string path, Action<T[]> onComplete) where T : Object;
    }
}
