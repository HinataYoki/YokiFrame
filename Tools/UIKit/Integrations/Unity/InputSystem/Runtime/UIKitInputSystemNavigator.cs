#if UNITY_2022_3_OR_NEWER && YOKIFRAME_INPUTSYSTEM_SUPPORT
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>把项目 Input Action 映射为带死区、重复和输入模式检测的 UIKit 导航。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("YokiFrame/UIKit/Input System Navigator")]
    public sealed class UIKitInputSystemNavigator : MonoBehaviour
    {
        [SerializeField] private GamepadConfig mConfig;
        [SerializeField] private InputActionReference mNavigate;
        [SerializeField] private InputActionReference mSubmit;
        [SerializeField] private InputActionReference mCancel;
        [SerializeField] private InputActionReference mPreviousTab;
        [SerializeField] private InputActionReference mNextTab;

        private readonly UINavigationRepeatState mNavigationState = new();
        private bool mNavigateEnabledByComponent;
        private bool mSubmitEnabledByComponent;
        private bool mCancelEnabledByComponent;
        private bool mPreviousTabEnabledByComponent;
        private bool mNextTabEnabledByComponent;

        /// <summary>获取当前组件使用的导航参数；未配置时使用运行时默认值。</summary>
        public GamepadConfig Config => mConfig != default ? mConfig : GamepadConfig.Default;

        /// <summary>启用并订阅所有已配置 Input Action，同时保留外部启用状态。</summary>
        private void OnEnable()
        {
            EnableAction(mNavigate, ref mNavigateEnabledByComponent);
            Subscribe(mSubmit, OnSubmit, ref mSubmitEnabledByComponent);
            Subscribe(mCancel, OnCancel, ref mCancelEnabledByComponent);
            Subscribe(mPreviousTab, OnPreviousTab, ref mPreviousTabEnabledByComponent);
            Subscribe(mNextTab, OnNextTab, ref mNextTabEnabledByComponent);
        }

        /// <summary>取消订阅并只禁用由当前组件主动启用的 Input Action。</summary>
        private void OnDisable()
        {
            Unsubscribe(mSubmit, OnSubmit, mSubmitEnabledByComponent);
            Unsubscribe(mCancel, OnCancel, mCancelEnabledByComponent);
            Unsubscribe(mPreviousTab, OnPreviousTab, mPreviousTabEnabledByComponent);
            Unsubscribe(mNextTab, OnNextTab, mNextTabEnabledByComponent);
            DisableAction(mNavigate, mNavigateEnabledByComponent);
            ResetNavigationState();
        }

        /// <summary>每帧检测指针模式，并推进导航死区和长按重复状态。</summary>
        private void Update()
        {
            ProcessPointerMode();
            ProcessNavigation(Time.unscaledDeltaTime);
        }

        /// <summary>读取导航轴，并按首次输入、方向切换和持续重复触发移动。</summary>
        private void ProcessNavigation(float deltaTime)
        {
            InputAction action = GetAction(mNavigate);
            if (action == null)
            {
                ResetNavigationState();
                return;
            }

            if (!mNavigationState.TryGetMove(
                action.ReadValue<Vector2>(),
                deltaTime,
                Config,
                out MoveDirection direction)) return;
            SetNavigationMode();
            ExecuteNavigation(direction);
        }

        /// <summary>寻找当前 Selectable 的相邻目标；无焦点时先恢复面板焦点。</summary>
        private static void ExecuteNavigation(MoveDirection direction)
        {
            GameObject current = UIKit.CurrentFocus;
            if (current == default)
            {
                UIKit.RestoreLastFocus();
                return;
            }

            Selectable selectable = current.GetComponent<Selectable>();
            if (selectable == default) return;
            Selectable target = FindTarget(selectable, direction);
            if (target != default && target.interactable) UIKit.SetFocus(target);
        }

        /// <summary>检测指针位移或按下，并切换回 Pointer 模式。</summary>
        private void ProcessPointerMode()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null) return;
            Vector2 delta = pointer.delta.ReadValue();
            float threshold = Mathf.Max(0f, Config.MouseMoveThreshold);
            bool moved = delta.sqrMagnitude > 0f && delta.sqrMagnitude >= threshold * threshold;
            if (moved || pointer.press.wasPressedThisFrame) SetPointerMode();
        }

        /// <summary>向当前焦点发送 Unity UI Submit 事件。</summary>
        private void OnSubmit(InputAction.CallbackContext context)
        {
            SetNavigationMode();
            GameObject current = UIKit.CurrentFocus;
            if (current != default)
                ExecuteEvents.Execute(
                    current,
                    new BaseEventData(UIKit.EnsureEventSystem()),
                    ExecuteEvents.submitHandler);
        }

        /// <summary>执行当前焦点或顶部面板上的 UIBackHandler。</summary>
        private void OnCancel(InputAction.CallbackContext context)
        {
            SetNavigationMode();
            GameObject current = UIKit.CurrentFocus;
            UIBackHandler handler = current != default
                ? current.GetComponentInParent<UIBackHandler>()
                : null;
            if (handler == default)
            {
                IPanel top = UIKit.GetGlobalTopPanel();
                handler = top != null ? top.Transform.GetComponent<UIBackHandler>() : null;
            }
            if (handler != default) handler.ExecuteBack();
            else UIKit.PopPanel();
        }

        /// <summary>切换当前焦点所属 TabGroup 的上一个 Tab。</summary>
        private void OnPreviousTab(InputAction.CallbackContext context)
        {
            SetNavigationMode();
            UITabGroup group = FindCurrentTabGroup();
            if (group != default) group.PreviousTab();
        }

        /// <summary>切换当前焦点所属 TabGroup 的下一个 Tab。</summary>
        private void OnNextTab(InputAction.CallbackContext context)
        {
            SetNavigationMode();
            UITabGroup group = FindCurrentTabGroup();
            if (group != default) group.NextTab();
        }

        /// <summary>按方向读取 Selectable 的 Unity Navigation 目标。</summary>
        private static Selectable FindTarget(Selectable selectable, MoveDirection direction)
        {
            switch (direction)
            {
                case MoveDirection.Left: return selectable.FindSelectableOnLeft();
                case MoveDirection.Right: return selectable.FindSelectableOnRight();
                case MoveDirection.Up: return selectable.FindSelectableOnUp();
                case MoveDirection.Down: return selectable.FindSelectableOnDown();
                default: return null;
            }
        }

        /// <summary>切换到 Navigation 并按配置更新光标可见性。</summary>
        private void SetNavigationMode()
        {
            UIKit.SetInputMode(UIInputMode.Navigation);
            if (Config.HideCursorOnGamepad) Cursor.visible = false;
        }

        /// <summary>切换到 Pointer 并恢复光标可见性。</summary>
        private static void SetPointerMode()
        {
            if (UIKit.InputMode != UIInputMode.Pointer) UIKit.SetInputMode(UIInputMode.Pointer);
            Cursor.visible = true;
        }

        /// <summary>清除导航保持与重复计时。</summary>
        private void ResetNavigationState()
        {
            mNavigationState.Reset();
        }

        /// <summary>获取引用中的 InputAction；未配置时返回空。</summary>
        private static InputAction GetAction(InputActionReference reference)
        {
            return reference != default ? reference.action : null;
        }

        /// <summary>订阅并按需启用按钮 Action，记录当前组件是否拥有启用操作。</summary>
        private static void Subscribe(
            InputActionReference reference,
            System.Action<InputAction.CallbackContext> callback,
            ref bool enabledByComponent)
        {
            InputAction action = GetAction(reference);
            if (action == null) return;
            action.performed += callback;
            EnableAction(reference, ref enabledByComponent);
        }

        /// <summary>取消订阅，并仅在当前组件曾启用时禁用 Action。</summary>
        private static void Unsubscribe(
            InputActionReference reference,
            System.Action<InputAction.CallbackContext> callback,
            bool enabledByComponent)
        {
            InputAction action = GetAction(reference);
            if (action == null) return;
            action.performed -= callback;
            if (enabledByComponent) action.Disable();
        }

        /// <summary>确保 Action 可读取，并记录是否由当前组件从禁用状态启用。</summary>
        private static void EnableAction(InputActionReference reference, ref bool enabledByComponent)
        {
            InputAction action = GetAction(reference);
            enabledByComponent = action != null && !action.enabled;
            if (enabledByComponent) action.Enable();
        }

        /// <summary>只撤销当前组件拥有的 Action 启用操作。</summary>
        private static void DisableAction(InputActionReference reference, bool enabledByComponent)
        {
            InputAction action = GetAction(reference);
            if (action != null && enabledByComponent) action.Disable();
        }

        /// <summary>从当前焦点向父级查找 TabGroup。</summary>
        private static UITabGroup FindCurrentTabGroup()
        {
            GameObject current = UIKit.CurrentFocus;
            return current != default ? current.GetComponentInParent<UITabGroup>() : null;
        }
    }
}
#endif
