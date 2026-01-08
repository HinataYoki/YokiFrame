using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// UI 输入模式
    /// </summary>
    public enum UIInputMode
    {
        /// <summary>
        /// 鼠标/触摸模式
        /// </summary>
        Pointer,
        
        /// <summary>
        /// 手柄/键盘导航模式
        /// </summary>
        Navigation
    }

    /// <summary>
    /// 焦点变更事件
    /// </summary>
    public struct FocusChangedEvent
    {
        public GameObject Previous;
        public GameObject Current;
        public IPanel Panel;
    }

    /// <summary>
    /// 输入模式变更事件
    /// </summary>
    public struct InputModeChangedEvent
    {
        public UIInputMode Previous;
        public UIInputMode Current;
    }

    /// <summary>
    /// UI 焦点系统 - 管理 UI 焦点和导航
    /// </summary>
    public class UIFocusSystem : MonoBehaviour
    {
        #region 单例

        private static UIFocusSystem sInstance;

        /// <summary>
        /// 获取焦点系统实例
        /// </summary>
        public static UIFocusSystem Instance
        {
            get
            {
                if (sInstance == null)
                {
                    sInstance = FindFirstObjectByType<UIFocusSystem>();
                    if (sInstance == null && UIRoot.Instance != null)
                    {
                        sInstance = UIRoot.Instance.gameObject.AddComponent<UIFocusSystem>();
                    }
                }
                return sInstance;
            }
        }

        #endregion

        #region 配置

        /// <summary>
        /// 是否启用自动焦点（面板显示时自动聚焦默认元素）
        /// </summary>
        [SerializeField] private bool mAutoFocusEnabled = true;

        /// <summary>
        /// 导航输入检测阈值
        /// </summary>
        [SerializeField] private float mNavigationThreshold = 0.5f;

        #endregion

        #region 状态

        private UIInputMode mCurrentInputMode = UIInputMode.Pointer;
        private GameObject mLastFocusedObject;
        private readonly Dictionary<IPanel, GameObject> mPanelFocusMemory = new();
        private EventSystem mEventSystem;

        /// <summary>
        /// 当前输入模式
        /// </summary>
        public UIInputMode CurrentInputMode => mCurrentInputMode;

        /// <summary>
        /// 当前焦点对象
        /// </summary>
        public GameObject CurrentFocus => mEventSystem?.currentSelectedGameObject;

        /// <summary>
        /// 是否启用自动焦点
        /// </summary>
        public bool AutoFocusEnabled
        {
            get => mAutoFocusEnabled;
            set => mAutoFocusEnabled = value;
        }

        #endregion

        #region 生命周期

        private void Awake()
        {
            if (sInstance != null && sInstance != this)
            {
                Destroy(this);
                return;
            }
            sInstance = this;
        }

        private void Start()
        {
            mEventSystem = EventSystem.current;
            if (mEventSystem == null)
            {
                mEventSystem = FindFirstObjectByType<EventSystem>();
            }
        }

        private void Update()
        {
            DetectInputModeChange();
            TrackFocusChange();
        }

        private void OnDestroy()
        {
            if (sInstance == this)
            {
                sInstance = null;
            }
            mPanelFocusMemory.Clear();
        }

        #endregion

        #region 输入模式检测

        private void DetectInputModeChange()
        {
            var newMode = mCurrentInputMode;

            // 检测鼠标移动
            if (Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0 || Input.GetMouseButtonDown(0))
            {
                newMode = UIInputMode.Pointer;
            }
            // 检测导航输入
            else if (Mathf.Abs(Input.GetAxis("Horizontal")) > mNavigationThreshold ||
                     Mathf.Abs(Input.GetAxis("Vertical")) > mNavigationThreshold ||
                     Input.GetButtonDown("Submit") ||
                     Input.GetButtonDown("Cancel"))
            {
                newMode = UIInputMode.Navigation;
            }

            if (newMode != mCurrentInputMode)
            {
                var oldMode = mCurrentInputMode;
                mCurrentInputMode = newMode;
                
                EventKit.Type.Send(new InputModeChangedEvent
                {
                    Previous = oldMode,
                    Current = newMode
                });

                // 切换到导航模式时，确保有焦点
                if (newMode == UIInputMode.Navigation && CurrentFocus == null)
                {
                    RestoreLastFocus();
                }
            }
        }

        #endregion

        #region 焦点追踪

        private void TrackFocusChange()
        {
            var currentFocus = CurrentFocus;
            if (currentFocus != mLastFocusedObject)
            {
                var panel = FindPanelForObject(currentFocus);
                
                EventKit.Type.Send(new FocusChangedEvent
                {
                    Previous = mLastFocusedObject,
                    Current = currentFocus,
                    Panel = panel
                });

                // 记忆面板焦点
                if (panel != null && currentFocus != null)
                {
                    mPanelFocusMemory[panel] = currentFocus;
                }

                mLastFocusedObject = currentFocus;
            }
        }

        private IPanel FindPanelForObject(GameObject obj)
        {
            if (obj == null) return null;
            
            var transform = obj.transform;
            while (transform != null)
            {
                var panel = transform.GetComponent<IPanel>();
                if (panel != null) return panel;
                transform = transform.parent;
            }
            return null;
        }

        #endregion

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
            if (mEventSystem != null)
            {
                mEventSystem.SetSelectedGameObject(null);
            }
        }

        /// <summary>
        /// 恢复上次焦点
        /// </summary>
        public void RestoreLastFocus()
        {
            if (mLastFocusedObject != null && mLastFocusedObject.activeInHierarchy)
            {
                SetFocus(mLastFocusedObject);
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

            // 优先恢复记忆的焦点
            if (mPanelFocusMemory.TryGetValue(panel, out var remembered) && 
                remembered != null && remembered.activeInHierarchy)
            {
                SetFocus(remembered);
                return;
            }

            // 查找默认焦点元素
            var uiPanel = panel as UIPanel;
            if (uiPanel != null)
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

        #region 辅助方法

        private Selectable FindFirstSelectable(Transform root)
        {
            if (root == null) return null;

            // 使用 GetComponentsInChildren 查找所有 Selectable
            var selectables = root.GetComponentsInChildren<Selectable>(false);
            foreach (var selectable in selectables)
            {
                if (selectable.interactable && selectable.gameObject.activeInHierarchy)
                {
                    return selectable;
                }
            }
            return null;
        }

        private bool IsChildOf(Transform child, Transform parent)
        {
            if (child == null || parent == null) return false;
            
            var current = child;
            while (current != null)
            {
                if (current == parent) return true;
                current = current.parent;
            }
            return false;
        }

        #endregion
    }
}
