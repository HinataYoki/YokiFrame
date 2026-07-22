#if UNITY_2022_3_OR_NEWER
using System;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 定义 UIKit 面板 Prefab 加载边界；实现不得缓存面板实例或复用 lease。
    /// </summary>
    public interface IPanelLoader
    {
        /// <summary>
        /// 获取或设置是否直接使用 Panel 类型名作为底层资源 location。
        /// 启用后，支持路径式加载的实现必须将 <see cref="Type.Name"/> 作为 location，
        /// 以适配 YooAsset 等使用可寻址 location 的 ResKit Provider。
        /// </summary>
        bool UseAddressableLocation { get; set; }

        /// <summary>
        /// 同步加载指定面板类型的 Prefab。
        /// </summary>
        /// <param name="panelType">需要加载的具体面板类型。</param>
        /// <returns>加载成功时返回调用方独占的 lease；资源不存在时返回空值。</returns>
        IPanelPrefabLease Load(Type panelType);

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 异步加载指定面板类型的 Prefab。
        /// </summary>
        /// <param name="panelType">需要加载的具体面板类型。</param>
        /// <param name="cancellationToken">仅取消当前等待者的取消令牌。</param>
        /// <returns>加载成功时返回调用方独占的 lease；资源不存在时返回空值。</returns>
        UniTask<IPanelPrefabLease> LoadAsync(Type panelType, CancellationToken cancellationToken = default);
#else
        /// <summary>
        /// 异步加载指定面板类型的 Prefab。
        /// </summary>
        /// <param name="panelType">需要加载的具体面板类型。</param>
        /// <param name="cancellationToken">仅取消当前等待者的取消令牌。</param>
        /// <returns>加载成功时返回调用方独占的 lease；资源不存在时返回空值。</returns>
        Task<IPanelPrefabLease> LoadAsync(Type panelType, CancellationToken cancellationToken = default);
#endif
    }
}
#endif
