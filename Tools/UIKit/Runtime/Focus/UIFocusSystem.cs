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
    /// UI 焦点系统 - 管理 UI 焦点和导航，集成手柄支持
    /// </summary>
    public partial class UIFocusSystem : MonoBehaviour
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

        [Header("基础配置")]
        [Tooltip("是否启用自动焦点（面板显示时自动聚焦默认元素）")]
        [SerializeField] private bool mAutoFocusEnabled = true;

        [Tooltip("是否启用手柄支持")]
        [SerializeField] private bool mGamepadEnabled = true;

        [Header("手柄配置")]
        [Tooltip("手柄配置资源")]
        [SerializeField] private GamepadConfig mGamepadConfig;

        [Tooltip("是否显示焦点高亮")]
        [SerializeField] private bool mShowFocusHighlight = true;

        #endregion

        #region 组件

        private EventSystem mEventSystem;
#if YOKIFRAME_INPUTSYSTEM_SUPPORT
        private GamepadInputHandler mInputHandler;
#endif
        private GamepadNavigator mNavigator;
        private UIFocusHighlight mFocusHighlight;

        #endregion

        #region 状态

        private UIInputMode mCurrentInputMode = UIInputMode.Pointer;
        private GameObject mLastFocusedObject;
        private readonly Dictionary<IPanel, GameObject> mPanelFocusMemory = new();

        #endregion

        #region 属性

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

        /// <summary>
        /// 是否启用手柄支持
        /// </summary>
        public bool GamepadEnabled
        {
            get => mGamepadEnabled;
            set
            {
                mGamepadEnabled = value;
                if (value)
                {
                    EnableGamepad();
                }
                else
                {
                    DisableGamepad();
                }
            }
        }

        /// <summary>
        /// 手柄导航器
        /// </summary>
        public GamepadNavigator Navigator => mNavigator;

        /// <summary>
        /// 焦点高亮组件
        /// </summary>
        public UIFocusHighlight FocusHighlight => mFocusHighlight;

        /// <summary>
        /// 手柄配置
        /// </summary>
        public GamepadConfig GamepadConfig => mGamepadConfig ?? GamepadConfig.Default;

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
            InitializeEventSystem();
            InitializeGamepad();
            InitializeFocusHighlight();
        }

        private void Update()
        {
            DetectInputModeChange();
            TrackFocusChange();
            UpdateNavigator();
            UpdateFocusHighlight();
        }

        private void LateUpdate()
        {
            mNavigator?.LateUpdate();
        }

        private void OnDestroy()
        {
            if (sInstance == this)
            {
                sInstance = null;
            }

            mNavigator?.Dispose();
#if YOKIFRAME_INPUTSYSTEM_SUPPORT
            mInputHandler?.Dispose();
#endif
            mPanelFocusMemory.Clear();
        }

        #endregion

        #region 初始化

        private void InitializeEventSystem()
        {
            mEventSystem = EventSystem.current;
            if (mEventSystem == null)
            {
                mEventSystem = FindFirstObjectByType<EventSystem>();
            }
        }

        private void InitializeGamepad()
        {
            if (!mGamepadEnabled) return;

#if YOKIFRAME_INPUTSYSTEM_SUPPORT
            mInputHandler = new GamepadInputHandler();
            mNavigator = new GamepadNavigator(mInputHandler, GamepadConfig, mEventSystem);

            // 绑定事件
            mNavigator.OnCancel += HandleCancel;
            mNavigator.OnTabSwitch += HandleTabSwitch;
            mNavigator.OnMenu += HandleMenu;

            mNavigator.Enable();
#endif
        }

        private void InitializeFocusHighlight()
        {
            if (!mShowFocusHighlight) return;

            // 在 UIRoot 下创建焦点高亮
            var parent = UIRoot.Instance?.transform;
            if (parent != null)
            {
                mFocusHighlight = UIFocusHighlight.Create(parent, GamepadConfig);
                // 确保高亮在最上层
                mFocusHighlight.transform.SetAsLastSibling();
            }
        }

        #endregion

        #region 手柄控制

        private void EnableGamepad()
        {
            if (mNavigator != null)
            {
                mNavigator.Enable();
                return;
            }

            // 首次启用时初始化
            InitializeGamepad();
        }

        private void DisableGamepad()
        {
            mNavigator?.Disable();
        }

        private void UpdateNavigator()
        {
            if (!mGamepadEnabled || mNavigator == null) return;
            mNavigator.Update(Time.unscaledDeltaTime);
        }

        #endregion

        #region 辅助方法

        private Selectable FindFirstSelectable(Transform root)
        {
            if (root == null) return null;

            // 先检查 NavigationGrid
            var grid = root.GetComponentInChildren<UINavigationGrid>();
            if (grid != null)
            {
                var first = grid.GetFirstSelectable();
                if (first != null) return first;
            }

            // 先检查 SelectableGroup
            var group = root.GetComponentInChildren<SelectableGroup>();
            if (group != null)
            {
                var first = group.GetFirstSelectable();
                if (first != null) return first;
            }

            // 使用 GetComponentsInChildren 查找所有 Selectable
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

        private IPanel FindPanelForObject(GameObject obj)
        {
            if (obj == null) return null;

            var current = obj.transform;
            while (current != null)
            {
                var panel = current.GetComponent<IPanel>();
                if (panel != null) return panel;
                current = current.parent;
            }
            return null;
        }

        #endregion
    }
}
