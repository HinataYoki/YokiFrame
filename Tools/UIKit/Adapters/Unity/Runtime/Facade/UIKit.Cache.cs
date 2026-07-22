#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame
{
    public static partial class UIKit
    {
        /// <summary>
        /// 判断指定类型是否已有物化实例。
        /// </summary>
        public static bool IsPanelLoaded<T>() where T : UIPanel
        {
            return IsPanelLoaded(typeof(T));
        }

        /// <summary>
        /// 判断指定运行时类型是否已有物化实例；读取不会创建 Root。
        /// </summary>
        public static bool IsPanelLoaded(Type panelType)
        {
            UIKitController controller = GetExistingController();
            return controller != null && controller.IsLoaded(panelType);
        }

        /// <summary>
        /// 判断指定类型是否仍是从未打开的预加载实例。
        /// </summary>
        public static bool IsPanelPreloaded<T>() where T : UIPanel
        {
            return IsPanelPreloaded(typeof(T));
        }

        /// <summary>
        /// 判断指定运行时类型是否仍是从未打开的预加载实例。
        /// </summary>
        public static bool IsPanelPreloaded(Type panelType)
        {
            UIKitController controller = GetExistingController();
            return controller != null && controller.IsPreloaded(panelType);
        }

        /// <summary>
        /// 获取已加载 Panel 类型的稳定排序快照。
        /// </summary>
        public static IReadOnlyCollection<Type> GetLoadedPanelTypes()
        {
            UIKitController controller = GetExistingController();
            return controller == null ? Array.Empty<Type>() : controller.GetLoadedTypes();
        }

        /// <summary>
        /// 获取全部已物化 Panel 的稳定排序快照。
        /// </summary>
        public static IReadOnlyList<IPanel> GetLoadedPanels()
        {
            UIKitController controller = GetExistingController();
            return controller == null ? Array.Empty<IPanel>() : controller.GetLoadedPanels();
        }

        /// <summary>
        /// 异步预加载指定类型，只执行物化和一次 OnInit。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static async UniTask<bool> PreloadPanelAsync<T>(
#else
        public static async Task<bool> PreloadPanelAsync<T>(
#endif
            UILevel level = default,
            CancellationToken ct = default,
            PanelCachePolicy cachePolicy = PanelCachePolicy.Reusable) where T : UIPanel
        {
            ct.ThrowIfCancellationRequested();
            return await RequireController().PreloadAsync(typeof(T), level, cachePolicy, ct);
        }

        /// <summary>
        /// 通过运行时 Type 异步预加载 Panel。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static async UniTask<bool> PreloadPanelAsync(
#else
        public static async Task<bool> PreloadPanelAsync(
#endif
            Type panelType,
            UILevel level = default,
            CancellationToken ct = default,
            PanelCachePolicy cachePolicy = PanelCachePolicy.Reusable)
        {
            ct.ThrowIfCancellationRequested();
            return await RequireController().PreloadAsync(panelType, level, cachePolicy, ct);
        }

        /// <summary>
        /// 卸载指定类型的预加载或关闭保留实例；活动面板不会被隐式关闭。
        /// </summary>
        public static bool UnloadPanel<T>() where T : UIPanel
        {
            return UnloadPanel(typeof(T));
        }

        /// <summary>
        /// 卸载指定运行时类型的 inactive 实例。
        /// </summary>
        public static bool UnloadPanel(Type panelType)
        {
            UIKitController controller = GetExistingController();
            return controller != null && controller.Unload(panelType);
        }

        /// <summary>
        /// 清空所有 inactive Reusable 实例，不影响 Persistent 或活动面板。
        /// </summary>
        public static int ClearReusableCache()
        {
            UIKitController controller = GetExistingController();
            return controller == null ? 0 : controller.ClearReusableCache();
        }
    }
}
#endif
