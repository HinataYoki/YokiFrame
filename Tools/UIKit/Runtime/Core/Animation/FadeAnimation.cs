using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 淡入淡出动画（协程实现）
    /// </summary>
    public class FadeAnimation : UIAnimationBase, IPoolable
    {
        private float mFromAlpha;
        private float mToAlpha;
        private CanvasGroup mCanvasGroup;

        #region IPoolable
        
        public bool IsRecycled { get; set; }
        
        void IPoolable.OnRecycled()
        {
            Stop();
            mCanvasGroup = null;
        }
        
        #endregion

        public FadeAnimation() : base() { }

        public FadeAnimation(FadeAnimationConfig config) : base()
        {
            Setup(config);
        }

        public FadeAnimation(float duration, float fromAlpha, float toAlpha, AnimationCurve curve = null) : base()
        {
            Setup(duration, fromAlpha, toAlpha, curve);
        }

        /// <summary>
        /// 设置参数（池化复用）
        /// </summary>
        internal FadeAnimation Setup(FadeAnimationConfig config)
        {
            SetupBase(config.Duration, config.Curve);
            mFromAlpha = config.FromAlpha;
            mToAlpha = config.ToAlpha;
            return this;
        }

        /// <summary>
        /// 设置参数（池化复用）
        /// </summary>
        internal FadeAnimation Setup(float duration, float fromAlpha, float toAlpha, AnimationCurve curve = null)
        {
            SetupBase(duration, curve);
            mFromAlpha = fromAlpha;
            mToAlpha = toAlpha;
            return this;
        }

        public override void Play(RectTransform target, Action onComplete = null)
        {
            if (target == default)
            {
                onComplete?.Invoke();
                return;
            }

            if (mCanvasGroup == default || mCanvasGroup.gameObject != target.gameObject)
            {
                mCanvasGroup = EnsureCanvasGroup(target);
            }
            mCanvasGroup.alpha = mFromAlpha;
            
            base.Play(target, onComplete);
        }

        public override void Reset(RectTransform target)
        {
            if (target == default) return;
            
            if (mCanvasGroup == default || mCanvasGroup.gameObject != target.gameObject)
            {
                mCanvasGroup = EnsureCanvasGroup(target);
            }
            mCanvasGroup.alpha = mFromAlpha;
        }

        public override void SetToEndState(RectTransform target)
        {
            if (target == default) return;
            
            if (mCanvasGroup == default || mCanvasGroup.gameObject != target.gameObject)
            {
                mCanvasGroup = EnsureCanvasGroup(target);
            }
            mCanvasGroup.alpha = mToAlpha;
        }

        protected override void ApplyAnimation(RectTransform target, float normalizedTime)
        {
            if (mCanvasGroup != default)
            {
                mCanvasGroup.alpha = Mathf.Lerp(mFromAlpha, mToAlpha, normalizedTime);
            }
        }

        private static CanvasGroup EnsureCanvasGroup(RectTransform target)
        {
            if (target.TryGetComponent<CanvasGroup>(out var canvasGroup))
            {
                return canvasGroup;
            }
            return target.gameObject.AddComponent<CanvasGroup>();
        }
        
        /// <summary>
        /// 归还到池
        /// </summary>
        public override void Recycle() => SafePoolKit<FadeAnimation>.Instance.Recycle(this);
    }
}
