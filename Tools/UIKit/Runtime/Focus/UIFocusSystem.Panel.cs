using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// UIFocusSystem - 焦点控制和面板焦点管理
    /// </summary>
    public partial class UIFocusSystem
    {
        #region 焦点控制

        /// <summary>
        /// 设置焦点到指定对象
        /// </summary>
        public void SetFocus(GameObject target)
        {
            if (target == null || mEventSystem == null) return;

            var selectable = target.GetComponent<Selectable>();
            if (selectable != null && selectable.interactable)
            {
                mEventSystem.SetSelectedGameObject(target);
            }
        }

        /// <summary>
        /// 设置焦点到指定 Selectable
        /// </summary>
        public void SetFocus(Selectable selectable)
        {
            if (selectable == null || !selectable.interactable || mEventSystem == null) return;
            mEventSystem.SetSelectedGameObject(selectable.gameObject);
        }

        /// <summary>
        /// 清除当前焦点
        /// </summary>
        public void ClearFocus()
        {
            mEventSystem?.SetSelectedGameObject(null);
        }

        /// <summary>
        /// 恢复上次焦点
        /// </summary>
        public void RestoreLastFocus()
        {
            if (mLastFocusedObject != null && mLastFocusedObject.activeInHierarchy)
            {
                SetFocus(mLastFocusedObject);
                return;
            }

            // 尝试找到当前面板的第一个可选元素
            var topPanel = UIStackManager.Peek();
            if (topPanel != null)
            {
                var firstSelectable = FindFirstSelectable(topPanel.Transform);
                if (firstSelectable != null)
                {
                    SetFocus(firstSelectable);
                }
            }
        }

        #endregion

        #region 面板焦点管理

        /// <summary>
        /// 当面板显示时调用，设置默认焦点
        /// </summary>
        public void OnPanelShow(IPanel panel)
        {
            if (!mAutoFocusEnabled || panel == null) return;

            // 只在导航模式下自动聚焦
            if (mCurrentInputMode != UIInputMode.Navigation) return;

            // 优先恢复记忆的焦点
            if (mPanelFocusMemory.TryGetValue(panel, out var remembered) &&
                remembered != null && remembered.activeInHierarchy)
            {
                SetFocus(remembered);
                return;
            }

            // 查找默认焦点元素
            if (panel is UIPanel uiPanel)
            {
                var defaultSelectable = uiPanel.GetDefaultSelectable();
                if (defaultSelectable != null)
                {
                    SetFocus(defaultSelectable);
                    return;
                }
            }

            // 查找第一个可交互元素
            var firstSelectable = FindFirstSelectable(panel.Transform);
            if (firstSelectable != null)
            {
                SetFocus(firstSelectable);
            }
        }

        /// <summary>
        /// 当面板隐藏时调用，保存焦点记忆
        /// </summary>
        public void OnPanelHide(IPanel panel)
        {
            if (panel == null) return;

            // 如果当前焦点在该面板内，保存并清除
            var currentFocus = CurrentFocus;
            if (currentFocus != null && IsChildOf(currentFocus.transform, panel.Transform))
            {
                mPanelFocusMemory[panel] = currentFocus;
                ClearFocus();
            }
        }

        /// <summary>
        /// 当面板关闭时调用，清理焦点记忆
        /// </summary>
        public void OnPanelClose(IPanel panel)
        {
            if (panel == null) return;
            mPanelFocusMemory.Remove(panel);
        }

        /// <summary>
        /// 获取面板的记忆焦点
        /// </summary>
        public GameObject GetPanelFocusMemory(IPanel panel)
        {
            if (panel == null) return null;
            mPanelFocusMemory.TryGetValue(panel, out var focus);
            return focus;
        }

        /// <summary>
        /// 设置面板的记忆焦点
        /// </summary>
        public void SetPanelFocusMemory(IPanel panel, GameObject focus)
        {
            if (panel == null) return;
            if (focus == null)
            {
                mPanelFocusMemory.Remove(panel);
            }
            else
            {
                mPanelFocusMemory[panel] = focus;
            }
        }

        #endregion
    }
}
