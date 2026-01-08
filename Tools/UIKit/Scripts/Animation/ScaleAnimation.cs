using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 缩放动画
    /// </summary>
    public class ScaleAnimation : UIAnimationBase
    {
        private readonly Vector3 mFromScale;
        private readonly Vector3 mToScale;

        public ScaleAnimation(ScaleAnimationConfig config) 
            : base(config.Duration, config.Curve)
        {
            mFromScale = config.FromScale;
            mToScale = config.ToScale;
        }

        public ScaleAnimation(float duration, Vector3 fromScale, Vector3 toScale, AnimationCurve curve = null)
            : base(duration, curve)
        {
            mFromScale = fromScale;
            mToScale = toScale;
        }

        public override void Play(RectTransform target, Action onComplete = null)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                return;
            }

            target.localScale = mFromScale;
            base.Play(target, onComplete);
        }

        public override void Reset(RectTransform target)
        {
            if (target == null) return;
            target.localScale = mFromScale;
        }

        public override void SetToEndState(RectTransform target)
        {
            if (target == null) return;
            target.localScale = mToScale;
        }

        protected override void ApplyAnimation(RectTransform target, float normalizedTime)
        {
            if (target != null)
            {
                target.localScale = Vector3.Lerp(mFromScale, mToScale, normalizedTime);
            }
        }
    }
}
