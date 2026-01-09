using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 栈管理扩展
    /// </summary>
    public partial class UIKit
    {
        #region 栈管理 API

        /// <summary>
        /// 压入一个Panel到栈中
        /// </summary>
        /// <param name="hidePreLevel">隐藏栈中上一层UI</param>
        public static void PushPanel<T>(bool hidePreLevel = true) where T : UIPanel
        {
            var panel = GetPanel<T>();
            if (panel != null) PushPanel(panel, hidePreLevel);
        }
        
        /// <summary>
        /// 压入一个Panel到栈中
        /// </summary>
        /// <param name="hidePreLevel">隐藏栈中上一层UI</param>
        public static void PushPanel(IPanel panel, bool hidePreLevel = true)
        {
            UIStackManager.Push(panel, UIStackManager.DEFAULT_STACK, hidePreLevel);
        }

        /// <summary>
        /// 压入一个Panel到指定命名栈中
        /// </summary>
        /// <param name="stackName">栈名称</param>
        /// <param name="hidePreLevel">隐藏栈中上一层UI</param>
        public static void PushPanel(IPanel panel, string stackName, bool hidePreLevel = true)
        {
            UIStackManager.Push(panel, stackName, hidePreLevel);
        }
        
        /// <summary>
        /// 打开并且压入指定类型的Panel到栈中
        /// </summary>
        public static void PushOpenPanel<T>(UILevel level = UILevel.Common, 
            IUIData data = null, bool hidePreLevel = true) where T : UIPanel
        {
            var panel = OpenPanel<T>(level, data);
            PushPanel(panel, hidePreLevel);
        }
        
        /// <summary>
        /// 异步打开并且压入指定类型的Panel到栈中
        /// </summary>
        public static void PushOpenPanelAsync<T>(Action<IPanel> callback = null, 
            UILevel level = UILevel.Common, IUIData data = null, bool hidePreLevel = true) where T : UIPanel
        {
            OpenPanelAsync<T>(panel =>
            {
                PushPanel(panel, hidePreLevel);
                callback?.Invoke(panel);
            }, level, data);
        }
        
        /// <summary>
        /// 弹出一个面板
        /// </summary>
        /// <param name="showPreLevel">自动显示上一层面板</param>
        /// <param name="autoClose">自动关闭弹出面板</param>
        public static IPanel PopPanel(bool showPreLevel = true, bool autoClose = true)
        {
            return UIStackManager.Pop(UIStackManager.DEFAULT_STACK, showPreLevel, autoClose);
        }

        /// <summary>
        /// 从指定命名栈弹出一个面板
        /// </summary>
        /// <param name="stackName">栈名称</param>
        /// <param name="showPreLevel">自动显示上一层面板</param>
        /// <param name="autoClose">自动关闭弹出面板</param>
        public static IPanel PopPanel(string stackName, bool showPreLevel = true, bool autoClose = true)
        {
            return UIStackManager.Pop(stackName, showPreLevel, autoClose);
        }

        /// <summary>
        /// 查看栈顶面板（不移除）
        /// </summary>
        public static IPanel PeekPanel(string stackName = UIStackManager.DEFAULT_STACK)
        {
            return UIStackManager.Peek(stackName);
        }

        /// <summary>
        /// 获取指定栈的深度
        /// </summary>
        public static int GetStackDepth(string stackName = UIStackManager.DEFAULT_STACK)
        {
            return UIStackManager.GetDepth(stackName);
        }

        /// <summary>
        /// 获取所有栈名称
        /// </summary>
        public static IReadOnlyCollection<string> GetAllStackNames()
        {
            return UIStackManager.GetStackNames();
        }

        /// <summary>
        /// 清空指定栈
        /// </summary>
        public static void ClearStack(string stackName = UIStackManager.DEFAULT_STACK, bool closeAll = true)
        {
            UIStackManager.Clear(stackName, closeAll);
        }
        
        /// <summary>
        /// 关闭所有栈上面板
        /// </summary>
        public static void CloseAllStackPanel()
        {
            UIStackManager.Clear(UIStackManager.DEFAULT_STACK, true);
        }

        #endregion
    }
}
