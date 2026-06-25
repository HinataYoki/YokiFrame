#if !GODOT
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 兼容入口与诊断快照入口。
    /// </summary>
    public partial class UIKit
    {
        /// <summary>
        /// 获取默认 UI 栈名称。
        /// </summary>
        public const string DEFAULT_STACK = UIRoot.DEFAULT_STACK;

        /// <summary>
        /// 获取默认 UI 栈名称。
        /// </summary>
        public static string DefaultStack => DEFAULT_STACK;

        private static IUIBackend sCompatibilityBackend;

        /// <summary>
        /// 获取 UIKit 是否已经初始化或已注入兼容 backend。
        /// </summary>
        public static bool IsInitialized => UIRoot.ExistingInstance != default || sCompatibilityBackend != default;

        /// <summary>
        /// 注入兼容 backend。
        /// </summary>
        /// <param name="backend">要注入的 UI backend。</param>
        public static void SetBackend(IUIBackend backend)
        {
            sCompatibilityBackend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        /// <summary>
        /// 获取当前注入的兼容 backend。
        /// </summary>
        /// <returns>当前兼容 backend；未注入时返回 null。</returns>
        public static IUIBackend GetBackend() => sCompatibilityBackend;

        /// <summary>
        /// 清除当前注入的兼容 backend。
        /// </summary>
        public static void ClearBackend()
        {
            sCompatibilityBackend = null;
        }

        /// <summary>
        /// 重置兼容 backend，并在 UIRoot 存在时关闭所有面板。
        /// </summary>
        public static void Reset()
        {
            sCompatibilityBackend = null;

            var root = UIRoot.ExistingInstance;
            if (root == default)
                return;

            CloseAllPanel();
        }

        internal static UIKitDiagnosticsSnapshot CreateDiagnosticsSnapshot()
        {
            var root = UIRoot.ExistingInstance;
            var panels = new List<UIPanelDiagnosticsSnapshot>();
            var stacks = new List<UIStackDiagnosticsSnapshot>();

            if (root != default)
            {
                root.AppendDiagnosticsPanels(panels);
                root.AppendDiagnosticsStacks(stacks);
            }

            return new UIKitDiagnosticsSnapshot(
                IsInitialized,
                sCompatibilityBackend != default ? sCompatibilityBackend.BackendName : "Unity.UIRoot",
                panels,
                stacks);
        }
    }
}
#endif
