#if UNITY_2022_3_OR_NEWER
using UnityEngine;

namespace YokiFrame
{
    /// <summary>共享导航死区、方向切换和长按重复规则。</summary>
    internal sealed class UINavigationRepeatState
    {
        private MoveDirection mLastDirection;
        private float mHeldDuration;
        private float mNextRepeatTime;
        private bool mIsNavigating;

        /// <summary>推进一次轴输入；只在首次、变向或达到重复时间时返回移动方向。</summary>
        internal bool TryGetMove(
            Vector2 axis,
            float deltaTime,
            GamepadConfig config,
            out MoveDirection direction)
        {
            GamepadConfig resolvedConfig = config != default ? config : GamepadConfig.Default;
            if (!TryResolveDirection(axis, resolvedConfig, out direction))
            {
                Reset();
                return false;
            }

            if (!mIsNavigating || direction != mLastDirection)
            {
                mIsNavigating = true;
                mLastDirection = direction;
                mHeldDuration = 0f;
                mNextRepeatTime = Mathf.Max(0f, resolvedConfig.NavigationRepeatDelay);
                return true;
            }

            mHeldDuration += Mathf.Max(0f, deltaTime);
            if (mHeldDuration < mNextRepeatTime) return false;
            mNextRepeatTime += Mathf.Max(0.01f, resolvedConfig.NavigationRepeatRate);
            return true;
        }

        /// <summary>清除当前保持方向和重复计时。</summary>
        internal void Reset()
        {
            mIsNavigating = false;
            mHeldDuration = 0f;
            mNextRepeatTime = 0f;
        }

        /// <summary>把有效二维轴解析为一个确定方向。</summary>
        private static bool TryResolveDirection(
            Vector2 axis,
            GamepadConfig config,
            out MoveDirection direction)
        {
            if (!IsFinite(axis.x) || !IsFinite(axis.y))
            {
                direction = default;
                return false;
            }
            float deadzone = config.NavigationDeadzone;
            float horizontal = Mathf.Abs(axis.x);
            float vertical = Mathf.Abs(axis.y);
            if (horizontal <= deadzone && vertical <= deadzone)
            {
                direction = default;
                return false;
            }
            if (config.AllowDiagonalNavigation && horizontal > deadzone)
                direction = axis.x >= 0f ? MoveDirection.Right : MoveDirection.Left;
            else if (horizontal >= vertical)
                direction = axis.x >= 0f ? MoveDirection.Right : MoveDirection.Left;
            else
                direction = axis.y >= 0f ? MoveDirection.Up : MoveDirection.Down;
            return true;
        }

        /// <summary>拒绝 NaN 与 Infinity，避免输入状态永久卡在重复阶段。</summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
#endif
