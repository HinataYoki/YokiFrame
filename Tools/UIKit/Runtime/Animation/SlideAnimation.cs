using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 滑动动画（协程实现）
    /// </summary>
    public class SlideAnimation : UIAnimationBase, IPoolable
    {
        private SlideDirection mDirection;
        private float mOffset;
        private Vector2 mFromPosition;
        private Vector2 mToPosition;

        #region IPoolable
        
        public bool IsRecycled { get; set; }
        
        void IPoolable.OnRecycled() => Stop();
        
        #endregion

        public SlideAnimation() : base() { }

        public SlideAnimation(SlideAnimationConfig config) : base()
        {
            Setup(config);
        }

        public SlideAnimation(float duration, SlideDirection direction, float offset, AnimationCurve curve = null) : base()
        {
            Setup(duration, direction, offset, curve);
        }

        /// <summary>
        /// 设置参数（池化复用）
        /// </summary>
        internal SlideAnimation Setup(SlideAnimationConfig config)
        {
            SetupBase(config.Duration, config.Curve);
            mDirection = config.Direction;
            mOffset = config.Offset;
            return this;
        }

        /// <summary>
        /// 设置参数（池化复用）
        /// </summary>
        internal SlideAnimation Setup(float duration, SlideDirection direction, float offset, AnimationCurve curve = null)
        {
            SetupBase(duration, curve);
            mDirection = direction;
            mOffset = offset;
            return this;
        }

        public override void Play(RectTransform target, Action onComplete = null)
        {
            if (target == default)
            {
                onComplete?.Invoke();
                return;
            }

            mToPosition = target.anchoredPosition;
            mFromPosition = CalculateStartPosition(mToPosition);
            target.anchoredPosition = mFromPosition;
            
            base.Play(target, onComplete);
        }

        public override void Reset(RectTransform target)
        {
            if (target == default) return;
            
            var currentPos = target.anchoredPosition;
            var startPos = CalculateStartPosition(currentPos);
            target.anchoredPosition = startPos;
        }

        public override void SetToEndState(RectTransform target)
        {
            if (target == default) return;
            target.anchoredPosition = mToPosition;
        }

        protected override void ApplyAnimation(RectTransform target, float normalizedTime)
        {
            if (target != default)
            {
                target.anchoredPosition = Vector2.Lerp(mFromPosition, mToPosition, normalizedTime);
            }
        }

        private Vector2 CalculateStartPosition(Vector2 endPosition)
        {
            return mDirection switch
            {
                SlideDirection.Top => endPosition + new Vector2(0, mOffset),
                SlideDirection.Bottom => endPosition + new Vector2(0, -mOffset),
                SlideDirection.Left => endPosition + new Vector2(-mOffset, 0),
                SlideDirection.Right => endPosition + new Vector2(mOffset, 0),
                _ => endPosition
            };
        }
        
        /// <summary>
        /// 归还到池
        /// </summary>
        public override void Recycle() => SafePoolKit<SlideAnimation>.Instance.Recycle(this);
    }
}
