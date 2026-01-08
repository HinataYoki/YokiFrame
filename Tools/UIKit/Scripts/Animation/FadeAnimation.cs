using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 淡入淡出动画
    /// </summary>
    public class FadeAnimation : UIAnimationBase
    {
        private readonly float mFromAlpha;
        private readonly float mToAlpha;
        private CanvasGroup mCanvasGroup;

        public FadeAnimation(FadeAnimationConfig config) 
            : base(config.Duration, config.Curve)
        {
            mFromAlpha = config.FromAlpha;
            mToAlpha = config.ToAlpha;
        }

        public FadeAnimation(float duration, float fromAlpha, float toAlpha, AnimationCurve curve = null)
            : base(duration, curve)
        {
            mFromAlpha = fromAlpha;
            mToAlpha = toAlpha;
        }

        public override void Play(RectTransform target, Action onComplete = null)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                return;
            }

            mCanvasGroup = GetOrAddCanvasGroup(target);
            mCanvasGroup.alpha = mFromAlpha;
            
            base.Play(target, onComplete);
        }

        public override void Reset(RectTransform target)
        {
            if (target == null) return;
            
            var canvasGroup = GetOrAddCanvasGroup(target);
            canvasGroup.alpha = mFromAlpha;
        }

        public override void SetToEndState(RectTransform target)
        {
            if (target == null) return;
            
            var canvasGroup = GetOrAddCanvasGroup(target);
            canvasGroup.alpha = mToAlpha;
        }

        protected override void ApplyAnimation(RectTransform target, float normalizedTime)
        {
            if (mCanvasGroup != null)
            {
                mCanvasGroup.alpha = Mathf.Lerp(mFromAlpha, mToAlpha, normalizedTime);
            }
        }

        private CanvasGroup GetOrAddCanvasGroup(RectTransform target)
        {
            var canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = target.gameObject.AddComponent<CanvasGroup>();
            }
            return canvasGroup;
        }
    }
}
