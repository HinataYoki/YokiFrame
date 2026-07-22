#if UNITY_2022_3_OR_NEWER
using System;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 使用 ResKit 加载面板 Prefab，并为每个调用方返回独立资源 lease。
    /// </summary>
    public sealed class ResKitPanelLoader : IPanelLoader
    {
        private readonly string mPrefabPathPrefix;
        private bool mUseAddressableLocation;

        /// <summary>使用稳定默认路径创建 Resources 模式的 Panel loader。</summary>
        public ResKitPanelLoader() : this(UIRoot.DEFAULT_PREFAB_PATH_PREFIX, false)
        {
        }

        /// <summary>使用 Root Prefab 序列化参数创建 Panel loader。</summary>
        /// <param name="prefabPathPrefix">非 addressable 模式的 ResKit location 前缀。</param>
        /// <param name="useAddressableLocation">是否直接使用 Panel 类型名作为 location。</param>
        public ResKitPanelLoader(string prefabPathPrefix, bool useAddressableLocation)
        {
            mPrefabPathPrefix = NormalizePathPrefix(prefabPathPrefix);
            mUseAddressableLocation = useAddressableLocation;
        }

        /// <summary>
        /// 获取或设置后续加载是否直接使用 Panel 类型名作为 ResKit location。
        /// 修改不会影响已开始的异步请求、已物化面板或它们持有的资源 lease。
        /// </summary>
        public bool UseAddressableLocation
        {
            get => mUseAddressableLocation;
            set => mUseAddressableLocation = value;
        }

        /// <summary>
        /// 同步通过 ResKit 加载指定面板类型的 Prefab。
        /// </summary>
        /// <param name="panelType">需要加载的具体面板类型。</param>
        /// <returns>加载成功时返回调用方独占的 lease；资源不存在时返回空值。</returns>
        public IPanelPrefabLease Load(Type panelType)
        {
            string location = BuildLocation(panelType);
            ResHandle<GameObject> resourceHandle = ResKit.LoadAsset<GameObject>(location);
            return CreateLease(location, resourceHandle);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 异步通过 ResKit 加载指定面板类型的 Prefab。
        /// </summary>
        /// <param name="panelType">需要加载的具体面板类型。</param>
        /// <param name="cancellationToken">仅取消当前等待者的取消令牌。</param>
        /// <returns>加载成功时返回调用方独占的 lease；资源不存在时返回空值。</returns>
        public async UniTask<IPanelPrefabLease> LoadAsync(
#else
        /// <summary>
        /// 异步通过 ResKit 加载指定面板类型的 Prefab。
        /// </summary>
        /// <param name="panelType">需要加载的具体面板类型。</param>
        /// <param name="cancellationToken">仅取消当前等待者的取消令牌。</param>
        /// <returns>加载成功时返回调用方独占的 lease；资源不存在时返回空值。</returns>
        public async Task<IPanelPrefabLease> LoadAsync(
#endif
            Type panelType,
            CancellationToken cancellationToken = default)
        {
            string location = BuildLocation(panelType);
            ResHandle<GameObject> resourceHandle =
                await ResKit.LoadAssetAsync<GameObject>(location, cancellationToken);
            return CreateLease(location, resourceHandle);
        }

        /// <summary>
        /// 将有效 ResKit handle 转换为 UIKit lease；无效资源会立即释放 handle。
        /// </summary>
        /// <param name="location">本次加载使用的 ResKit location。</param>
        /// <param name="resourceHandle">ResKit 返回的独立资源 handle。</param>
        /// <returns>资源有效时返回独占 lease，否则返回空值。</returns>
        private static IPanelPrefabLease CreateLease(
            string location,
            ResHandle<GameObject> resourceHandle)
        {
            if (resourceHandle == null) return null;
            GameObject prefab = resourceHandle.Asset;
            if (prefab != default)
            {
                return new ResKitPanelPrefabLease(location, resourceHandle, prefab);
            }

            resourceHandle.Dispose();
            return null;
        }

        /// <summary>
        /// 按当前 loader 配置为面板类型构建 ResKit location。
        /// </summary>
        /// <param name="panelType">需要加载的具体面板类型。</param>
        /// <returns>类型名 location，或路径前缀与类型名组合后的 Resources 路径。</returns>
        private string BuildLocation(Type panelType)
        {
            if (panelType == null) throw new ArgumentNullException(nameof(panelType));
            if (mUseAddressableLocation) return panelType.Name;
            return string.Concat(mPrefabPathPrefix, "/", panelType.Name);
        }

        /// <summary>
        /// 将 Prefab 字段中的路径前缀规范为 ResKit 使用的正斜杠相对路径。
        /// </summary>
        /// <param name="pathPrefix">Root Prefab 中的原始路径前缀。</param>
        /// <returns>去除首尾分隔符的有效路径前缀。</returns>
        private static string NormalizePathPrefix(string pathPrefix)
        {
            if (string.IsNullOrWhiteSpace(pathPrefix)) return UIRoot.DEFAULT_PREFAB_PATH_PREFIX;
            string normalized = pathPrefix.Trim().Replace('\\', '/').Trim('/');
            return string.IsNullOrEmpty(normalized) ? UIRoot.DEFAULT_PREFAB_PATH_PREFIX : normalized;
        }
    }
}
#endif
