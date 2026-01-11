using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 手势识别器
    /// 支持滑动、捏合、旋转等常见手势
    /// </summary>
    public class GestureRecognizer : MonoBehaviour
    {
        #region 序列化字段

        [Header("滑动配置")]
        [SerializeField] private float mSwipeThreshold = 50f;
        [SerializeField] private float mSwipeTimeLimit = 0.5f;

        [Header("捏合配置")]
        [SerializeField] private float mPinchThreshold = 0.1f;

        [Header("旋转配置")]
        [SerializeField] private float mRotationThreshold = 5f;

        [Header("点击配置")]
        [SerializeField] private float mTapTimeLimit = 0.3f;
        [SerializeField] private float mDoubleTapInterval = 0.3f;

        #endregion

        #region 私有字段

        // 单指状态
        private Vector2 mTouchStartPos;
        private float mTouchStartTime;
        private bool mIsTouching;

        // 双指状态
        private float mInitialPinchDistance;
        private float mInitialRotationAngle;
        private bool mIsPinching;

        // 点击检测
        private float mLastTapTime;
        private int mTapCount;

        #endregion

        #region 事件

        /// <summary>滑动事件（方向）</summary>
        public event Action<SwipeDirection> OnSwipe;

        /// <summary>捏合事件（缩放比例）</summary>
        public event Action<float> OnPinch;

        /// <summary>旋转事件（角度变化）</summary>
        public event Action<float> OnRotate;

        /// <summary>单击事件（位置）</summary>
        public event Action<Vector2> OnTap;

        /// <summary>双击事件（位置）</summary>
        public event Action<Vector2> OnDoubleTap;

        /// <summary>长按事件（位置）</summary>
        public event Action<Vector2> OnLongPress;

        #endregion

        #region 属性

        /// <summary>滑动阈值（像素）</summary>
        public float SwipeThreshold
        {
            get => mSwipeThreshold;
            set => mSwipeThreshold = Mathf.Max(10f, value);
        }

        /// <summary>是否正在触摸</summary>
        public bool IsTouching => mIsTouching;

        /// <summary>是否正在捏合</summary>
        public bool IsPinching => mIsPinching;

        #endregion

        #region 生命周期

        private void Update()
        {
            int touchCount = Input.touchCount;

            if (touchCount == 1)
            {
                ProcessSingleTouch(Input.GetTouch(0));
            }
            else if (touchCount >= 2)
            {
                ProcessMultiTouch(Input.GetTouch(0), Input.GetTouch(1));
            }
            else
            {
                ResetState();
            }
        }

        #endregion

        #region 单指处理

        private void ProcessSingleTouch(Touch touch)
        {
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    mTouchStartPos = touch.position;
                    mTouchStartTime = Time.unscaledTime;
                    mIsTouching = true;
                    break;

                case TouchPhase.Ended:
                    if (mIsTouching)
                    {
                        ProcessTouchEnd(touch);
                    }
                    mIsTouching = false;
                    break;

                case TouchPhase.Canceled:
                    mIsTouching = false;
                    break;
            }
        }

        private void ProcessTouchEnd(Touch touch)
        {
            float duration = Time.unscaledTime - mTouchStartTime;
            Vector2 delta = touch.position - mTouchStartPos;
            float distance = delta.magnitude;

            // 滑动检测
            if (distance >= mSwipeThreshold && duration <= mSwipeTimeLimit)
            {
                SwipeDirection direction = GetSwipeDirection(delta);
                OnSwipe?.Invoke(direction);
                return;
            }

            // 点击检测
            if (duration <= mTapTimeLimit && distance < mSwipeThreshold)
            {
                ProcessTap(touch.position);
            }
        }

        private void ProcessTap(Vector2 position)
        {
            float timeSinceLastTap = Time.unscaledTime - mLastTapTime;

            if (timeSinceLastTap <= mDoubleTapInterval)
            {
                mTapCount++;
                if (mTapCount >= 2)
                {
                    OnDoubleTap?.Invoke(position);
                    mTapCount = 0;
                }
            }
            else
            {
                mTapCount = 1;
                OnTap?.Invoke(position);
            }

            mLastTapTime = Time.unscaledTime;
        }

        private SwipeDirection GetSwipeDirection(Vector2 delta)
        {
            float absX = Mathf.Abs(delta.x);
            float absY = Mathf.Abs(delta.y);

            if (absX > absY)
            {
                return delta.x > 0 ? SwipeDirection.Right : SwipeDirection.Left;
            }
            return delta.y > 0 ? SwipeDirection.Up : SwipeDirection.Down;
        }

        #endregion

        #region 双指处理

        private void ProcessMultiTouch(Touch touch0, Touch touch1)
        {
            if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
            {
                InitializeMultiTouch(touch0, touch1);
                return;
            }

            if (!mIsPinching) return;

            // 捏合检测
            float currentDistance = Vector2.Distance(touch0.position, touch1.position);
            float pinchDelta = (currentDistance - mInitialPinchDistance) / mInitialPinchDistance;

            if (Mathf.Abs(pinchDelta) >= mPinchThreshold)
            {
                OnPinch?.Invoke(1f + pinchDelta);
                mInitialPinchDistance = currentDistance;
            }

            // 旋转检测
            float currentAngle = GetAngleBetweenTouches(touch0, touch1);
            float rotationDelta = Mathf.DeltaAngle(mInitialRotationAngle, currentAngle);

            if (Mathf.Abs(rotationDelta) >= mRotationThreshold)
            {
                OnRotate?.Invoke(rotationDelta);
                mInitialRotationAngle = currentAngle;
            }
        }

        private void InitializeMultiTouch(Touch touch0, Touch touch1)
        {
            mIsPinching = true;
            mIsTouching = false;
            mInitialPinchDistance = Vector2.Distance(touch0.position, touch1.position);
            mInitialRotationAngle = GetAngleBetweenTouches(touch0, touch1);
        }

        private float GetAngleBetweenTouches(Touch touch0, Touch touch1)
        {
            Vector2 delta = touch1.position - touch0.position;
            return Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        }

        #endregion

        #region 内部方法

        private void ResetState()
        {
            mIsTouching = false;
            mIsPinching = false;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 重置识别器状态
        /// </summary>
        public void ResetRecognizer()
        {
            ResetState();
            mTapCount = 0;
            mLastTapTime = 0f;
        }

        #endregion
    }

    /// <summary>
    /// 滑动方向
    /// </summary>
    public enum SwipeDirection
    {
        Up,
        Down,
        Left,
        Right
    }
}
