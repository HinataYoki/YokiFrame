using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 层级与焦点管理扩展
    /// </summary>
    public partial class UIKit
    {
        #region 焦点系统 API

        /// <summary>
        /// 获取焦点系统实例
        /// </summary>
        public static UIFocusSystem FocusSystem => UIFocusSystem.Instance;

        /// <summary>
        /// 设置焦点到指定对象
        /// </summary>
        public static void SetFocus(UnityEngine.GameObject target)
        {
            var focusSystem = UIFocusSystem.Instance;
            if (focusSystem != default)
            {
                focusSystem.SetFocus(target);
            }
        }

        /// <summary>
        /// 设置焦点到指定 Selectable
        /// </summary>
        public static void SetFocus(UnityEngine.UI.Selectable selectable)
        {
            var focusSystem = UIFocusSystem.Instance;
            if (focusSystem != default)
            {
                focusSystem.SetFocus(selectable);
            }
        }

        /// <summary>
        /// 清除当前焦点
        /// </summary>
        public static void ClearFocus()
        {
            var focusSystem = UIFocusSystem.Instance;
            if (focusSystem != default)
            {
                focusSystem.ClearFocus();
            }
        }

        /// <summary>
        /// 获取当前焦点对象
        /// </summary>
        public static UnityEngine.GameObject GetCurrentFocus()
        {
            var focusSystem = UIFocusSystem.Instance;
            return focusSystem != default ? focusSystem.CurrentFocus : null;
        }

        /// <summary>
        /// 获取当前输入模式
        /// </summary>
        public static UIInputMode GetInputMode()
        {
            var focusSystem = UIFocusSystem.Instance;
            return focusSystem != default ? focusSystem.CurrentInputMode : UIInputMode.Pointer;
        }

        #endregion

        #region 层级管理 API

        /// <summary>
        /// 设置面板层级
        /// </summary>
        public static void SetPanelLevel(IPanel panel, UILevel level, int subLevel = 0)
        {
            UILevelManager.SetPanelLevel(panel, level, subLevel);
        }

        /// <summary>
        /// 设置面板子层级
        /// </summary>
        public static void SetPanelSubLevel(IPanel panel, int subLevel)
        {
            UILevelManager.SetPanelSubLevel(panel, subLevel);
        }

        /// <summary>
        /// 获取指定层级的顶部面板
        /// </summary>
        public static IPanel GetTopPanelAtLevel(UILevel level)
        {
            return UILevelManager.GetTopPanelAtLevel(level);
        }

        /// <summary>
        /// 获取全局顶部面板
        /// </summary>
        public static IPanel GetGlobalTopPanel()
        {
            return UILevelManager.GetGlobalTopPanel();
        }

        /// <summary>
        /// 获取指定层级的所有面板
        /// </summary>
        public static IReadOnlyList<IPanel> GetPanelsAtLevel(UILevel level)
        {
            return UILevelManager.GetPanelsAtLevel(level);
        }

        /// <summary>
        /// 设置面板为模态
        /// </summary>
        public static void SetPanelModal(IPanel panel, bool isModal)
        {
            UILevelManager.SetModal(panel, isModal);
        }

        /// <summary>
        /// 检查是否有模态面板阻断
        /// </summary>
        public static bool HasModalBlocker()
        {
            return UILevelManager.HasModalBlocker();
        }

        #endregion
    }
}
