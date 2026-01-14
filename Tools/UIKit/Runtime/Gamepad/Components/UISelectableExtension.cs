using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if YOKIFRAME_DOTWEEN_SUPPORT
using DG.Tweening;
#endif

namespace YokiFrame
{
    /// <summary>
    /// Selectable 扩展组件 - 为 UI 元素添加手柄支持的额外功能
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class UISelectableExtension : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        #region 配置

        [Header("焦点设置")]
        [Tooltip("选中时播放的音效 ID")]
        [SerializeField] private int mSelectSoundId;

        [Tooltip("确认时播放的音效 ID")]
        [SerializeField] private int mSubmitSoundId;

        [Tooltip("选中时的缩放")]
        [SerializeField] private float mSelectedScale = 1.05f;

        [Tooltip("缩放动画时长")]
        [SerializeField] private float mScaleDuration = 0.1f;

        [Header("导航覆盖")]
        [Tooltip("覆盖上方导航目标")]
        [SerializeField] private Selectable mOverrideUp;

        [Tooltip("覆盖下方导航目标")]
        [SerializeField] private Selectable mOverrideDown;

        [Tooltip("覆盖左方导航目标")]
        [SerializeField] private Selectable mOverrideLeft;

        [Tooltip("覆盖右方导航目标")]
        [SerializeField] private Selectable mOverrideRight;

        #endregion

        #region 组件缓存

        private Selectable mSelectable;
        private RectTransform mRectTransform;
        private Vector3 mOriginalScale;

        #endregion

        #region 生命周期

        private void Awake()
        {
            mSelectable = GetComponent<Selectable>();
            mRectTransform = GetComponent<RectTransform>();
            mOriginalScale = mRectTransform.localScale;

            ApplyNavigationOverrides();
        }

        #endregion

        #region 事件处理

        public void OnSelect(BaseEventData eventData)
        {
            // 播放选中音效
            if (mSelectSoundId > 0)
            {
                // AudioKit.Play(mSelectSoundId);
            }

            // 缩放动画
            if (Mathf.Abs(mSelectedScale - 1f) > 0.001f)
            {
                AnimateScale(mOriginalScale * mSelectedScale);
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
            // 恢复缩放
            if (Mathf.Abs(mSelectedScale - 1f) > 0.001f)
            {
                AnimateScale(mOriginalScale);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // 鼠标悬停时自动选中（可选行为）
            if (UIRoot.Instance != default &&
                UIRoot.Instance.CurrentInputMode == UIInputMode.Pointer)
            {
                // 在指针模式下，悬停不自动选中
                return;
            }

            if (mSelectable != null && mSelectable.interactable)
            {
                EventSystem.current?.SetSelectedGameObject(gameObject);
            }
        }

        #endregion

        #region 导航覆盖

        private void ApplyNavigationOverrides()
        {
            if (mSelectable == null) return;

            bool hasOverride = mOverrideUp != null || mOverrideDown != null ||
                              mOverrideLeft != null || mOverrideRight != null;

            if (!hasOverride) return;

            var nav = mSelectable.navigation;

            // 如果有任何覆盖，切换到显式模式
            if (nav.mode != Navigation.Mode.Explicit)
            {
                // 先获取自动导航的结果
                var autoUp = mSelectable.FindSelectableOnUp();
                var autoDown = mSelectable.FindSelectableOnDown();
                var autoLeft = mSelectable.FindSelectableOnLeft();
                var autoRight = mSelectable.FindSelectableOnRight();

                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = mOverrideUp != null ? mOverrideUp : autoUp;
                nav.selectOnDown = mOverrideDown != null ? mOverrideDown : autoDown;
                nav.selectOnLeft = mOverrideLeft != null ? mOverrideLeft : autoLeft;
                nav.selectOnRight = mOverrideRight != null ? mOverrideRight : autoRight;
            }
            else
            {
                // 已经是显式模式，只覆盖指定的方向
                if (mOverrideUp != null) nav.selectOnUp = mOverrideUp;
                if (mOverrideDown != null) nav.selectOnDown = mOverrideDown;
                if (mOverrideLeft != null) nav.selectOnLeft = mOverrideLeft;
                if (mOverrideRight != null) nav.selectOnRight = mOverrideRight;
            }

            mSelectable.navigation = nav;
        }

        #endregion

        #region 动画

        private void AnimateScale(Vector3 targetScale)
        {
#if YOKIFRAME_DOTWEEN_SUPPORT
            mRectTransform.DOScale(targetScale, mScaleDuration).SetEase(DG.Tweening.Ease.OutQuad);
#else
            // 无 DOTween 时直接设置缩放
            _ = mScaleDuration; // 抑制未使用警告
            mRectTransform.localScale = targetScale;
#endif
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置导航覆盖
        /// </summary>
        public void SetNavigationOverride(MoveDirection direction, Selectable target)
        {
            switch (direction)
            {
                case MoveDirection.Up: mOverrideUp = target; break;
                case MoveDirection.Down: mOverrideDown = target; break;
                case MoveDirection.Left: mOverrideLeft = target; break;
                case MoveDirection.Right: mOverrideRight = target; break;
            }
            ApplyNavigationOverrides();
        }

        /// <summary>
        /// 清除所有导航覆盖
        /// </summary>
        public void ClearNavigationOverrides()
        {
            mOverrideUp = null;
            mOverrideDown = null;
            mOverrideLeft = null;
            mOverrideRight = null;

            if (mSelectable != null)
            {
                var nav = mSelectable.navigation;
                nav.mode = Navigation.Mode.Automatic;
                mSelectable.navigation = nav;
            }
        }

        #endregion
    }
}
