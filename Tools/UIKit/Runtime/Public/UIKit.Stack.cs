#if !GODOT
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
    /// <summary>
    /// UI 管理工具 - 堆栈管理
    /// </summary>
    public partial class UIKit
    {
        #region 堆栈

        /// <summary>
        /// 压入 Panel 到栈中
        /// </summary>
        public static void PushPanel<T>(bool hidePreLevel = true) where T : UIPanel
        {
            var root = Root;
            if (root == default) return;
            
            var panel = GetPanel<T>();
            if (panel != default) root.PushToStack(panel, UIRoot.DEFAULT_STACK, hidePreLevel);
        }

        /// <summary>
        /// 压入 Panel 到栈中
        /// </summary>
        public static void PushPanel(IPanel panel, bool hidePreLevel = true)
        {
            var root = Root;
            if (root == default) return;
            root.PushToStack(panel, UIRoot.DEFAULT_STACK, hidePreLevel);
        }

        /// <summary>
        /// 压入 Panel 到指定命名栈
        /// </summary>
        public static void PushPanel(IPanel panel, string stackName, bool hidePreLevel = true)
        {
            var root = Root;
            if (root == default) return;
            root.PushToStack(panel, stackName, hidePreLevel);
        }

        /// <summary>
        /// 打开并压入 Panel 到栈中
        /// </summary>
        public static void PushOpenPanel<T>(UILevel level = default,
            IUIData data = null, bool hidePreLevel = true) where T : UIPanel
        {
            var root = Root;
            if (root == default) return;
            
            var panel = OpenPanel<T>(level, data);
            root.PushToStack(panel, UIRoot.DEFAULT_STACK, hidePreLevel);
        }

        /// <summary>
        /// 异步打开并压入 Panel 到栈中
        /// </summary>
        public static void PushOpenPanelAsync<T>(Action<IPanel> callback = null,
            UILevel level = default, IUIData data = null, bool hidePreLevel = true) where T : UIPanel
        {
            var root = Root;
            if (root == default)
            {
                callback?.Invoke(null);
                return;
            }

            OpenPanelAsync<T>(panel =>
            {
                if (panel != default)
                {
                    root.PushToStack(panel, UIRoot.DEFAULT_STACK, hidePreLevel);
                }

                callback?.Invoke(panel);
            }, level, data);
        }

        /// <summary>
        /// 异步打开并压入 Panel 到栈中
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static async UniTask<T> PushOpenPanelAsync<T>(UILevel level = default,
            IUIData data = null, bool hidePreLevel = true, CancellationToken ct = default) where T : UIPanel
#else
        public static async Task<T> PushOpenPanelAsync<T>(UILevel level = default,
            IUIData data = null, bool hidePreLevel = true, CancellationToken ct = default) where T : UIPanel
#endif
        {
            var root = Root;
            if (root == default) return null;

#if YOKIFRAME_UNITASK_SUPPORT
            var panel = await OpenPanelAsync<T>(level, data, ct);
#else
            var panel = await OpenPanelAsync<T>(level, data, ct).ConfigureAwait(false);
#endif
            if (panel != default)
            {
                root.PushToStack(panel, UIRoot.DEFAULT_STACK, hidePreLevel);
            }

            return panel;
        }

        /// <summary>
        /// 弹出面板
        /// </summary>
        public static IPanel PopPanel(bool showPreLevel = true, bool autoClose = true)
        {
            var root = Root;
            if (root == default) return null;
            return root.PopFromStack(UIRoot.DEFAULT_STACK, showPreLevel, autoClose);
        }

        /// <summary>
        /// 从指定命名栈弹出面板
        /// </summary>
        public static IPanel PopPanel(string stackName, bool showPreLevel = true, bool autoClose = true)
        {
            var root = Root;
            if (root == default) return null;
            return root.PopFromStack(stackName, showPreLevel, autoClose);
        }

        /// <summary>
        /// 异步弹出面板。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static UniTask<IPanel> PopPanelAsync(string stackName = UIRoot.DEFAULT_STACK,
            bool showPreLevel = true, bool autoClose = true, CancellationToken ct = default)
#else
        public static Task<IPanel> PopPanelAsync(string stackName = UIRoot.DEFAULT_STACK,
            bool showPreLevel = true, bool autoClose = true, CancellationToken ct = default)
#endif
        {
            var root = Root;
            if (root != default)
                return root.PopFromStackAsync(stackName, showPreLevel, autoClose, ct);

#if YOKIFRAME_UNITASK_SUPPORT
            return UniTask.FromResult<IPanel>(null);
#else
            return Task.FromResult<IPanel>(null);
#endif
        }

        /// <summary>
        /// 查看栈顶面板
        /// </summary>
        public static IPanel PeekPanel(string stackName = UIRoot.DEFAULT_STACK)
        {
            var root = Root;
            if (root == default) return null;
            return root.PeekStack(stackName);
        }

        /// <summary>
        /// 获取栈深度
        /// </summary>
        public static int GetStackDepth(string stackName = UIRoot.DEFAULT_STACK)
        {
            var root = Root;
            return root != default ? root.GetStackDepth(stackName) : 0;
        }

        /// <summary>
        /// 获取所有栈名称
        /// </summary>
        public static IReadOnlyCollection<string> GetAllStackNames()
        {
            var root = Root;
            return root != default ? root.GetAllStackNames() : Array.Empty<string>();
        }

        /// <summary>
        /// 检查面板是否在任一 UI 栈中。
        /// </summary>
        public static bool IsInStack(IPanel panel)
        {
            var root = Root;
            return root != default && root.IsInStack(panel);
        }

        /// <summary>
        /// 获取面板所在的栈名称。
        /// </summary>
        public static string GetPanelStackName(IPanel panel)
        {
            var root = Root;
            return root != default ? root.GetPanelStackName(panel) : null;
        }

        /// <summary>
        /// 清空指定栈
        /// </summary>
        public static void ClearStack(string stackName = UIRoot.DEFAULT_STACK, bool closeAll = true)
        {
            var root = Root;
            if (root == default) return;
            root.ClearStack(stackName, closeAll);
        }

        #endregion
    }
}
#endif
