#if ENABLE_INPUT_SYSTEM
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace YokiFrame
{
    /// <summary>
    /// 手柄输入处理器 - 基于 Input System 实现
    /// </summary>
    public sealed class GamepadInputHandler : IGamepadInput, IDisposable
    {
        #region 输入动作

        private readonly InputAction mNavigateAction;
        private readonly InputAction mSubmitAction;
        private readonly InputAction mCancelAction;
        private readonly InputAction mTabLeftAction;
        private readonly InputAction mTabRightAction;
        private readonly InputAction mTriggerLeftAction;
        private readonly InputAction mTriggerRightAction;
        private readonly InputAction mMenuAction;
        private readonly InputAction mMousePositionAction;
        private readonly InputAction mMouseClickAction;

        #endregion

        #region 状态缓存

        private Vector2 mCachedNavigationAxis;
        private Vector2 mCachedMouseDelta;
        private Vector2 mLastMousePosition;
        private bool mSubmitPressed;
        private bool mCancelPressed;
        private bool mTabLeftPressed;
        private bool mTabRightPressed;
        private bool mTriggerLeftPressed;
        private bool mTriggerRightPressed;
        private bool mMenuPressed;
        private bool mMouseLeftPressed;
        private bool mIsEnabled;

        #endregion

        #region IGamepadInput 实现

        public Vector2 NavigationAxis => mCachedNavigationAxis;
        public bool SubmitPressed => mSubmitPressed;
        public bool CancelPressed => mCancelPressed;
        public bool TabLeftPressed => mTabLeftPressed;
        public bool TabRightPressed => mTabRightPressed;
        public bool TriggerLeftPressed => mTriggerLeftPressed;
        public bool TriggerRightPressed => mTriggerRightPressed;
        public bool MenuPressed => mMenuPressed;
        public Vector2 MouseDelta => mCachedMouseDelta;
        public bool MouseLeftPressed => mMouseLeftPressed;

        public bool IsGamepadConnected
        {
            get
            {
                var gamepads = Gamepad.all;
                return gamepads.Count > 0;
            }
        }

        #endregion

        #region 构造与销毁

        public GamepadInputHandler()
        {
            // 创建导航动作（支持手柄和键盘）
            mNavigateAction = new InputAction("Navigate", InputActionType.Value);
            mNavigateAction.AddCompositeBinding("2DVector")
                .With("Up", "<Gamepad>/leftStick/up")
                .With("Down", "<Gamepad>/leftStick/down")
                .With("Left", "<Gamepad>/leftStick/left")
                .With("Right", "<Gamepad>/leftStick/right");
            mNavigateAction.AddCompositeBinding("2DVector")
                .With("Up", "<Gamepad>/dpad/up")
                .With("Down", "<Gamepad>/dpad/down")
                .With("Left", "<Gamepad>/dpad/left")
                .With("Right", "<Gamepad>/dpad/right");
            mNavigateAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            mNavigateAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            // 确认键
            mSubmitAction = new InputAction("Submit", InputActionType.Button);
            mSubmitAction.AddBinding("<Gamepad>/buttonSouth");
            mSubmitAction.AddBinding("<Keyboard>/enter");
            mSubmitAction.AddBinding("<Keyboard>/space");

            // 取消键
            mCancelAction = new InputAction("Cancel", InputActionType.Button);
            mCancelAction.AddBinding("<Gamepad>/buttonEast");
            mCancelAction.AddBinding("<Keyboard>/escape");

            // 左肩键
            mTabLeftAction = new InputAction("TabLeft", InputActionType.Button);
            mTabLeftAction.AddBinding("<Gamepad>/leftShoulder");
            mTabLeftAction.AddBinding("<Keyboard>/q");

            // 右肩键
            mTabRightAction = new InputAction("TabRight", InputActionType.Button);
            mTabRightAction.AddBinding("<Gamepad>/rightShoulder");
            mTabRightAction.AddBinding("<Keyboard>/e");

            // 左扳机
            mTriggerLeftAction = new InputAction("TriggerLeft", InputActionType.Button);
            mTriggerLeftAction.AddBinding("<Gamepad>/leftTrigger");

            // 右扳机
            mTriggerRightAction = new InputAction("TriggerRight", InputActionType.Button);
            mTriggerRightAction.AddBinding("<Gamepad>/rightTrigger");

            // 菜单键
            mMenuAction = new InputAction("Menu", InputActionType.Button);
            mMenuAction.AddBinding("<Gamepad>/start");
            mMenuAction.AddBinding("<Keyboard>/tab");

            // 鼠标位置
            mMousePositionAction = new InputAction("MousePosition", InputActionType.Value);
            mMousePositionAction.AddBinding("<Mouse>/position");

            // 鼠标点击
            mMouseClickAction = new InputAction("MouseClick", InputActionType.Button);
            mMouseClickAction.AddBinding("<Mouse>/leftButton");

            BindCallbacks();
        }

        public void Dispose()
        {
            Disable();
            UnbindCallbacks();

            mNavigateAction?.Dispose();
            mSubmitAction?.Dispose();
            mCancelAction?.Dispose();
            mTabLeftAction?.Dispose();
            mTabRightAction?.Dispose();
            mTriggerLeftAction?.Dispose();
            mTriggerRightAction?.Dispose();
            mMenuAction?.Dispose();
            mMousePositionAction?.Dispose();
            mMouseClickAction?.Dispose();
        }

        #endregion

        #region 启用/禁用

        public void Enable()
        {
            if (mIsEnabled) return;
            mIsEnabled = true;

            mNavigateAction.Enable();
            mSubmitAction.Enable();
            mCancelAction.Enable();
            mTabLeftAction.Enable();
            mTabRightAction.Enable();
            mTriggerLeftAction.Enable();
            mTriggerRightAction.Enable();
            mMenuAction.Enable();
            mMousePositionAction.Enable();
            mMouseClickAction.Enable();

            // 初始化鼠标位置
            mLastMousePosition = mMousePositionAction.ReadValue<Vector2>();
        }

        public void Disable()
        {
            if (!mIsEnabled) return;
            mIsEnabled = false;

            mNavigateAction.Disable();
            mSubmitAction.Disable();
            mCancelAction.Disable();
            mTabLeftAction.Disable();
            mTabRightAction.Disable();
            mTriggerLeftAction.Disable();
            mTriggerRightAction.Disable();
            mMenuAction.Disable();
            mMousePositionAction.Disable();
            mMouseClickAction.Disable();
        }

        #endregion

        #region 回调绑定

        private void BindCallbacks()
        {
            mNavigateAction.performed += OnNavigate;
            mNavigateAction.canceled += OnNavigateCanceled;

            mSubmitAction.performed += _ => mSubmitPressed = true;
            mSubmitAction.canceled += _ => mSubmitPressed = false;

            mCancelAction.performed += _ => mCancelPressed = true;
            mCancelAction.canceled += _ => mCancelPressed = false;

            mTabLeftAction.performed += _ => mTabLeftPressed = true;
            mTabLeftAction.canceled += _ => mTabLeftPressed = false;

            mTabRightAction.performed += _ => mTabRightPressed = true;
            mTabRightAction.canceled += _ => mTabRightPressed = false;

            mTriggerLeftAction.performed += _ => mTriggerLeftPressed = true;
            mTriggerLeftAction.canceled += _ => mTriggerLeftPressed = false;

            mTriggerRightAction.performed += _ => mTriggerRightPressed = true;
            mTriggerRightAction.canceled += _ => mTriggerRightPressed = false;

            mMenuAction.performed += _ => mMenuPressed = true;
            mMenuAction.canceled += _ => mMenuPressed = false;

            mMousePositionAction.performed += OnMouseMove;
            mMouseClickAction.performed += _ => mMouseLeftPressed = true;
            mMouseClickAction.canceled += _ => mMouseLeftPressed = false;
        }

        private void UnbindCallbacks()
        {
            mNavigateAction.performed -= OnNavigate;
            mNavigateAction.canceled -= OnNavigateCanceled;
            mMousePositionAction.performed -= OnMouseMove;
        }

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            mCachedNavigationAxis = ctx.ReadValue<Vector2>();
        }

        private void OnNavigateCanceled(InputAction.CallbackContext ctx)
        {
            mCachedNavigationAxis = Vector2.zero;
        }

        private void OnMouseMove(InputAction.CallbackContext ctx)
        {
            var currentPos = ctx.ReadValue<Vector2>();
            mCachedMouseDelta = currentPos - mLastMousePosition;
            mLastMousePosition = currentPos;
        }

        #endregion

        #region 帧更新

        /// <summary>
        /// 每帧结束时重置单帧状态（由 GamepadNavigator 调用）
        /// </summary>
        public void LateUpdate()
        {
            // 鼠标增量每帧重置
            mCachedMouseDelta = Vector2.zero;
        }

        #endregion
    }
}
#endif
