#if YOKIFRAME_DOTWEEN_SUPPORT
using System;
using UnityEngine;
using DG.Tweening;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// DOTween 淡入淡出动画
    /// </summary>
    public class DOTweenFadeAnimation : IUIAnimation
#if YOKIFRAME_UNITASK_SUPPORT
        , IUIAnimationUniTask
#endif
    {
        private readonly float mDuration;
        private readonly float mFromAlpha;
        private readonly float mToAlpha;
        private readonly Ease mEase;
        private Tweener mTweener;
        private CanvasGroup mCanvasGroup;

        public float Duration => mDuration;
        public bool IsPlaying => mTweener != null && mTweener.IsPlaying();

        public DOTweenFadeAnimation(FadeAnimationConfig config, Ease ease = Ease.OutQuad)
        {
            mDuration = config.Duration;
            mFromAlpha = config.FromAlpha;
            mToAlpha = config.ToAlpha;
            mEase = ease;
        }

        public DOTweenFadeAnimation(float duration, float fromAlpha, float toAlpha, Ease ease = Ease.OutQuad)
        {
            mDuration = duration;
            mFromAlpha = fromAlpha;
            mToAlpha = toAlpha;
            mEase = ease;
        }

        public void Play(RectTransform target, Action onComplete = null)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                return;
            }

            Stop();
            mCanvasGroup = GetOrAddCanvasGroup(target);
            mCanvasGroup.alpha = mFromAlpha;

            mTweener = mCanvasGroup.DOFade(mToAlpha, mDuration)
                .SetEase(mEase)
                .OnComplete(() => onComplete?.Invoke());
        }

        public void Stop()
        {
            mTweener?.Kill();
            mTweener = null;
        }

        public void Reset(RectTransform target)
        {
            if (target == null) return;
            var canvasGroup = GetOrAddCanvasGroup(target);
            canvasGroup.alpha = mFromAlpha;
        }

        public void SetToEndState(RectTransform target)
        {
            if (target == null) return;
            var canvasGroup = GetOrAddCanvasGroup(target);
            canvasGroup.alpha = mToAlpha;
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

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask PlayUniTaskAsync(RectTransform target, CancellationToken ct = default)
        {
            if (target == null) return;

            Stop();
            mCanvasGroup = GetOrAddCanvasGroup(target);
            mCanvasGroup.alpha = mFromAlpha;

            var tcs = new UniTaskCompletionSource();
            
            mTweener = mCanvasGroup.DOFade(mToAlpha, mDuration)
                .SetEase(mEase)
                .OnComplete(() => tcs.TrySetResult())
                .OnKill(() => tcs.TrySetResult());

            await using (ct.Register(() =>
            {
                Stop();
                SetToEndState(target);
                tcs.TrySetCanceled(ct);
            }))
            {
                await tcs.Task;
            }
        }
#endif
    }

    /// <summary>
    /// DOTween 缩放动画
    /// </summary>
    public class DOTweenScaleAnimation : IUIAnimation
#if YOKIFRAME_UNITASK_SUPPORT
        , IUIAnimationUniTask
#endif
    {
        private readonly float mDuration;
        private readonly Vector3 mFromScale;
        private readonly Vector3 mToScale;
        private readonly Ease mEase;
        private Tweener mTweener;

        public float Duration => mDuration;
        public bool IsPlaying => mTweener != null && mTweener.IsPlaying();

        public DOTweenScaleAnimation(ScaleAnimationConfig config, Ease ease = Ease.OutBack)
        {
            mDuration = config.Duration;
            mFromScale = config.FromScale;
            mToScale = config.ToScale;
            mEase = ease;
        }

        public DOTweenScaleAnimation(float duration, Vector3 fromScale, Vector3 toScale, Ease ease = Ease.OutBack)
        {
            mDuration = duration;
            mFromScale = fromScale;
            mToScale = toScale;
            mEase = ease;
        }

        public void Play(RectTransform target, Action onComplete = null)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                return;
            }

            Stop();
            target.localScale = mFromScale;

            mTweener = target.DOScale(mToScale, mDuration)
                .SetEase(mEase)
                .OnComplete(() => onComplete?.Invoke());
        }

        public void Stop()
        {
            mTweener?.Kill();
            mTweener = null;
        }

        public void Reset(RectTransform target)
        {
            if (target == null) return;
            target.localScale = mFromScale;
        }

        public void SetToEndState(RectTransform target)
        {
            if (target == null) return;
            target.localScale = mToScale;
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask PlayUniTaskAsync(RectTransform target, CancellationToken ct = default)
        {
            if (target == null) return;

            Stop();
            target.localScale = mFromScale;

            var tcs = new UniTaskCompletionSource();
            
            mTweener = target.DOScale(mToScale, mDuration)
                .SetEase(mEase)
                .OnComplete(() => tcs.TrySetResult())
                .OnKill(() => tcs.TrySetResult());

            await using (ct.Register(() =>
            {
                Stop();
                SetToEndState(target);
                tcs.TrySetCanceled(ct);
            }))
            {
                await tcs.Task;
            }
        }
#endif
    }

    /// <summary>
    /// DOTween 滑动动画
    /// </summary>
    public class DOTweenSlideAnimation : IUIAnimation
#if YOKIFRAME_UNITASK_SUPPORT
        , IUIAnimationUniTask
#endif
    {
        private readonly float mDuration;
        private readonly SlideDirection mDirection;
        private readonly float mOffset;
        private readonly Ease mEase;
        private Tweener mTweener;
        private Vector2 mToPosition;

        public float Duration => mDuration;
        public bool IsPlaying => mTweener != null && mTweener.IsPlaying();

        public DOTweenSlideAnimation(SlideAnimationConfig config, Ease ease = Ease.OutQuad)
        {
            mDuration = config.Duration;
            mDirection = config.Direction;
            mOffset = config.Offset;
            mEase = ease;
        }

        public DOTweenSlideAnimation(float duration, SlideDirection direction, float offset, Ease ease = Ease.OutQuad)
        {
            mDuration = duration;
            mDirection = direction;
            mOffset = offset;
            mEase = ease;
        }

        public void Play(RectTransform target, Action onComplete = null)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                return;
            }

            Stop();
            mToPosition = target.anchoredPosition;
            var fromPosition = CalculateStartPosition(mToPosition);
            target.anchoredPosition = fromPosition;

            mTweener = target.DOAnchorPos(mToPosition, mDuration)
                .SetEase(mEase)
                .OnComplete(() => onComplete?.Invoke());
        }

        public void Stop()
        {
            mTweener?.Kill();
            mTweener = null;
        }

        public void Reset(RectTransform target)
        {
            if (target == null) return;
            var currentPos = target.anchoredPosition;
            target.anchoredPosition = CalculateStartPosition(currentPos);
        }

        public void SetToEndState(RectTransform target)
        {
            if (target == null) return;
            target.anchoredPosition = mToPosition;
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

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask PlayUniTaskAsync(RectTransform target, CancellationToken ct = default)
        {
            if (target == null) return;

            Stop();
            mToPosition = target.anchoredPosition;
            var fromPosition = CalculateStartPosition(mToPosition);
            target.anchoredPosition = fromPosition;

            var tcs = new UniTaskCompletionSource();
            
            mTweener = target.DOAnchorPos(mToPosition, mDuration)
                .SetEase(mEase)
                .OnComplete(() => tcs.TrySetResult())
                .OnKill(() => tcs.TrySetResult());

            await using (ct.Register(() =>
            {
                Stop();
                SetToEndState(target);
                tcs.TrySetCanceled(ct);
            }))
            {
                await tcs.Task;
            }
        }
#endif
    }
}
#endif
