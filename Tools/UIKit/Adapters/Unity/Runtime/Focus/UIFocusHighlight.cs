#if UNITY_2022_3_OR_NEWER
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>跟随当前焦点 RectTransform 的可视高亮。</summary>
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    [DisallowMultipleComponent]
    [AddComponentMenu("YokiFrame/UIKit/Focus Highlight")]
    public sealed class UIFocusHighlight : MonoBehaviour
    {
        [SerializeField] private GamepadConfig mConfig;
        [SerializeField] private Sprite mHighlightSprite;
        [SerializeField] private Image.Type mImageType = Image.Type.Sliced;
        private RectTransform mRectTransform;
        private Image mImage;
        private CanvasGroup mCanvasGroup;
        private GameObject mCurrentTarget;
        private RectTransform mTargetRect;
        private Vector3 mLastTargetPosition;
        private Vector2 mLastTargetSize;
        private bool mHasTargetLayout;

        /// <summary>当前跟随目标。</summary>
        public GameObject CurrentTarget => mCurrentTarget;

        /// <summary>当前高亮是否可见。</summary>
        public bool IsVisible => mCanvasGroup != null && mCanvasGroup.alpha > 0f;

        /// <summary>获取或设置焦点导航配置。</summary>
        public GamepadConfig Config
        {
            get { return mConfig; }
            set { mConfig = value; }
        }

        /// <summary>初始化高亮组件并保持默认隐藏。</summary>
        private void Awake()
        {
            InitializeComponents();
        }

        /// <summary>在目标移动时同步高亮位置和大小。</summary>
        private void LateUpdate()
        {
            if (mTargetRect != null && IsVisible) UpdatePositionIfChanged();
        }

        /// <summary>设置新的高亮目标；无效目标会隐藏高亮。</summary>
        public void SetTarget(GameObject target)
        {
            mCurrentTarget = target;
            mTargetRect = target != null ? target.GetComponent<RectTransform>() : null;
            mHasTargetLayout = false;
            Selectable selectable = target != null ? target.GetComponent<Selectable>() : null;
            if (mTargetRect == null || !target.activeInHierarchy || (selectable != null && !selectable.interactable))
            {
                Hide();
                return;
            }
            UpdatePositionImmediate();
            Show();
        }

        /// <summary>显示有效目标的高亮。</summary>
        public void Show()
        {
            if (mTargetRect == null) return;
            mImage.enabled = true;
            mCanvasGroup.alpha = 1f;
        }

        /// <summary>隐藏高亮并清除尺寸。</summary>
        public void Hide()
        {
            mHasTargetLayout = false;
            if (mCanvasGroup != null) mCanvasGroup.alpha = 0f;
            if (mImage != null) mImage.enabled = false;
            if (mRectTransform != null) mRectTransform.sizeDelta = Vector2.zero;
        }

        /// <summary>立即同步目标的世界位置和视觉尺寸。</summary>
        public void UpdatePositionImmediate()
        {
            if (mTargetRect == null) return;
            GetTargetLayout(out Vector3 position, out Vector2 size);
            ApplyTargetLayout(position, size);
        }

        /// <summary>仅在目标位置或尺寸变化时写入高亮布局，避免每帧触发无效 Canvas 脏标记。</summary>
        private void UpdatePositionIfChanged()
        {
            GetTargetLayout(out Vector3 position, out Vector2 size);
            if (mHasTargetLayout && position == mLastTargetPosition && size == mLastTargetSize)
                return;
            ApplyTargetLayout(position, size);
        }

        /// <summary>读取当前目标的世界位置和带配置边距的高亮尺寸。</summary>
        private void GetTargetLayout(out Vector3 position, out Vector2 size)
        {
            GamepadConfig config = mConfig != null ? mConfig : GamepadConfig.Default;
            position = mTargetRect.position;
            size = mTargetRect.rect.size + config.HighlightPadding * 2f;
        }

        /// <summary>提交已计算的布局并缓存结果，供后续帧快速跳过重复写入。</summary>
        private void ApplyTargetLayout(Vector3 position, Vector2 size)
        {
            mRectTransform.position = position;
            mRectTransform.sizeDelta = size;
            mLastTargetPosition = position;
            mLastTargetSize = size;
            mHasTargetLayout = true;
        }

        /// <summary>设置高亮颜色。</summary>
        public void SetColor(Color color)
        {
            if (mImage != null) mImage.color = color;
        }

        /// <summary>在指定父节点下创建高亮实例。</summary>
        public static UIFocusHighlight Create(Transform parent, GamepadConfig config = null)
        {
            GameObject host = new("FocusHighlight", typeof(RectTransform), typeof(Image), typeof(UIFocusHighlight));
            RectTransform rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            UIFocusHighlight highlight = host.GetComponent<UIFocusHighlight>();
            highlight.mConfig = config;
            highlight.InitializeComponents();
            highlight.SetColor((config != null ? config : GamepadConfig.Default).HighlightColor);
            return highlight;
        }

        /// <summary>缓存组件引用和序列化视觉配置。</summary>
        private void InitializeComponents()
        {
            mRectTransform = GetComponent<RectTransform>();
            mImage = GetComponent<Image>();
            mCanvasGroup = GetComponent<CanvasGroup>();
            if (mCanvasGroup == null) mCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            mImage.sprite = mHighlightSprite;
            mImage.type = mHighlightSprite != null ? mImageType : Image.Type.Simple;
            mImage.raycastTarget = false;
            Hide();
        }
    }
}
#endif
