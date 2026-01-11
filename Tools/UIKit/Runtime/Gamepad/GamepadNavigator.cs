using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// 手柄导航器 - 核心导航逻辑，处理方向导航、确认/取消、Tab切换
    /// </summary>
    public sealed partial class GamepadNavigator : IDisposable
    {
        #region 事件

        /// <summary>
        /// 导航事件（方向移动时触发）
        /// </summary>
        public event Action<MoveDirection> OnNavigate;

        /// <summary>
        /// 确认事件
        /// </summary>
        public event Action OnSubmit;

        /// <summary>
        /// 取消事件
        /// </summary>
        public event Action OnCancel;

        /// <summary>
        /// Tab 切换事件（参数：-1 左，1 右）
        /// </summary>
        public event Action<int> OnTabSwitch;

        /// <summary>
        /// 菜单键事件
        /// </summary>
        public event Action OnMenu;

        #endregion

        #region 依赖

        private readonly IGamepadInput mInput;
        private readonly GamepadConfig mConfig;
        private readonly EventSystem mEventSystem;

        #endregion

        #region 状态

        private float mNavigationTimer;
        private bool mIsNavigating;
        private MoveDirection mLastDirection;
        private bool mSubmitConsumed;
        private bool mCancelConsumed;
        private bool mTabLeftConsumed;
        private bool mTabRightConsumed;
        private bool mMenuConsumed;
        private bool mIsEnabled;

        #endregion

        #region 属性

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled => mIsEnabled;

        /// <summary>
        /// 当前焦点对象
        /// </summary>
        public GameObject CurrentFocus => mEventSystem?.currentSelectedGameObject;

        #endregion

        #region 构造与销毁

        public GamepadNavigator(IGamepadInput input, GamepadConfig config, EventSystem eventSystem)
        {
            mInput = input ?? throw new ArgumentNullException(nameof(input));
            mConfig = config ?? GamepadConfig.Default;
            mEventSystem = eventSystem;
        }

        public void Dispose()
        {
            Disable();
            OnNavigate = null;
            OnSubmit = null;
            OnCancel = null;
            OnTabSwitch = null;
            OnMenu = null;
        }

        #endregion

        #region 启用/禁用

        /// <summary>
        /// 启用导航器
        /// </summary>
        public void Enable()
        {
            if (mIsEnabled) return;
            mIsEnabled = true;
            mInput.Enable();
        }

        /// <summary>
        /// 禁用导航器
        /// </summary>
        public void Disable()
        {
            if (!mIsEnabled) return;
            mIsEnabled = false;
            mInput.Disable();
            ResetState();
        }

        private void ResetState()
        {
            mNavigationTimer = 0f;
            mIsNavigating = false;
            mSubmitConsumed = false;
            mCancelConsumed = false;
            mTabLeftConsumed = false;
            mTabRightConsumed = false;
            mMenuConsumed = false;
        }

        #endregion

        #region 更新

        /// <summary>
        /// 每帧更新（由 UIFocusSystem 调用）
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!mIsEnabled) return;

            ProcessNavigation(deltaTime);
            ProcessSubmit();
            ProcessCancel();
            ProcessTabSwitch();
            ProcessMenu();
        }

        /// <summary>
        /// 帧末更新
        /// </summary>
        public void LateUpdate()
        {
#if YOKIFRAME_INPUTSYSTEM_SUPPORT
            if (mInput is GamepadInputHandler handler)
            {
                handler.LateUpdate();
            }
#endif
        }

        #endregion

    }
}
