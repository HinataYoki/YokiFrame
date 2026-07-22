#if UNITY_2022_3_OR_NEWER
using UnityEngine;

namespace YokiFrame
{
    /// <summary>对 CanvasGroup alpha 执行淡入淡出动画。</summary>
    public sealed class FadeAnimation : UIAnimationBase
    {
        private readonly float mFromAlpha;
        private readonly float mToAlpha;
        private CanvasGroup mCanvasGroup;

        /// <summary>创建一个指定时长和透明度范围的淡入淡出动画。</summary>
        public FadeAnimation(
            float duration,
            float fromAlpha,
            float toAlpha,
            AnimationCurve curve = null) : base(duration, curve)
        {
            mFromAlpha = Mathf.Clamp01(fromAlpha);
            mToAlpha = Mathf.Clamp01(toAlpha);
        }

        /// <inheritdoc />
        public override void Reset(RectTransform target)
        {
            if (target == default) return;
            mCanvasGroup = EnsureCanvasGroup(target);
            mCanvasGroup.alpha = mFromAlpha;
        }

        /// <inheritdoc />
        public override void SetToEndState(RectTransform target)
        {
            if (target == default) return;
            mCanvasGroup = EnsureCanvasGroup(target);
            mCanvasGroup.alpha = mToAlpha;
        }

        /// <inheritdoc />
        protected override void Apply(RectTransform target, float normalizedTime)
        {
            if (mCanvasGroup == default) mCanvasGroup = EnsureCanvasGroup(target);
            mCanvasGroup.alpha = Mathf.Lerp(mFromAlpha, mToAlpha, normalizedTime);
        }

        /// <summary>获取或创建目标上的 CanvasGroup。</summary>
        private static CanvasGroup EnsureCanvasGroup(RectTransform target)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            return group != default ? group : target.gameObject.AddComponent<CanvasGroup>();
        }
    }
}
#endif
