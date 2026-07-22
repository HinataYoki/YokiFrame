#if UNITY_2022_3_OR_NEWER
using UnityEngine;

namespace YokiFrame
{
    /// <summary>对 RectTransform anchoredPosition 执行滑入滑出动画。</summary>
    public sealed class SlideAnimation : UIAnimationBase
    {
        private readonly Vector2 mFromPosition;
        private readonly Vector2 mToPosition;
        private readonly SlideDirection mDirection;
        private readonly float mOffset;
        private readonly bool mUseDirection;
        private Vector2 mRuntimeFromPosition;
        private Vector2 mRuntimeToPosition;
        private bool mHasRuntimeState;

        /// <summary>创建一个指定时长和位移范围的动画。</summary>
        public SlideAnimation(
            float duration,
            Vector2 fromPosition,
            Vector2 toPosition,
            AnimationCurve curve = null) : base(duration, curve)
        {
            mFromPosition = fromPosition;
            mToPosition = toPosition;
            mRuntimeFromPosition = fromPosition;
            mRuntimeToPosition = toPosition;
            mHasRuntimeState = true;
        }

        /// <summary>创建一个以目标当前位置为终点、按方向偏移为起点的滑入动画。</summary>
        public SlideAnimation(
            float duration,
            SlideDirection direction,
            float offset,
            AnimationCurve curve = null) : base(duration, curve)
        {
            mDirection = direction;
            mOffset = Mathf.Max(0f, offset);
            mUseDirection = true;
        }

        /// <inheritdoc />
        public override void Reset(RectTransform target)
        {
            if (target == default) return;
            if (mUseDirection)
            {
                mRuntimeToPosition = target.anchoredPosition;
                mRuntimeFromPosition = CalculateStartPosition(mRuntimeToPosition);
            }
            else
            {
                mRuntimeFromPosition = mFromPosition;
                mRuntimeToPosition = mToPosition;
            }
            target.anchoredPosition = mRuntimeFromPosition;
            mHasRuntimeState = true;
        }

        /// <inheritdoc />
        public override void SetToEndState(RectTransform target)
        {
            if (target != default && mHasRuntimeState) target.anchoredPosition = mRuntimeToPosition;
        }

        /// <inheritdoc />
        protected override void Apply(RectTransform target, float normalizedTime)
        {
            if (target != default)
                target.anchoredPosition = Vector2.LerpUnclamped(
                    mRuntimeFromPosition,
                    mRuntimeToPosition,
                    normalizedTime);
        }

        /// <summary>根据配置方向计算当前目标位置之外的动画起点。</summary>
        private Vector2 CalculateStartPosition(Vector2 endPosition)
        {
            switch (mDirection)
            {
                case SlideDirection.Top: return endPosition + Vector2.up * mOffset;
                case SlideDirection.Bottom: return endPosition + Vector2.down * mOffset;
                case SlideDirection.Left: return endPosition + Vector2.left * mOffset;
                case SlideDirection.Right: return endPosition + Vector2.right * mOffset;
                default: return endPosition;
            }
        }
    }
}
#endif
