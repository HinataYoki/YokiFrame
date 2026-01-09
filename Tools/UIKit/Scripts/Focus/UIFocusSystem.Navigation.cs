using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UIFocusSystem - 导航和输入模式检测
    /// </summary>
    public partial class UIFocusSystem
    {
        #region 输入模式检测

        private void DetectInputModeChange()
        {
#if ENABLE_INPUT_SYSTEM
            if (mInputHandler == null) return;

            var newMode = mCurrentInputMode;
            var config = GamepadConfig;

            // 检测鼠标移动
            var mouseDelta = mInputHandler.MouseDelta;
            if (mouseDelta.sqrMagnitude > config.MouseMoveThreshold * config.MouseMoveThreshold ||
                mInputHandler.MouseLeftPressed)
            {
                newMode = UIInputMode.Pointer;
            }
            // 检测导航输入
            else if (mInputHandler.NavigationAxis.sqrMagnitude > config.NavigationDeadzone * config.NavigationDeadzone ||
                     mInputHandler.SubmitPressed ||
                     mInputHandler.CancelPressed ||
                     mInputHandler.TabLeftPressed ||
                     mInputHandler.TabRightPressed)
            {
                newMode = UIInputMode.Navigation;
            }

            if (newMode != mCurrentInputMode)
            {
                SwitchInputMode(newMode);
            }
#endif
        }

        private void SwitchInputMode(UIInputMode newMode)
        {
            var oldMode = mCurrentInputMode;
            mCurrentInputMode = newMode;

            // 处理光标显示
            var config = GamepadConfig;
            if (config.HideCursorOnGamepad)
            {
                Cursor.visible = newMode == UIInputMode.Pointer;
            }

            // 切换到导航模式时，确保有焦点
            if (newMode == UIInputMode.Navigation && CurrentFocus == null)
            {
                RestoreLastFocus();
            }

            // 更新焦点高亮显示
            if (mFocusHighlight != null)
            {
                if (newMode == UIInputMode.Navigation)
                {
                    mFocusHighlight.SetTarget(CurrentFocus);
                }
                else
                {
                    mFocusHighlight.Hide();
                }
            }

            EventKit.Type.Send(new InputModeChangedEvent
            {
                Previous = oldMode,
                Current = newMode
            });
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

        private void UpdateFocusHighlight()
        {
            if (mFocusHighlight == null) return;

            // 只在导航模式下显示高亮
            if (mCurrentInputMode == UIInputMode.Navigation)
            {
                mFocusHighlight.SetTarget(CurrentFocus);
            }
        }

        #endregion

        #region 事件处理

        private void HandleCancel()
        {
            // 获取当前栈顶面板
            var topPanel = UIStackManager.Peek();
            if (topPanel != null)
            {
                EventKit.Type.Send(new GamepadCancelEvent { CurrentPanel = topPanel });

                // 默认行为：弹出栈顶面板
                UIKit.PopPanel();
            }
        }

        private void HandleTabSwitch(int direction)
        {
            // 查找当前面板中的 TabGroup
            var currentFocus = CurrentFocus;
            if (currentFocus == null) return;

            var panel = FindPanelForObject(currentFocus);
            if (panel == null) return;

            var tabGroup = panel.Transform.GetComponentInChildren<UITabGroup>();
            if (tabGroup != null)
            {
                tabGroup.SwitchTab(direction);

                // 切换后聚焦到新 Tab 的第一个元素
                var firstSelectable = tabGroup.GetFirstSelectableInCurrentTab();
                if (firstSelectable != null)
                {
                    SetFocus(firstSelectable);
                }
            }
        }

        private void HandleMenu()
        {
            // 可以在这里处理菜单键，或者让用户自行订阅事件
            // 默认不做处理
        }

        #endregion
    }
}
