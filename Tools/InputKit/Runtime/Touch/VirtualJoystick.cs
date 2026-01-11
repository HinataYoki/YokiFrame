using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// 虚拟摇杆组件
    /// 支持固定/浮动模式，可配置死区和灵敏度
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        #region 序列化字段

        [Header("组件引用")]
        [SerializeField] private RectTransform mBackground;
        [SerializeField] private RectTransform mHandle;

        [Header("行为配置")]
        [SerializeField] private JoystickMode mMode = JoystickMode.Fixed;
        [SerializeField] private float mHandleRange = 1f;
        [SerializeField] private float mDeadZone = 0.1f;
        [SerializeField] private float mSensitivity = 1f;

        [Header("视觉配置")]
        [SerializeField] private bool mHideOnRelease = true;
        [SerializeField] private float mFadeSpeed = 5f;

        #endregion

        #region 私有字段

        private RectTransform mRectTransform;
        private Canvas mCanvas;
        private Camera mCanvasCamera;
        private Vector2 mInput;
        private Vector2 mStartPosition;
        private int mPointerId = -1;
        private CanvasGroup mCanvasGroup;
        private bool mIsActive;

        #endregion

        #region 属性

        /// <summary>摇杆输入值（-1 到 1）</summary>
        public Vector2 Input => mInput;

        /// <summary>摇杆输入值（应用死区后）</summary>
        public Vector2 InputWithDeadZone
        {
            get
            {
                float magnitude = mInput.magnitude;
                if (magnitude < mDeadZone) return Vector2.zero;
                return mInput.normalized * ((magnitude - mDeadZone) / (1f - mDeadZone));
            }
        }

        /// <summary>水平输入</summary>
        public float Horizontal => InputWithDeadZone.x;

        /// <summary>垂直输入</summary>
        public float Vertical => InputWithDeadZone.y;

        /// <summary>是否正在操作</summary>
        public bool IsActive => mIsActive;

        /// <summary>摇杆模式</summary>
        public JoystickMode Mode
        {
            get => mMode;
            set => mMode = value;
        }

        /// <summary>死区</summary>
        public float DeadZone
        {
            get => mDeadZone;
            set => mDeadZone = Mathf.Clamp01(value);
        }

        /// <summary>灵敏度</summary>
        public float Sensitivity
        {
            get => mSensitivity;
            set => mSensitivity = Mathf.Max(0.1f, value);
        }

        #endregion

        #region 生命周期

        private void Awake()
        {
            mRectTransform = GetComponent<RectTransform>();
            mCanvas = GetComponentInParent<Canvas>();
            mCanvasGroup = GetComponent<CanvasGroup>();

            if (mCanvas != default)
            {
                mCanvasCamera = mCanvas.renderMode == RenderMode.ScreenSpaceCamera 
                    ? mCanvas.worldCamera 
                    : default;
            }

            mStartPosition = mRectTransform.anchoredPosition;

            if (mHideOnRelease && mCanvasGroup != default)
            {
                mCanvasGroup.alpha = 0f;
            }
        }

        private void Update()
        {
            if (mCanvasGroup == default || !mHideOnRelease) return;

            float targetAlpha = mIsActive ? 1f : 0f;
            mCanvasGroup.alpha = Mathf.MoveTowards(mCanvasGroup.alpha, targetAlpha, mFadeSpeed * Time.deltaTime);
        }

        #endregion

        #region 事件处理

        public void OnPointerDown(PointerEventData eventData)
        {
            if (mPointerId != -1) return;

            mPointerId = eventData.pointerId;
            mIsActive = true;

            if (mMode == JoystickMode.Floating)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    mCanvas.transform as RectTransform,
                    eventData.position,
                    mCanvasCamera,
                    out Vector2 localPoint);
                mRectTransform.anchoredPosition = localPoint;
            }

            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != mPointerId) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mBackground,
                eventData.position,
                mCanvasCamera,
                out Vector2 localPoint);

            float radius = mBackground.sizeDelta.x * 0.5f;
            Vector2 normalizedInput = localPoint / radius;

            mInput = normalizedInput.magnitude > 1f 
                ? normalizedInput.normalized 
                : normalizedInput;

            mInput *= mSensitivity;
            mInput = Vector2.ClampMagnitude(mInput, 1f);

            if (mHandle != default)
            {
                mHandle.anchoredPosition = mInput * radius * mHandleRange;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != mPointerId) return;

            mPointerId = -1;
            mIsActive = false;
            mInput = Vector2.zero;

            if (mHandle != default)
            {
                mHandle.anchoredPosition = Vector2.zero;
            }

            if (mMode == JoystickMode.Floating)
            {
                mRectTransform.anchoredPosition = mStartPosition;
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 重置摇杆状态
        /// </summary>
        public void ResetJoystick()
        {
            mPointerId = -1;
            mIsActive = false;
            mInput = Vector2.zero;

            if (mHandle != default)
            {
                mHandle.anchoredPosition = Vector2.zero;
            }

            mRectTransform.anchoredPosition = mStartPosition;
        }

        #endregion
    }

    /// <summary>
    /// 摇杆模式
    /// </summary>
    public enum JoystickMode
    {
        /// <summary>固定位置</summary>
        Fixed,
        /// <summary>浮动（跟随触摸点）</summary>
        Floating
    }
}
