#if UNITY_2022_3_OR_NEWER
using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 将 ResKit GameObject handle 适配为 UIKit 独占 Prefab lease。
    /// </summary>
    internal sealed class ResKitPanelPrefabLease : IPanelPrefabLease
    {
        private ResHandle<GameObject> mResourceHandle;
        private GameObject mPrefab;

        /// <summary>
        /// 使用已验证的 ResKit handle 和 Prefab 创建独占 lease。
        /// </summary>
        /// <param name="location">本次加载使用的 ResKit location。</param>
        /// <param name="resourceHandle">当前 lease 独占释放的 ResKit handle。</param>
        /// <param name="prefab">handle 当前持有的有效 Prefab。</param>
        internal ResKitPanelPrefabLease(
            string location,
            ResHandle<GameObject> resourceHandle,
            GameObject prefab)
        {
            if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException("Location is required.", nameof(location));
            if (resourceHandle == null) throw new ArgumentNullException(nameof(resourceHandle));
            if (prefab == default) throw new ArgumentNullException(nameof(prefab));
            Location = location;
            mResourceHandle = resourceHandle;
            mPrefab = prefab;
        }

        /// <inheritdoc />
        public string Location { get; }

        /// <inheritdoc />
        public GameObject Prefab => mPrefab;

        /// <summary>
        /// 幂等释放当前 lease 的 ResKit handle，并立即使 Prefab 属性失效。
        /// </summary>
        public void Dispose()
        {
            ResHandle<GameObject> resourceHandle = mResourceHandle;
            mResourceHandle = null;
            mPrefab = null;
            if (resourceHandle != null)
            {
                resourceHandle.Dispose();
            }
        }
    }
}
#endif
