#if UNITY_2022_3_OR_NEWER
using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// Unity 专属 UI 管理门面；所有状态变更由唯一 UIRoot owner 执行。
    /// </summary>
    public static partial class UIKit
    {
        private const string DEFAULT_ROOT_PREFAB_PATH = "UIKit";
        private static GameObject sRootPrefab;

        /// <summary>
        /// 默认命名栈标识。
        /// </summary>
        public const string DEFAULT_STACK = "main";

        /// <summary>
        /// 获取当前已存在的 UIRoot；未创建时返回 null，读取不会创建 Root。
        /// </summary>
        public static UIRoot Root
        {
            get
            {
                return UIRoot.TryGetExisting(out UIRoot root) ? root : null;
            }
        }

        /// <summary>
        /// 获取 UIKit Root 是否已经存在；读取不会创建 Root 或触发资源加载。
        /// Editor Interaction 等只读调用方应使用此属性，避免引用 Unity Runtime Root 类型。
        /// </summary>
        public static bool HasRoot => UIRoot.TryGetExisting(out _);

        /// <summary>
        /// 获取或设置 Reusable 关闭缓存容量；设置会按 LRU 立即淘汰超额项。
        /// </summary>
        public static int ReusableCacheCapacity
        {
            get
            {
                UIKitController controller = GetExistingController();
                if (controller != null) return controller.ReusableCapacity;
                GameObject prefab = sRootPrefab;
                if (prefab == default) return UIRoot.DEFAULT_REUSABLE_CAPACITY;
                UIRoot root = prefab.GetComponentInChildren<UIRoot>(true);
                return root == default ? UIRoot.DEFAULT_REUSABLE_CAPACITY : root.InitialReusableCacheCapacity;
            }
            set
            {
                RequireController().ReusableCapacity = value;
            }
        }

        /// <summary>
        /// 获取或创建当前 Panel loader，供启动阶段在首次面板物化前配置加载策略。
        /// 默认 ResKit loader 可通过 <see cref="IPanelLoader.UseAddressableLocation"/> 切换 location 模式。
        /// 项目使用 Root Prefab Variant 时，必须先调用 <see cref="SetRootPrefab"/>。
        /// </summary>
        /// <returns>当前 Root 持有的 Panel loader。</returns>
        /// <exception cref="InvalidOperationException">Unity 正在退出，无法创建 UIKit Root 时抛出。</exception>
        public static IPanelLoader GetPanelLoader()
        {
            return RequireController().GetLoader();
        }

        /// <summary>
        /// 替换后续面板物化使用的 loader；已有实例继续由自己的 lease 释放。
        /// </summary>
        /// <param name="loader">返回独占 Prefab lease 的 loader。</param>
        public static void SetPanelLoader(IPanelLoader loader)
        {
            RequireController().SetLoader(loader);
        }

        /// <summary>
        /// 设置首次创建 UIRoot 时使用的项目 Prefab；未设置时使用包内默认模板。
        /// </summary>
        /// <param name="prefab">包含唯一 UIRoot 组件的项目 Prefab 或包内模板的 Prefab Variant。</param>
        /// <exception cref="ArgumentNullException">Prefab 为空时抛出。</exception>
        /// <exception cref="ArgumentException">Prefab 不包含 UIRoot 时抛出。</exception>
        /// <exception cref="InvalidOperationException">UIRoot 已经创建时抛出。</exception>
        public static void SetRootPrefab(GameObject prefab)
        {
            if (prefab == default) throw new ArgumentNullException(nameof(prefab));
            if (UIRoot.HasInstance)
                throw new InvalidOperationException("UIKit root prefab must be configured before UIRoot is created.");
            UIRoot[] roots = prefab.GetComponentsInChildren<UIRoot>(true);
            if (roots.Length != 1)
                throw new ArgumentException("UIKit root prefab must contain exactly one UIRoot component.", nameof(prefab));
            sRootPrefab = prefab;
        }

        /// <summary>
        /// 为 Root 显式绑定 Screen Space Camera；调用会按需创建 Root。
        /// </summary>
        /// <param name="camera">有效的 Unity Camera。</param>
        public static void BindRootCamera(Camera camera)
        {
            UIRoot root = RequireRoot();
            root.BindWorldCamera(camera);
        }

        /// <summary>
        /// 读取现有控制器，不触发 UIRoot 自动创建。
        /// </summary>
        private static UIKitController GetExistingController()
        {
            return UIRoot.TryGetExisting(out UIRoot root) ? root.Controller : null;
        }

        /// <summary>
        /// 获取变更操作所需 UIRoot；应用退出阶段创建失败时报告明确异常。
        /// </summary>
        private static UIRoot RequireRoot()
        {
            UIRoot root = GetOrCreateRoot();
            if (root == default) throw new InvalidOperationException("UIKit cannot create UIRoot while Unity is quitting.");
            return root;
        }

        /// <summary>
        /// 创建或复用 UIRoot，优先实例化项目显式传入的 Prefab，再使用包内默认模板。
        /// </summary>
        internal static UIRoot GetOrCreateRoot()
        {
            if (UIRoot.TryGetExisting(out UIRoot existing)) return existing;
            GameObject prefab = sRootPrefab;
            if (prefab == default) prefab = Resources.Load<GameObject>(DEFAULT_ROOT_PREFAB_PATH);
            if (prefab != default) return UIRoot.CreateFromPrefab(prefab);
            LogKit.Warning("UIKit root prefab was not found at Resources/UIKit; using the procedural fallback.");
            return UIRoot.CreateProceduralFallback();
        }

        /// <summary>
        /// 获取变更操作所需控制器。
        /// </summary>
        private static UIKitController RequireController()
        {
            return RequireRoot().Controller;
        }
    }
}
#endif
