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
    public static partial class UIKit
    {
        /// <summary>
        /// 获取指定类型的已物化 Panel；不创建 Root、不提升预加载项、不更新 LRU。
        /// </summary>
        public static T GetPanel<T>() where T : UIPanel
        {
            UIKitController controller = GetExistingController();
            return controller == null ? null : controller.GetPanel(typeof(T)) as T;
        }

        /// <summary>
        /// 同步打开指定类型 Panel；同类型异步加载进行中时会拒绝阻塞主线程。
        /// </summary>
        public static T OpenPanel<T>(
            UILevel level = default,
            IUIData data = null,
            string tag = null,
            PanelCachePolicy cachePolicy = PanelCachePolicy.Reusable) where T : UIPanel
        {
            return RequireController().Open(typeof(T), level, data, tag, cachePolicy) as T;
        }

        /// <summary>
        /// 异步打开指定类型 Panel；同类型调用共享物化但各自提交 Open 数据。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static async UniTask<T> OpenPanelAsync<T>(
#else
        public static async Task<T> OpenPanelAsync<T>(
#endif
            UILevel level = default,
            IUIData data = null,
            CancellationToken ct = default,
            string tag = null,
            PanelCachePolicy cachePolicy = PanelCachePolicy.Reusable) where T : UIPanel
        {
            ct.ThrowIfCancellationRequested();
            UIPanel panel = await RequireController().OpenAsync(typeof(T), level, data, tag, cachePolicy, ct);
            return panel as T;
        }

        /// <summary>
        /// 通过运行时 Type 异步打开 Panel。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static async UniTask<IPanel> OpenPanelAsync(
#else
        public static async Task<IPanel> OpenPanelAsync(
#endif
            Type panelType,
            UILevel level = default,
            IUIData data = null,
            CancellationToken ct = default,
            string tag = null,
            PanelCachePolicy cachePolicy = PanelCachePolicy.Reusable)
        {
            ct.ThrowIfCancellationRequested();
            return await RequireController().OpenAsync(panelType, level, data, tag, cachePolicy, ct);
        }

        /// <summary>
        /// 显示指定类型的已打开隐藏 Panel。
        /// </summary>
        public static void ShowPanel<T>() where T : UIPanel
        {
            ShowPanel(GetPanel<T>());
        }

        /// <summary>
        /// 显示传入的受管 Panel；预加载和关闭保留项必须先 Open。
        /// </summary>
        public static void ShowPanel(IPanel panel)
        {
            UIKitController controller = GetExistingController();
            if (controller != null && panel != null) controller.Show(panel);
        }

        /// <summary>
        /// 隐藏指定类型的当前可见 Panel。
        /// </summary>
        public static void HidePanel<T>() where T : UIPanel
        {
            HidePanel(GetPanel<T>());
        }

        /// <summary>
        /// 隐藏传入的受管 Panel，保留其打开轮次和栈归属。
        /// </summary>
        public static void HidePanel(IPanel panel)
        {
            UIKitController controller = GetExistingController();
            if (controller != null && panel != null) controller.Hide(panel);
        }

        /// <summary>
        /// 隐藏所有当前可见 Panel。
        /// </summary>
        public static void HideAllPanels()
        {
            GetExistingController()?.HideAll();
        }

        /// <summary>
        /// 关闭指定类型 Panel 的当前打开轮次。
        /// </summary>
        public static void ClosePanel<T>() where T : UIPanel
        {
            ClosePanel(GetPanel<T>());
        }

        /// <summary>
        /// 关闭传入 Panel，并按其显式缓存策略销毁或保留实例。
        /// </summary>
        public static void ClosePanel(IPanel panel)
        {
            UIKitController controller = GetExistingController();
            if (controller != null && panel != null) controller.Close(panel);
        }

        /// <summary>
        /// 关闭全部逻辑打开面板，不卸载纯预加载或已关闭保留项。
        /// </summary>
        public static void CloseAllPanels()
        {
            GetExistingController()?.CloseAll();
        }

        /// <summary>
        /// 关闭全部匹配 Tag 的打开面板。
        /// </summary>
        public static void ClosePanelsByTag(string tag)
        {
            UIKitController controller = GetExistingController();
            if (controller != null) controller.CloseByTag(tag);
        }
    }
}
#endif
