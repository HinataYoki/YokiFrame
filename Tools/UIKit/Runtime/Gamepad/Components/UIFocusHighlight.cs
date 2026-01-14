using UnityEngine;
using UnityEngine.UI;
#if YOKIFRAME_DOTWEEN_SUPPORT
using DG.Tweening;
#endif

namespace YokiFrame
{
    /// <summary>
    /// UI 焦点高亮组件 - 跟随当前焦点元素显示高亮效果
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public class UIFocusHighlight : MonoBehaviour
    {
        #region 配置

        [Header("配置")]
        [SerializeField] private GamepadConfig mConfig;

        [Header("视觉设置")]
        [SerializeField] private Sprite mHighlightSprite;
        [SerializeField] private Image.Type mImageType = Image.Type.Sliced;

        #endregion

        #region 组件缓存

        private RectTransform mRectTransform;
        private Image mImage;
        private CanvasGroup mCanvasGroup;
        private Canvas mCanvas;

        #endregion

        #region 状态

        private GameObject mCurrentTarget;
        private RectTransform mTargetRect;
        private bool mIsVisible;

#if YOKIFRAME_DOTWEEN_SUPPORT
        private Tweener mMoveTween;
        private Tweener mSizeTween;
        private Tweener mFadeTween;
#endif

        #endregion

        #region 属性

        /// <summary>
        /// 当前跟随的目标
        /// </summary>
        public GameObject CurrentTarget => mCurrentTarget;

        /// <summary>
        /// 是否可见
        /// </summary>
        public bool IsVisible => mIsVisible;

        /// <summary>
        /// 配置
        /// </summary>
        public GamepadConfig Config
        {
            get => mConfig;
            set => mConfig = value;
        }

        #endregion

        #region 生命周期

        private void Awake()
        {
            mRectTransform = GetComponent<RectTransform>();
            mImage = GetComponent<Image>();
            mCanvasGroup = GetComponent<CanvasGroup>();
            
            if (mCanvasGroup == default)
            {
                mCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // 初始化 Image
            if (mHighlightSprite != default)
            {
                mImage.sprite = mHighlightSprite;
                mImage.type = mImageType;
            }
            else
            {
                // 无 Sprite 时使用 Simple 类型，避免 Sliced 模式下全屏填充
                mImage.type = Image.Type.Simple;
            }
            mImage.raycastTarget = false;

            // 初始隐藏 - 同时禁用 Image 防止闪烁
            mCanvasGroup.alpha = 0f;
            mImage.enabled = false;
            mIsVisible = false;
            
            // 初始大小为 0
            mRectTransform.sizeDelta = Vector2.zero;

            // 获取 Canvas
            mCanvas = GetComponentInParent<Canvas>();
            
            Debug.Log($"[UIFocusHighlight] Awake - Image.enabled={mImage.enabled}, sizeDelta={mRectTransform.sizeDelta}, alpha={mCanvasGroup.alpha}");
        }

        private void OnDestroy()
        {
#if YOKIFRAME_DOTWEEN_SUPPORT
            mMoveTween?.Kill();
            mSizeTween?.Kill();
            mFadeTween?.Kill();
#endif
        }

        private void LateUpdate()
        {
            // 持续跟随目标（处理目标移动的情况）
            if (mIsVisible && mTargetRect != default)
            {
                UpdatePositionImmediate();
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置跟随目标
        /// </summary>
        public void SetTarget(GameObject target)
        {
            if (target == mCurrentTarget) return;

            mCurrentTarget = target;

            if (target == default)
            {
                Hide();
                mTargetRect = null;
                mRectTransform.sizeDelta = Vector2.zero;
                return;
            }

            if (!target.activeInHierarchy)
            {
                Hide();
                mTargetRect = null;
                mRectTransform.sizeDelta = Vector2.zero;
                return;
            }

            var selectable = target.GetComponent<Selectable>();
            if (selectable != null && !selectable.interactable)
            {
                Hide();
                mTargetRect = null;
                mRectTransform.sizeDelta = Vector2.zero;
                return;
            }

            mTargetRect = target.GetComponent<RectTransform>();
            if (mTargetRect == default)
            {
                Hide();
                mRectTransform.sizeDelta = Vector2.zero;
                return;
            }

            var rectSize = mTargetRect.rect.size;
            if (rectSize.x <= 0f || rectSize.y <= 0f || rectSize.x > 2000f || rectSize.y > 2000f)
            {
                Hide();
                mTargetRect = null;
                mRectTransform.sizeDelta = Vector2.zero;
                return;
            }

            // 先更新位置，再显示，避免闪烁
            UpdatePositionImmediate();
            Show();
            AnimateToTarget();
        }

        /// <summary>
        /// 显示高亮
        /// </summary>
        public void Show()
        {
            if (mIsVisible) return;
            
            // 无目标时不显示
            if (mTargetRect == default) return;
            
            mIsVisible = true;
            mImage.enabled = true;
            
            Debug.Log($"[UIFocusHighlight] Show - target={mCurrentTarget?.name}, sizeDelta={mRectTransform.sizeDelta}");

#if YOKIFRAME_DOTWEEN_SUPPORT
            mFadeTween?.Kill();
            mFadeTween = mCanvasGroup.DOFade(1f, GetConfig().HighlightScaleDuration);
#else
            mCanvasGroup.alpha = 1f;
#endif
        }

        /// <summary>
        /// 隐藏高亮
        /// </summary>
        public void Hide()
        {
            if (!mIsVisible) return;
            mIsVisible = false;

#if YOKIFRAME_DOTWEEN_SUPPORT
            mFadeTween?.Kill();
            mFadeTween = mCanvasGroup.DOFade(0f, GetConfig().HighlightScaleDuration)
                .OnComplete(static () => { })  // 确保完成
                .OnKill(() => { if (!mIsVisible) mImage.enabled = false; });
#else
            mCanvasGroup.alpha = 0f;
            mImage.enabled = false;
#endif

            // 隐藏时归零尺寸，避免后续启用时残留上一次的巨大 size
            mRectTransform.sizeDelta = Vector2.zero;
        }

        /// <summary>
        /// 立即更新位置（无动画）
        /// </summary>
        public void UpdatePositionImmediate()
        {
            if (mTargetRect == default) return;

            var config = GetConfig();
            var targetPos = GetTargetWorldPosition();
            var targetSize = GetTargetSize() + config.HighlightPadding * 2f;

            mRectTransform.position = targetPos;
            mRectTransform.sizeDelta = targetSize;
        }

        /// <summary>
        /// 设置高亮颜色
        /// </summary>
        public void SetColor(Color color)
        {
            if (mImage != default)
            {
                mImage.color = color;
            }
        }

        #endregion

        #region 私有方法

        private GamepadConfig GetConfig()
        {
            return mConfig != default ? mConfig : GamepadConfig.Default;
        }

        private void AnimateToTarget()
        {
            if (mTargetRect == default) return;

            var config = GetConfig();
            var targetPos = GetTargetWorldPosition();
            var targetSize = GetTargetSize() + config.HighlightPadding * 2f;

#if YOKIFRAME_DOTWEEN_SUPPORT
            mMoveTween?.Kill();
            mSizeTween?.Kill();

            mMoveTween = mRectTransform.DOMove(targetPos, config.HighlightMoveDuration)
                .SetEase(Ease.OutQuad);
            mSizeTween = mRectTransform.DOSizeDelta(targetSize, config.HighlightScaleDuration)
                .SetEase(Ease.OutQuad);
#else
            mRectTransform.position = targetPos;
            mRectTransform.sizeDelta = targetSize;
#endif
        }

        private Vector3 GetTargetWorldPosition()
        {
            if (mTargetRect == default) return Vector3.zero;
            return mTargetRect.position;
        }

        private Vector2 GetTargetSize()
        {
            if (mTargetRect == default) return Vector2.zero;
            return mTargetRect.rect.size;
        }

        #endregion

        #region 工厂方法

        /// <summary>
        /// 创建焦点高亮实例
        /// </summary>
        public static UIFocusHighlight Create(Transform parent, GamepadConfig config = null)
        {
            var go = new GameObject("FocusHighlight", typeof(RectTransform), typeof(Image), typeof(UIFocusHighlight));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            // 设置锚点为中心
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            
            // 初始大小为 0，避免未设置目标时覆盖屏幕
            rect.sizeDelta = Vector2.zero;

            var highlight = go.GetComponent<UIFocusHighlight>();
            highlight.mConfig = config;

            // 设置默认颜色，确保 Image 保持禁用状态
            var image = go.GetComponent<Image>();
            image.color = config != default ? config.HighlightColor : GamepadConfig.Default.HighlightColor;
            image.enabled = false;  // 确保初始禁用

            return highlight;
        }

        #endregion
    }
}
