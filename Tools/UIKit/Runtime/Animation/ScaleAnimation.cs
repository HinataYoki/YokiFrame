using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 缩放动画（协程实现）
    /// </summary>
    public class ScaleAnimation : UIAnimationBase, IPoolable
    {
        private Vector3 mFromScale;
        private Vector3 mToScale;

        #region IPoolable
        
        public bool IsRecycled { get; set; }
        
        void IPoolable.OnRecycled() => Stop();
        
        #endregion

        public ScaleAnimation() : base() { }

        public ScaleAnimation(ScaleAnimationConfig config) : base()
        {
            Setup(config);
        }

        public ScaleAnimation(float duration, Vector3 fromScale, Vector3 toScale, AnimationCurve curve = null) : base()
        {
            Setup(duration, fromScale, toScale, curve);
        }

        /// <summary>
        /// 设置参数（池化复用）
        /// </summary>
        internal ScaleAnimation Setup(ScaleAnimationConfig config)
        {
            SetupBase(config.Duration, config.Curve);
            mFromScale = config.FromScale;
            mToScale = config.ToScale;
            return this;
        }

        /// <summary>
        /// 设置参数（池化复用）
        /// </summary>
        internal ScaleAnimation Setup(float duration, Vector3 fromScale, Vector3 toScale, AnimationCurve curve = null)
        {
            SetupBase(duration, curve);
            mFromScale = fromScale;
            mToScale = toScale;
            return this;
        }

        public override void Play(RectTransform target, Action onComplete = null)
        {
            if (target == default)
            {
                onComplete?.Invoke();
                return;
            }

            target.localScale = mFromScale;
            base.Play(target, onComplete);
        }

        public override void Reset(RectTransform target)
        {
            if (target == default) return;
            target.localScale = mFromScale;
        }

        public override void SetToEndState(RectTransform target)
        {
            if (target == default) return;
            target.localScale = mToScale;
        }

        protected override void ApplyAnimation(RectTransform target, float normalizedTime)
        {
            if (target != default)
            {
                target.localScale = Vector3.Lerp(mFromScale, mToScale, normalizedTime);
            }
        }
        
        /// <summary>
        /// 归还到池
        /// </summary>
        public override void Recycle() => SafePoolKit<ScaleAnimation>.Instance.Recycle(this);
    }
}
