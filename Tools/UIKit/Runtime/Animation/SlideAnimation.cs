using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 滑动动画
    /// </summary>
    public class SlideAnimation : UIAnimationBase
    {
        private readonly SlideDirection mDirection;
        private readonly float mOffset;
        private Vector2 mFromPosition;
        private Vector2 mToPosition;

        public SlideAnimation(SlideAnimationConfig config) 
            : base(config.Duration, config.Curve)
        {
            mDirection = config.Direction;
            mOffset = config.Offset;
        }

        public SlideAnimation(float duration, SlideDirection direction, float offset, AnimationCurve curve = null)
            : base(duration, curve)
        {
            mDirection = direction;
            mOffset = offset;
        }

        public override void Play(RectTransform target, Action onComplete = null)
        {
            if (target == null)
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
            if (target == null) return;
            
            var currentPos = target.anchoredPosition;
            var startPos = CalculateStartPosition(currentPos);
            target.anchoredPosition = startPos;
        }

        public override void SetToEndState(RectTransform target)
        {
            if (target == null) return;
            target.anchoredPosition = mToPosition;
        }

        protected override void ApplyAnimation(RectTransform target, float normalizedTime)
        {
            if (target != null)
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
    }
}
