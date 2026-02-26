using System;
using System.Collections.Generic;

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
            Root?.PushToStack(panel, UIRoot.DEFAULT_STACK, hidePreLevel);
        }

        /// <summary>
        /// 压入 Panel 到指定命名栈
        /// </summary>
        public static void PushPanel(IPanel panel, string stackName, bool hidePreLevel = true)
        {
            Root?.PushToStack(panel, stackName, hidePreLevel);
        }

        /// <summary>
        /// 打开并压入 Panel 到栈中
        /// </summary>
        public static void PushOpenPanel<T>(UILevel level = UILevel.Common,
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
            UILevel level = UILevel.Common, IUIData data = null, bool hidePreLevel = true) where T : UIPanel
        {
            var root = Root;
            if (root == default) return;
            
            OpenPanelAsync<T>(panel =>
            {
                root.PushToStack(panel, UIRoot.DEFAULT_STACK, hidePreLevel);
                callback?.Invoke(panel);
            }, level, data);
        }

        /// <summary>
        /// 弹出面板
        /// </summary>
        public static IPanel PopPanel(bool showPreLevel = true, bool autoClose = true)
        {
            return Root?.PopFromStack(UIRoot.DEFAULT_STACK, showPreLevel, autoClose);
        }

        /// <summary>
        /// 从指定命名栈弹出面板
        /// </summary>
        public static IPanel PopPanel(string stackName, bool showPreLevel = true, bool autoClose = true)
        {
            return Root?.PopFromStack(stackName, showPreLevel, autoClose);
        }

        /// <summary>
        /// 查看栈顶面板
        /// </summary>
        public static IPanel PeekPanel(string stackName = UIRoot.DEFAULT_STACK)
        {
            return Root?.PeekStack(stackName);
        }

        /// <summary>
        /// 获取栈深度
        /// </summary>
        public static int GetStackDepth(string stackName = UIRoot.DEFAULT_STACK)
        {
            return Root?.GetStackDepth(stackName) ?? 0;
        }

        /// <summary>
        /// 获取所有栈名称
        /// </summary>
        public static IReadOnlyCollection<string> GetAllStackNames()
        {
            return Root?.GetAllStackNames() ?? Array.Empty<string>();
        }

        /// <summary>
        /// 清空指定栈
        /// </summary>
        public static void ClearStack(string stackName = UIRoot.DEFAULT_STACK, bool closeAll = true)
        {
            Root?.ClearStack(stackName, closeAll);
        }

        #endregion
    }
}
