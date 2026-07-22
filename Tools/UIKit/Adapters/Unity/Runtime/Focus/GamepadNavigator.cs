#if UNITY_2022_3_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>输入系统无关的 UIKit 导航器，处理移动、确认、取消、Tab 和菜单。</summary>
    public sealed class GamepadNavigator : IDisposable
    {
        private readonly UINavigationRepeatState mNavigationState = new();
        private readonly IGamepadInput mInput;
        private readonly GamepadConfig mConfig;
        private readonly EventSystem mEventSystem;
        private bool mSubmitConsumed;
        private bool mCancelConsumed;
        private bool mTabLeftConsumed;
        private bool mTabRightConsumed;
        private bool mMenuConsumed;

        /// <summary>导航方向实际处理后触发。</summary>
        public event Action<MoveDirection> OnNavigate;

        /// <summary>确认键首次按下后触发。</summary>
        public event Action OnSubmit;

        /// <summary>取消键首次按下后触发。</summary>
        public event Action OnCancel;

        /// <summary>Tab 首次按下后触发，参数 -1 表示上一项，1 表示下一项。</summary>
        public event Action<int> OnTabSwitch;

        /// <summary>菜单键首次按下后触发。</summary>
        public event Action OnMenu;

        /// <summary>创建使用指定输入、配置和 EventSystem 的导航器。</summary>
        public GamepadNavigator(
            IGamepadInput input,
            GamepadConfig config,
            EventSystem eventSystem)
        {
            mInput = input ?? throw new ArgumentNullException(nameof(input));
            mConfig = config != default ? config : GamepadConfig.Default;
            mEventSystem = eventSystem;
        }

        /// <summary>获取导航器是否正在采集输入。</summary>
        public bool IsEnabled { get; private set; }

        /// <summary>获取 EventSystem 当前焦点。</summary>
        public GameObject CurrentFocus => mEventSystem != default
            ? mEventSystem.currentSelectedGameObject
            : null;

        /// <summary>启用输入采集；重复调用不重复启用。</summary>
        public void Enable()
        {
            if (IsEnabled) return;
            IsEnabled = true;
            mInput.Enable();
        }

        /// <summary>禁用输入并清除按键消费和导航重复状态。</summary>
        public void Disable()
        {
            if (!IsEnabled) return;
            IsEnabled = false;
            mInput.Disable();
            ResetState();
        }

        /// <summary>推进一帧导航与按钮边沿状态。</summary>
        public void Update(float deltaTime)
        {
            if (!IsEnabled) return;
            if (mNavigationState.TryGetMove(
                mInput.NavigationAxis,
                deltaTime,
                mConfig,
                out MoveDirection direction)) ExecuteNavigation(direction);
            ProcessSubmit();
            ProcessCancel();
            ProcessTabSwitch();
            ProcessMenu();
        }

        /// <summary>保留旧调用时序入口；输入帧清理由具体 IGamepadInput 自己管理。</summary>
        public void LateUpdate() { }

        /// <summary>设置指定有效 GameObject 为 EventSystem 焦点。</summary>
        public void SetFocus(GameObject target)
        {
            if (target == default || mEventSystem == default || !target.activeInHierarchy) return;
            Selectable selectable = target.GetComponent<Selectable>();
            if (selectable != default && !selectable.interactable) return;
            mEventSystem.SetSelectedGameObject(target);
        }

        /// <summary>设置指定可交互 Selectable 为 EventSystem 焦点。</summary>
        public void SetFocus(Selectable selectable)
        {
            if (selectable != default && selectable.interactable) SetFocus(selectable.gameObject);
        }

        /// <summary>清除 EventSystem 当前焦点。</summary>
        public void ClearFocus()
        {
            if (mEventSystem != default) mEventSystem.SetSelectedGameObject(null);
        }

        /// <summary>禁用输入并释放全部事件订阅。</summary>
        public void Dispose()
        {
            Disable();
            OnNavigate = null;
            OnSubmit = null;
            OnCancel = null;
            OnTabSwitch = null;
            OnMenu = null;
        }

        /// <summary>按当前焦点的 Unity Navigation 配置移动并发送导航事件。</summary>
        private void ExecuteNavigation(MoveDirection direction)
        {
            GameObject current = CurrentFocus;
            Selectable selectable = current != default ? current.GetComponent<Selectable>() : null;
            Selectable target = selectable != default ? FindTarget(selectable, direction) : null;
            if (target != default && target.interactable) SetFocus(target);
            Action<MoveDirection> callback = OnNavigate;
            if (callback != null) callback(direction);
        }

        /// <summary>在确认键按下边沿发送 Submit，并通知观察者。</summary>
        private void ProcessSubmit()
        {
            if (!TryConsume(mInput.SubmitPressed, ref mSubmitConsumed)) return;
            GameObject current = CurrentFocus;
            if (current != default && mEventSystem != default)
                ExecuteEvents.Execute(
                    current,
                    new BaseEventData(mEventSystem),
                    ExecuteEvents.submitHandler);
            Action callback = OnSubmit;
            if (callback != null) callback();
        }

        /// <summary>在取消键按下边沿通知观察者。</summary>
        private void ProcessCancel()
        {
            if (!TryConsume(mInput.CancelPressed, ref mCancelConsumed)) return;
            Action callback = OnCancel;
            if (callback != null) callback();
        }

        /// <summary>分别处理上一和下一 Tab 的按下边沿。</summary>
        private void ProcessTabSwitch()
        {
            Action<int> callback = OnTabSwitch;
            if (TryConsume(mInput.TabLeftPressed, ref mTabLeftConsumed) && callback != null)
                callback(-1);
            if (TryConsume(mInput.TabRightPressed, ref mTabRightConsumed) && callback != null)
                callback(1);
        }

        /// <summary>在菜单键按下边沿通知观察者。</summary>
        private void ProcessMenu()
        {
            if (!TryConsume(mInput.MenuPressed, ref mMenuConsumed)) return;
            Action callback = OnMenu;
            if (callback != null) callback();
        }

        /// <summary>将持续按下状态转换为一次按下边沿。</summary>
        private static bool TryConsume(bool pressed, ref bool consumed)
        {
            if (!pressed)
            {
                consumed = false;
                return false;
            }
            if (consumed) return false;
            consumed = true;
            return true;
        }

        /// <summary>清除全部内部输入状态，不修改 EventSystem 当前选择。</summary>
        private void ResetState()
        {
            mNavigationState.Reset();
            mSubmitConsumed = false;
            mCancelConsumed = false;
            mTabLeftConsumed = false;
            mTabRightConsumed = false;
            mMenuConsumed = false;
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
    }
}
#endif
