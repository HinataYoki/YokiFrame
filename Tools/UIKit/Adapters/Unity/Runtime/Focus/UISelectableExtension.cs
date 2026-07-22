#if UNITY_2022_3_OR_NEWER
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>为 Selectable 增加焦点缩放、指针选中和导航覆盖。</summary>
    [RequireComponent(typeof(Selectable))]
    [DisallowMultipleComponent]
    [AddComponentMenu("YokiFrame/UIKit/Selectable Extension")]
    public sealed class UISelectableExtension : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        [SerializeField] private int mSelectSoundId;
        [SerializeField] private int mSubmitSoundId;
        [SerializeField] private float mSelectedScale = 1.05f;
        [SerializeField] private float mScaleDuration = 0.1f;
        [SerializeField] private Selectable mOverrideUp;
        [SerializeField] private Selectable mOverrideDown;
        [SerializeField] private Selectable mOverrideLeft;
        [SerializeField] private Selectable mOverrideRight;
        private Selectable mSelectable;
        private RectTransform mRectTransform;
        private Vector3 mOriginalScale;

        /// <summary>初始化组件和序列化导航覆盖。</summary>
        private void Awake()
        {
            mSelectable = GetComponent<Selectable>();
            mRectTransform = GetComponent<RectTransform>();
            mOriginalScale = mRectTransform != null ? mRectTransform.localScale : Vector3.one;
            mScaleDuration = Mathf.Max(0f, mScaleDuration);
            ApplyNavigationOverrides();
        }

        /// <summary>选中时应用视觉缩放。</summary>
        public void OnSelect(BaseEventData eventData)
        {
            if (mRectTransform != null) mRectTransform.localScale = mOriginalScale * mSelectedScale;
        }

        /// <summary>失去选中时恢复原始缩放。</summary>
        public void OnDeselect(BaseEventData eventData)
        {
            if (mRectTransform != null) mRectTransform.localScale = mOriginalScale;
        }

        /// <summary>导航输入模式下允许指针悬停切换焦点。</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (UIKit.InputMode == UIInputMode.Navigation && mSelectable != null && mSelectable.interactable)
                UIKit.SetFocus(mSelectable);
        }

        /// <summary>设置单个方向的导航覆盖。</summary>
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

        /// <summary>清除全部覆盖并恢复 Automatic Navigation。</summary>
        public void ClearNavigationOverrides()
        {
            mOverrideUp = null;
            mOverrideDown = null;
            mOverrideLeft = null;
            mOverrideRight = null;
            if (mSelectable == null) return;
            Navigation navigation = mSelectable.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            mSelectable.navigation = navigation;
        }

        /// <summary>把非空覆盖写入 Selectable Navigation。</summary>
        private void ApplyNavigationOverrides()
        {
            if (mSelectable == null) return;
            if (mOverrideUp == null && mOverrideDown == null && mOverrideLeft == null && mOverrideRight == null) return;
            Navigation navigation = mSelectable.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            if (mOverrideUp != null) navigation.selectOnUp = mOverrideUp;
            if (mOverrideDown != null) navigation.selectOnDown = mOverrideDown;
            if (mOverrideLeft != null) navigation.selectOnLeft = mOverrideLeft;
            if (mOverrideRight != null) navigation.selectOnRight = mOverrideRight;
            mSelectable.navigation = navigation;
        }
    }
}
#endif
