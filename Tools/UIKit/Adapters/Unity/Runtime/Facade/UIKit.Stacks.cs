#if UNITY_2022_3_OR_NEWER
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
        /// 把指定类型的已打开 Panel 压入默认栈。
        /// </summary>
        public static void PushPanel<T>(bool hidePrevious = true) where T : UIPanel
        {
            PushPanel(GetPanel<T>(), DEFAULT_STACK, hidePrevious);
        }

        /// <summary>
        /// 把受管 Panel 压入默认栈。
        /// </summary>
        public static void PushPanel(IPanel panel, bool hidePrevious = true)
        {
            PushPanel(panel, DEFAULT_STACK, hidePrevious);
        }

        /// <summary>
        /// 把受管 Panel 压入指定命名栈。
        /// </summary>
        public static void PushPanel(IPanel panel, string stackName, bool hidePrevious = true)
        {
            UIKitController controller = GetExistingController();
            if (controller != null && panel != null) controller.Push(panel, stackName, hidePrevious);
        }

        /// <summary>
        /// 同步打开 Panel 并压入命名栈，返回受管实例。
        /// </summary>
        public static T PushOpenPanel<T>(
            UILevel level = default,
            IUIData data = null,
            bool hidePrevious = true,
            string stackName = DEFAULT_STACK,
            string tag = null,
            PanelCachePolicy cachePolicy = PanelCachePolicy.Reusable) where T : UIPanel
        {
            T panel = OpenPanel<T>(level, data, tag, cachePolicy);
            RequireController().Push(panel, stackName, hidePrevious);
            return panel;
        }

        /// <summary>
        /// 异步打开 Panel 并压入命名栈，取消只影响当前等待者。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static async UniTask<T> PushOpenPanelAsync<T>(
#else
        public static async Task<T> PushOpenPanelAsync<T>(
#endif
            UILevel level = default,
            IUIData data = null,
            bool hidePrevious = true,
            CancellationToken ct = default,
            string stackName = DEFAULT_STACK,
            string tag = null,
            PanelCachePolicy cachePolicy = PanelCachePolicy.Reusable) where T : UIPanel
        {
            T panel = await OpenPanelAsync<T>(level, data, ct, tag, cachePolicy);
            ct.ThrowIfCancellationRequested();
            RequireController().Push(panel, stackName, hidePrevious);
            return panel;
        }

        /// <summary>
        /// 弹出默认栈顶部，并按需恢复旧栈顶与关闭弹出项。
        /// </summary>
        public static IPanel PopPanel(bool showPrevious = true, bool autoClose = true)
        {
            return PopPanel(DEFAULT_STACK, showPrevious, autoClose);
        }

        /// <summary>
        /// 弹出指定命名栈顶部。
        /// </summary>
        public static IPanel PopPanel(string stackName, bool showPrevious = true, bool autoClose = true)
        {
            UIKitController controller = GetExistingController();
            return controller == null ? null : controller.Pop(stackName, showPrevious, autoClose);
        }

        /// <summary>
        /// 以 Task/UniTask 形态弹出命名栈顶部。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static async UniTask<IPanel> PopPanelAsync(
#else
        public static async Task<IPanel> PopPanelAsync(
#endif
            string stackName = DEFAULT_STACK,
            bool showPrevious = true,
            bool autoClose = true,
            CancellationToken ct = default)
        {
            UIKitController controller = GetExistingController();
            if (controller == null) return null;
            return await controller.PopAsync(stackName, showPrevious, autoClose, ct);
        }

        /// <summary>
        /// 查看命名栈顶部，不改变焦点或可见性。
        /// </summary>
        public static IPanel PeekPanel(string stackName = DEFAULT_STACK)
        {
            UIKitController controller = GetExistingController();
            return controller == null ? null : controller.Peek(stackName);
        }

        /// <summary>
        /// 获取命名栈深度。
        /// </summary>
        public static int GetStackDepth(string stackName = DEFAULT_STACK)
        {
            UIKitController controller = GetExistingController();
            return controller == null ? 0 : controller.GetStackDepth(stackName);
        }

        /// <summary>
        /// 获取当前非空栈名称的稳定排序快照。
        /// </summary>
        public static IReadOnlyCollection<string> GetAllStackNames()
        {
            UIKitController controller = GetExistingController();
            return controller == null ? System.Array.Empty<string>() : controller.GetStackNames();
        }

        /// <summary>
        /// 判断面板是否属于任意命名栈。
        /// </summary>
        public static bool IsInStack(IPanel panel)
        {
            UIKitController controller = GetExistingController();
            return controller != null && panel != null && controller.IsInStack(panel);
        }

        /// <summary>
        /// 获取面板所在栈名称；未入栈时返回 null。
        /// </summary>
        public static string GetPanelStackName(IPanel panel)
        {
            UIKitController controller = GetExistingController();
            return controller == null || panel == null ? null : controller.GetStackName(panel);
        }

        /// <summary>
        /// 清空指定命名栈，可选择统一关闭被摘除面板。
        /// </summary>
        public static void ClearStack(string stackName = DEFAULT_STACK, bool closeAll = true)
        {
            UIKitController controller = GetExistingController();
            if (controller != null) controller.ClearStack(stackName, closeAll);
        }
    }
}
#endif
