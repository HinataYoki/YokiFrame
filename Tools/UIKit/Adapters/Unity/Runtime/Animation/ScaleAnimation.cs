#if UNITY_2022_3_OR_NEWER
using UnityEngine;

namespace YokiFrame
{
    /// <summary>对 RectTransform localScale 执行缩放动画。</summary>
    public sealed class ScaleAnimation : UIAnimationBase
    {
        private readonly Vector3 mFromScale;
        private readonly Vector3 mToScale;

        /// <summary>创建一个指定时长和缩放范围的动画。</summary>
        public ScaleAnimation(
            float duration,
            Vector3 fromScale,
            Vector3 toScale,
            AnimationCurve curve = null) : base(duration, curve)
        {
            mFromScale = fromScale;
            mToScale = toScale;
        }

        /// <inheritdoc />
        public override void Reset(RectTransform target)
        {
            if (target != default) target.localScale = mFromScale;
        }

        /// <inheritdoc />
        public override void SetToEndState(RectTransform target)
        {
            if (target != default) target.localScale = mToScale;
        }

        /// <inheritdoc />
        protected override void Apply(RectTransform target, float normalizedTime)
        {
            if (target != default)
                target.localScale = Vector3.LerpUnclamped(mFromScale, mToScale, normalizedTime);
        }
    }
}
#endif
