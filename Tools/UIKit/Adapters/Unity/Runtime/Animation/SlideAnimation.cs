#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>对 RectTransform anchoredPosition 执行滑入滑出动画。</summary>
    public sealed class SlideAnimation : UIAnimationBase, IUIAnimationInternal
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

        /// <inheritdoc />
        void IUIAnimationInternal.PlayFromCurrent(RectTransform target, Action onComplete)
        {
            if (target == default)
            {
                if (onComplete != null) onComplete();
                return;
            }

            Vector2 currentPosition = target.anchoredPosition;
            Vector2 targetPosition = mHasRuntimeState ? mRuntimeToPosition : mToPosition;
            
            if (mUseDirection && !mHasRuntimeState)
            {
                targetPosition = currentPosition;
            }

            float distance = Vector2.Distance(currentPosition, targetPosition);
            Vector2 originalFrom = mHasRuntimeState ? mRuntimeFromPosition : mFromPosition;
            float totalDistance = Vector2.Distance(originalFrom, targetPosition);

            if (distance < 0.001f || totalDistance < 0.001f)
            {
                target.anchoredPosition = targetPosition;
                if (onComplete != null) onComplete();
                return;
            }

            float scaledDuration = Duration * (distance / totalDistance);
            PlayFromCurrentVector2(target, currentPosition, targetPosition, scaledDuration, onComplete);
        }

        /// <summary>从当前位置播放到目标位置。</summary>
        private void PlayFromCurrentVector2(
            RectTransform target,
            Vector2 fromPosition,
            Vector2 toPosition,
            float scaledDuration,
            Action onComplete)
        {
            Stop();
            mOnComplete = onComplete;
            if (scaledDuration <= 0f)
            {
                target.anchoredPosition = toPosition;
                Complete();
                return;
            }

            EnsureRunner();
            IsPlaying = true;
            mCoroutine = sRunner.StartCoroutine(PlayVector2Coroutine(target, fromPosition, toPosition, scaledDuration));
        }

        /// <summary>按缩放时长从当前位置插值到目标位置。</summary>
        private IEnumerator PlayVector2Coroutine(
            RectTransform target,
            Vector2 fromPosition,
            Vector2 toPosition,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (target == default) yield break;
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                target.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, EvaluateCurve(normalizedTime));
                yield return null;
            }

            if (target != default) target.anchoredPosition = toPosition;
            Complete();
        }
    }
}
#endif
