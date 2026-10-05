#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>对 RectTransform localScale 执行缩放动画。</summary>
    public sealed class ScaleAnimation : UIAnimationBase, IUIAnimationInternal
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

        /// <inheritdoc />
        void IUIAnimationInternal.PlayFromCurrent(RectTransform target, Action onComplete)
        {
            if (target == default)
            {
                if (onComplete != null) onComplete();
                return;
            }

            Vector3 currentScale = target.localScale;
            float distance = Vector3.Distance(currentScale, mToScale);
            float totalDistance = Vector3.Distance(mFromScale, mToScale);

            if (distance < 0.001f || totalDistance < 0.001f)
            {
                target.localScale = mToScale;
                if (onComplete != null) onComplete();
                return;
            }

            float scaledDuration = Duration * (distance / totalDistance);
            PlayFromCurrentVector3(target, currentScale, mToScale, scaledDuration, onComplete);
        }

        /// <summary>从当前缩放值播放到目标缩放值。</summary>
        private void PlayFromCurrentVector3(
            RectTransform target,
            Vector3 fromScale,
            Vector3 toScale,
            float scaledDuration,
            Action onComplete)
        {
            Stop();
            mOnComplete = onComplete;
            if (scaledDuration <= 0f)
            {
                SetToEndState(target);
                Complete();
                return;
            }

            EnsureRunner();
            IsPlaying = true;
            mCoroutine = sRunner.StartCoroutine(PlayVector3Coroutine(target, fromScale, toScale, scaledDuration));
        }

        /// <summary>按缩放时长从当前缩放插值到目标缩放。</summary>
        private IEnumerator PlayVector3Coroutine(
            RectTransform target,
            Vector3 fromScale,
            Vector3 toScale,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (target == default) yield break;
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                target.localScale = Vector3.LerpUnclamped(fromScale, toScale, EvaluateCurve(normalizedTime));
                yield return null;
            }

            if (target != default) SetToEndState(target);
            Complete();
        }
    }
}
#endif
