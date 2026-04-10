using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// UIRoot - 焦点子系统
    /// </summary>
    public partial class UIRoot
    {
        #region 焦点组件

#if YOKIFRAME_INPUTSYSTEM_SUPPORT
        private GamepadInputHandler mInputHandler;
#endif
        private GamepadNavigator mNavigator;
        private UIFocusHighlight mFocusHighlight;

        #endregion

        #region 焦点状态

        private UIInputMode mCurrentInputMode = UIInputMode.Pointer;
        private GameObject mLastFocusedObject;
        private readonly Dictionary<IPanel, GameObject> mPanelFocusMemory = new();

        #endregion

        #region 焦点属性

        /// <summary>
        /// 当前输入模式
        /// </summary>
        public UIInputMode CurrentInputMode => mCurrentInputMode;

        /// <summary>
        /// 当前焦点对象
        /// </summary>
        public GameObject CurrentFocus => EventSystem != default ? EventSystem.currentSelectedGameObject : null;

        /// <summary>
        /// 是否启用焦点系统
        /// </summary>
        public bool FocusSystemEnabled
        {
            get => mConfig.EnableFocusSystem;
            set
            {
                mConfig.EnableFocusSystem = value;
                if (value) InitializeFocusSystem();
                else DisposeFocusSystem();
            }
        }

        /// <summary>
        /// 是否启用手柄支持
        /// </summary>
        public bool GamepadEnabled
        {
            get => mConfig.EnableGamepad;
            set
            {
                mConfig.EnableGamepad = value;
                if (value) EnableGamepad();
                else DisableGamepad();
            }
        }

        /// <summary>
        /// /// 手柄导航器
        /// </summary>
        public GamepadNavigator Navigator => mNavigator;

        /// <summary>
        /// 手柄配置
        /// </summary>
        public GamepadConfig GamepadConfig => mConfig.GamepadConfig != default
            ? mConfig.GamepadConfig
            : GamepadConfig.Default;

        #endregion

        #region 焦点初始化

        private void InitializeFocusSystem()
        {
            if (!mConfig.EnableFocusSystem) return;
            if (mConfig.EnableGamepad) InitializeGamepad();
            InitializeFocusHighlight();
        }

        private void InitializeGamepad()
        {
            if (!mConfig.EnableGamepad) return;

#if YOKIFRAME_INPUTSYSTEM_SUPPORT
            mInputHandler = new GamepadInputHandler();
            mNavigator = new GamepadNavigator(mInputHandler, GamepadConfig, EventSystem);
            mNavigator.Enable();
#endif
        }

        private void InitializeFocusHighlight()
        {
            if (mFocusHighlight != default) return;
            mFocusHighlight = UIFocusHighlight.Create(transform, GamepadConfig);
            mFocusHighlight.transform.SetAsLastSibling();
        }

        private void DisposeFocusSystem()
        {
            if (mNavigator != default)
            {
                mNavigator.Dispose();
                mNavigator = null;
            }
#if YOKIFRAME_INPUTSYSTEM_SUPPORT
            if (mInputHandler != default)
            {
                mInputHandler.Dispose();
                mInputHandler = null;
            }
#endif
            mPanelFocusMemory.Clear();

            if (mFocusHighlight != default)
            {
                // OnDestroy 中必须使用 DestroyImmediate，且需判空防止重复销毁
                if (mFocusHighlight.gameObject != default)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        UnityEngine.Object.DestroyImmediate(mFocusHighlight.gameObject);
                    else
#endif
                    {
                        // 先停止所有动画，避免 DOTween 持有引用
                        mFocusHighlight.Hide();
                        UnityEngine.Object.DestroyImmediate(mFocusHighlight.gameObject);
                    }
                }
                mFocusHighlight = null;
            }
        }

        #endregion

        #region 焦点控制

        /// <summary>
        /// 设置焦点到指定对象
        /// </summary>
        public void SetFocus(GameObject target)
        {
            if (target == default || EventSystem == default) return;
            if (target.TryGetComponent<Selectable>(out var selectable) && selectable.interactable)
            {
                EventSystem.SetSelectedGameObject(target);
            }
        }

        /// <summary>
        /// 设置焦点到指定 Selectable
        /// </summary>
        public void SetFocus(Selectable selectable)
        {
            if (selectable == default || !selectable.interactable || EventSystem == default) return;
            EventSystem.SetSelectedGameObject(selectable.gameObject);
        }

        /// <summary>
        /// 清除当前焦点
        /// </summary>
        public void ClearFocus()
        {
            if (EventSystem != default) EventSystem.SetSelectedGameObject(null);
        }

        /// <summary>
        /// 恢复上次焦点
        /// </summary>
        public void RestoreLastFocus()
        {
            if (mLastFocusedObject != default && mLastFocusedObject.activeInHierarchy)
            {
                SetFocus(mLastFocusedObject);
                return;
            }

            var topPanel = PeekStack();
            if (topPanel != default)
            {
                var firstSelectable = FindFirstSelectable(topPanel.Transform);
                if (firstSelectable != default) SetFocus(firstSelectable);
            }
        }

        #endregion

        #region 面板焦点管理

        /// <summary>
        /// 面板显示时处理焦点
        /// </summary>
        internal void OnPanelShowFocus(IPanel panel)
        {
            if (!mConfig.EnableFocusSystem || panel == default) return;
            if (mCurrentInputMode != UIInputMode.Navigation) return;

            // 检查面板级配置
            if (panel is UIPanel uiPanel && !uiPanel.AutoFocusOnShow) return;

            // 优先恢复记忆的焦点
            if (mPanelFocusMemory.TryGetValue(panel, out var remembered) &&
                remembered != default && remembered.activeInHierarchy)
            {
                SetFocus(remembered);
                return;
            }

            // 查找默认焦点元素
            if (panel is UIPanel up)
            {
                var defaultSelectable = up.GetDefaultSelectable();
                if (defaultSelectable != default)
                {
                    SetFocus(defaultSelectable);
                    return;
                }
            }

            // 查找第一个可交互元素
            var firstSelectable = FindFirstSelectable(panel.Transform);
            if (firstSelectable != default) SetFocus(firstSelectable);
        }

        /// <summary>
        /// 面板隐藏时处理焦点
        /// </summary>
        internal void OnPanelHideFocus(IPanel panel)
        {
            if (panel == default) return;

            var currentFocus = CurrentFocus;
            if (currentFocus != default && IsChildOf(currentFocus.transform, panel.Transform))
            {
                mPanelFocusMemory[panel] = currentFocus;
                ClearFocus();
            }
        }

        /// <summary>
        /// 面板关闭时清理焦点记忆
        /// </summary>
        internal void OnPanelCloseFocus(IPanel panel)
        {
            if (panel == default) return;
            mPanelFocusMemory.Remove(panel);
        }

        #endregion

        #region 手柄控制

        private void EnableGamepad()
        {
            if (mNavigator != default)
            {
                mNavigator.Enable();
                return;
            }
            InitializeGamepad();
        }

        private void DisableGamepad()
        {
            if (mNavigator != default) mNavigator.Disable();
        }

        private void UpdateFocusSystem()
        {
            if (!mConfig.EnableFocusSystem) return;
            UpdateNavigator();
            TrackFocusChange();
        }

        private void LateUpdateFocusSystem()
        {
            if (mNavigator != default) mNavigator.LateUpdate();
        }

        private void UpdateNavigator()
        {
            if (!mConfig.EnableGamepad || mNavigator == default) return;
            mNavigator.Update(Time.unscaledDeltaTime);
        }

        private void TrackFocusChange()
        {
            var current = CurrentFocus;
            if (current != mLastFocusedObject)
            {
                mLastFocusedObject = current;
            }
        }

        #endregion

        #region 辅助方法

        private Selectable FindFirstSelectable(Transform root)
        {
            if (root == default) return null;

            var grid = root.GetComponentInChildren<UINavigationGrid>();
            if (grid != default)
            {
                var first = grid.GetFirstSelectable();
                if (first != default) return first;
            }

            var group = root.GetComponentInChildren<SelectableGroup>();
            if (group != default)
            {
                var first = group.GetFirstSelectable();
                if (first != default) return first;
            }

            var selectables = root.GetComponentsInChildren<Selectable>(false);
            for (int i = 0; i < selectables.Length; i++)
            {
                if (selectables[i].interactable && selectables[i].gameObject.activeInHierarchy)
                {
                    return selectables[i];
                }
            }
            return null;
        }

        private static bool IsChildOf(Transform child, Transform parent)
        {
            if (child == default || parent == default) return false;
            var current = child;
            while (current != default)
            {
                if (current == parent) return true;
                current = current.parent;
            }
            return false;
        }

        #endregion
    }
}
