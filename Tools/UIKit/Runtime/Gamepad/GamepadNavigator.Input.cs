using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// 手柄导航器 - 输入处理、按键处理、焦点控制
    /// </summary>
    public sealed partial class GamepadNavigator
    {
        #region 导航处理

        private void ProcessNavigation(float deltaTime)
        {
            var axis = mInput.NavigationAxis;
            var deadzone = mConfig.NavigationDeadzone;

            // 检测是否有有效输入
            bool hasInput = Mathf.Abs(axis.x) > deadzone || Mathf.Abs(axis.y) > deadzone;

            if (!hasInput)
            {
                // 无输入，重置状态
                mIsNavigating = false;
                mNavigationTimer = 0f;
                return;
            }

            // 确定方向
            var direction = GetDirection(axis, deadzone);
            if (direction == MoveDirection.None) return;

            // 首次输入或方向改变
            if (!mIsNavigating || direction != mLastDirection)
            {
                mIsNavigating = true;
                mLastDirection = direction;
                mNavigationTimer = 0f;
                ExecuteNavigation(direction);
                return;
            }

            // 重复输入
            mNavigationTimer += deltaTime;
            if (mNavigationTimer >= mConfig.NavigationRepeatDelay)
            {
                float repeatTime = mNavigationTimer - mConfig.NavigationRepeatDelay;
                if (repeatTime >= mConfig.NavigationRepeatRate)
                {
                    mNavigationTimer = mConfig.NavigationRepeatDelay;
                    ExecuteNavigation(direction);
                }
            }
        }

        private MoveDirection GetDirection(Vector2 axis, float deadzone)
        {
            // 不允许对角线时，取主方向
            if (!mConfig.AllowDiagonalNavigation)
            {
                if (Mathf.Abs(axis.x) > Mathf.Abs(axis.y))
                {
                    return axis.x > deadzone ? MoveDirection.Right : 
                           axis.x < -deadzone ? MoveDirection.Left : MoveDirection.None;
                }
                return axis.y > deadzone ? MoveDirection.Up : 
                       axis.y < -deadzone ? MoveDirection.Down : MoveDirection.None;
            }

            // 允许对角线时，优先处理主方向
            if (Mathf.Abs(axis.x) > deadzone)
            {
                return axis.x > 0 ? MoveDirection.Right : MoveDirection.Left;
            }
            if (Mathf.Abs(axis.y) > deadzone)
            {
                return axis.y > 0 ? MoveDirection.Up : MoveDirection.Down;
            }
            return MoveDirection.None;
        }

        private void ExecuteNavigation(MoveDirection direction)
        {
            // 使用 EventSystem 的导航
            var current = CurrentFocus;
            if (current == null)
            {
                // 无焦点时，尝试找到第一个可选元素
                OnNavigate?.Invoke(direction);
                return;
            }

            var selectable = current.GetComponent<Selectable>();
            if (selectable == null)
            {
                OnNavigate?.Invoke(direction);
                return;
            }

            // 获取导航目标
            Selectable target = direction switch
            {
                MoveDirection.Up => selectable.FindSelectableOnUp(),
                MoveDirection.Down => selectable.FindSelectableOnDown(),
                MoveDirection.Left => selectable.FindSelectableOnLeft(),
                MoveDirection.Right => selectable.FindSelectableOnRight(),
                _ => null
            };

            if (target != null && target.interactable)
            {
                SetFocus(target.gameObject);
            }

            OnNavigate?.Invoke(direction);
        }

        #endregion

        #region 按键处理

        private void ProcessSubmit()
        {
            if (mInput.SubmitPressed)
            {
                if (!mSubmitConsumed)
                {
                    mSubmitConsumed = true;
                    ExecuteSubmit();
                }
            }
            else
            {
                mSubmitConsumed = false;
            }
        }

        private void ProcessCancel()
        {
            if (mInput.CancelPressed)
            {
                if (!mCancelConsumed)
                {
                    mCancelConsumed = true;
                    OnCancel?.Invoke();
                }
            }
            else
            {
                mCancelConsumed = false;
            }
        }

        private void ProcessTabSwitch()
        {
            // 左 Tab
            if (mInput.TabLeftPressed)
            {
                if (!mTabLeftConsumed)
                {
                    mTabLeftConsumed = true;
                    OnTabSwitch?.Invoke(-1);
                }
            }
            else
            {
                mTabLeftConsumed = false;
            }

            // 右 Tab
            if (mInput.TabRightPressed)
            {
                if (!mTabRightConsumed)
                {
                    mTabRightConsumed = true;
                    OnTabSwitch?.Invoke(1);
                }
            }
            else
            {
                mTabRightConsumed = false;
            }
        }

        private void ProcessMenu()
        {
            if (mInput.MenuPressed)
            {
                if (!mMenuConsumed)
                {
                    mMenuConsumed = true;
                    OnMenu?.Invoke();
                }
            }
            else
            {
                mMenuConsumed = false;
            }
        }

        private void ExecuteSubmit()
        {
            var current = CurrentFocus;
            if (current == null)
            {
                OnSubmit?.Invoke();
                return;
            }

            // 触发 UI 元素的点击
            var pointer = new PointerEventData(mEventSystem)
            {
                button = PointerEventData.InputButton.Left
            };

            ExecuteEvents.Execute(current, pointer, ExecuteEvents.submitHandler);
            OnSubmit?.Invoke();
        }

        #endregion

        #region 焦点控制

        /// <summary>
        /// 设置焦点到指定对象
        /// </summary>
        public void SetFocus(GameObject target)
        {
            if (target == null || mEventSystem == null) return;
            mEventSystem.SetSelectedGameObject(target);
        }

        /// <summary>
        /// 设置焦点到指定 Selectable
        /// </summary>
        public void SetFocus(Selectable selectable)
        {
            if (selectable == null || !selectable.interactable) return;
            SetFocus(selectable.gameObject);
        }

        /// <summary>
        /// 清除焦点
        /// </summary>
        public void ClearFocus()
        {
            if (mEventSystem != default)
            {
                mEventSystem.SetSelectedGameObject(null);
            }
        }

        #endregion
    }
}
