using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// 虚拟按钮组件
    /// 支持按下/抬起/长按事件
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class VirtualButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        #region 序列化字段

        [Header("配置")]
        [SerializeField] private string mButtonName = "Action";
        [SerializeField] private float mLongPressThreshold = 0.5f;
        [SerializeField] private bool mRepeatOnHold;
        [SerializeField] private float mRepeatInterval = 0.1f;

        [Header("视觉反馈")]
        [SerializeField] private Graphic mTargetGraphic;
        [SerializeField] private Color mNormalColor = Color.white;
        [SerializeField] private Color mPressedColor = new(0.8f, 0.8f, 0.8f, 1f);

        #endregion

        #region 私有字段

        private bool mIsPressed;
        private float mPressStartTime;
        private float mLastRepeatTime;
        private bool mLongPressTriggered;
        private int mPointerId = -1;

        #endregion

        #region 事件

        /// <summary>按下事件</summary>
        public event Action OnPressed;

        /// <summary>抬起事件</summary>
        public event Action OnReleased;

        /// <summary>长按事件</summary>
        public event Action OnLongPress;

        /// <summary>重复触发事件（按住时）</summary>
        public event Action OnRepeat;

        #endregion

        #region 属性

        /// <summary>按钮名称</summary>
        public string ButtonName => mButtonName;

        /// <summary>是否按下</summary>
        public bool IsPressed => mIsPressed;

        /// <summary>按下持续时间</summary>
        public float HoldDuration => mIsPressed ? Time.unscaledTime - mPressStartTime : 0f;

        /// <summary>长按阈值</summary>
        public float LongPressThreshold
        {
            get => mLongPressThreshold;
            set => mLongPressThreshold = Mathf.Max(0.1f, value);
        }

        #endregion

        #region 生命周期

        private void Update()
        {
            if (!mIsPressed) return;

            float holdTime = Time.unscaledTime - mPressStartTime;

            // 长按检测
            if (!mLongPressTriggered && holdTime >= mLongPressThreshold)
            {
                mLongPressTriggered = true;
                OnLongPress?.Invoke();
            }

            // 重复触发
            if (mRepeatOnHold && holdTime >= mLongPressThreshold)
            {
                if (Time.unscaledTime - mLastRepeatTime >= mRepeatInterval)
                {
                    mLastRepeatTime = Time.unscaledTime;
                    OnRepeat?.Invoke();
                }
            }
        }

        #endregion

        #region 事件处理

        public void OnPointerDown(PointerEventData eventData)
        {
            if (mPointerId != -1) return;

            mPointerId = eventData.pointerId;
            mIsPressed = true;
            mPressStartTime = Time.unscaledTime;
            mLastRepeatTime = mPressStartTime;
            mLongPressTriggered = false;

            UpdateVisual(true);
            OnPressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != mPointerId) return;

            mPointerId = -1;
            mIsPressed = false;

            UpdateVisual(false);
            OnReleased?.Invoke();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 模拟按下
        /// </summary>
        public void SimulatePress()
        {
            if (mIsPressed) return;

            mIsPressed = true;
            mPressStartTime = Time.unscaledTime;
            mLastRepeatTime = mPressStartTime;
            mLongPressTriggered = false;

            UpdateVisual(true);
            OnPressed?.Invoke();
        }

        /// <summary>
        /// 模拟抬起
        /// </summary>
        public void SimulateRelease()
        {
            if (!mIsPressed) return;

            mIsPressed = false;
            mPointerId = -1;

            UpdateVisual(false);
            OnReleased?.Invoke();
        }

        /// <summary>
        /// 重置按钮状态
        /// </summary>
        public void ResetButton()
        {
            mIsPressed = false;
            mPointerId = -1;
            mLongPressTriggered = false;
            UpdateVisual(false);
        }

        #endregion

        #region 内部方法

        private void UpdateVisual(bool pressed)
        {
            if (mTargetGraphic == default) return;
            mTargetGraphic.color = pressed ? mPressedColor : mNormalColor;
        }

        #endregion
    }
}
