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
    public class DOTweenFadeAnimation : IUIAnimation, IPoolable
#if YOKIFRAME_UNITASK_SUPPORT
        , IUIAnimationUniTask
#endif
    {
        private float mDuration;
        private float mFromAlpha;
        private float mToAlpha;
        private Ease mEase;
        private Tweener mTweener;
        private CanvasGroup mCanvasGroup;
        
        private Action<object> mOnCompleteCallback;
        private object mCallbackState;

        #region IPoolable
        
        public bool IsRecycled { get; set; }
        
        void IPoolable.OnRecycled()
        {
            Stop();
            mCanvasGroup = null;
        }
        
        #endregion

        public float Duration => mDuration;
        public bool IsPlaying => mTweener != default && mTweener.IsPlaying();

        public DOTweenFadeAnimation() { }

        public DOTweenFadeAnimation(FadeAnimationConfig config, Ease ease = Ease.OutQuad)
        {
            Setup(config, ease);
        }

        public DOTweenFadeAnimation(float duration, float fromAlpha, float toAlpha, Ease ease = Ease.OutQuad)
        {
            Setup(duration, fromAlpha, toAlpha, ease);
        }

        internal DOTweenFadeAnimation Setup(FadeAnimationConfig config, Ease ease = Ease.OutQuad)
        {
            mDuration = config.Duration;
            mFromAlpha = config.FromAlpha;
            mToAlpha = config.ToAlpha;
            mEase = ease;
            return this;
        }

        internal DOTweenFadeAnimation Setup(float duration, float fromAlpha, float toAlpha, Ease ease = Ease.OutQuad)
        {
            mDuration = duration;
            mFromAlpha = fromAlpha;
            mToAlpha = toAlpha;
            mEase = ease;
            return this;
        }

        public void Play(RectTransform target, Action onComplete = null)
        {
            Play(target, onComplete == default ? null : static state => ((Action)state)?.Invoke(), onComplete);
        }
        
        public void Play(RectTransform target, Action<object> onComplete, object state)
        {
            if (target == default)
            {
                onComplete?.Invoke(state);
                return;
            }

            Stop();
            
            if (mCanvasGroup == default || mCanvasGroup.gameObject != target.gameObject)
            {
                mCanvasGroup = EnsureCanvasGroup(target);
            }
            
            mCanvasGroup.alpha = mFromAlpha;
            mOnCompleteCallback = onComplete;
            mCallbackState = state;

            mTweener = mCanvasGroup.DOFade(mToAlpha, mDuration)
                .SetEase(mEase)
                .OnComplete(OnTweenComplete);
        }
        
        private void OnTweenComplete()
        {
            var callback = mOnCompleteCallback;
            var state = mCallbackState;
            mOnCompleteCallback = null;
            mCallbackState = null;
            callback?.Invoke(state);
        }

        public void Stop()
        {
            mTweener?.Kill();
            mTweener = null;
            mOnCompleteCallback = null;
            mCallbackState = null;
        }

        public void Reset(RectTransform target)
        {
            if (target == default) return;
            
            if (mCanvasGroup == default || mCanvasGroup.gameObject != target.gameObject)
            {
                mCanvasGroup = EnsureCanvasGroup(target);
            }
            mCanvasGroup.alpha = mFromAlpha;
        }

        public void SetToEndState(RectTransform target)
        {
            if (target == default) return;
            
            if (mCanvasGroup == default || mCanvasGroup.gameObject != target.gameObject)
            {
                mCanvasGroup = EnsureCanvasGroup(target);
            }
            mCanvasGroup.alpha = mToAlpha;
        }

        private static CanvasGroup EnsureCanvasGroup(RectTransform target)
        {
            if (target.TryGetComponent<CanvasGroup>(out var canvasGroup))
            {
                return canvasGroup;
            }
            return target.gameObject.AddComponent<CanvasGroup>();
        }
        
        public void Recycle() => SafePoolKit<DOTweenFadeAnimation>.Instance.Recycle(this);

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask PlayUniTaskAsync(RectTransform target, CancellationToken ct = default)
        {
            if (target == default) return;

            Stop();
            
            if (mCanvasGroup == default || mCanvasGroup.gameObject != target.gameObject)
            {
                mCanvasGroup = EnsureCanvasGroup(target);
            }
            
            mCanvasGroup.alpha = mFromAlpha;

            // 手动处理 DOTween 异步（兼容免费版）
            var tcs = AutoResetUniTaskCompletionSource.Create();
            var tween = mCanvasGroup.DOFade(mToAlpha, mDuration)
                .SetEase(mEase)
                .SetLink(target.gameObject)
                .OnComplete(() => tcs.TrySetResult());
            
            // 注册取消回调
            using var registration = ct.Register(static state => ((Tweener)state).Kill(), tween);
            
            await tcs.Task;
        }
#endif
    }

    /// <summary>
    /// DOTween 缩放动画
    /// </summary>
    public class DOTweenScaleAnimation : IUIAnimation, IPoolable
#if YOKIFRAME_UNITASK_SUPPORT
        , IUIAnimationUniTask
#endif
    {
        private float mDuration;
        private Vector3 mFromScale;
        private Vector3 mToScale;
        private Ease mEase;
        private Tweener mTweener;
        
        private Action<object> mOnCompleteCallback;
        private object mCallbackState;

        #region IPoolable
        
        public bool IsRecycled { get; set; }
        
        void IPoolable.OnRecycled() => Stop();
        
        #endregion

        public float Duration => mDuration;
        public bool IsPlaying => mTweener != default && mTweener.IsPlaying();

        public DOTweenScaleAnimation() { }

        public DOTweenScaleAnimation(ScaleAnimationConfig config, Ease ease = Ease.OutBack)
        {
            Setup(config, ease);
        }

        public DOTweenScaleAnimation(float duration, Vector3 fromScale, Vector3 toScale, Ease ease = Ease.OutBack)
        {
            Setup(duration, fromScale, toScale, ease);
        }

        internal DOTweenScaleAnimation Setup(ScaleAnimationConfig config, Ease ease = Ease.OutBack)
        {
            mDuration = config.Duration;
            mFromScale = config.FromScale;
            mToScale = config.ToScale;
            mEase = ease;
            return this;
        }

        internal DOTweenScaleAnimation Setup(float duration, Vector3 fromScale, Vector3 toScale, Ease ease = Ease.OutBack)
        {
            mDuration = duration;
            mFromScale = fromScale;
            mToScale = toScale;
            mEase = ease;
            return this;
        }

        public void Play(RectTransform target, Action onComplete = null)
        {
            Play(target, onComplete == default ? null : static state => ((Action)state)?.Invoke(), onComplete);
        }
        
        public void Play(RectTransform target, Action<object> onComplete, object state)
        {
            if (target == default)
            {
                onComplete?.Invoke(state);
                return;
            }

            Stop();
            target.localScale = mFromScale;
            
            mOnCompleteCallback = onComplete;
            mCallbackState = state;

            mTweener = target.DOScale(mToScale, mDuration)
                .SetEase(mEase)
                .OnComplete(OnTweenComplete);
        }
        
        private void OnTweenComplete()
        {
            var callback = mOnCompleteCallback;
            var state = mCallbackState;
            mOnCompleteCallback = null;
            mCallbackState = null;
            callback?.Invoke(state);
        }

        public void Stop()
        {
            mTweener?.Kill();
            mTweener = null;
            mOnCompleteCallback = null;
            mCallbackState = null;
        }

        public void Reset(RectTransform target)
        {
            if (target == default) return;
            target.localScale = mFromScale;
        }

        public void SetToEndState(RectTransform target)
        {
            if (target == default) return;
            target.localScale = mToScale;
        }
        
        public void Recycle() => SafePoolKit<DOTweenScaleAnimation>.Instance.Recycle(this);

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask PlayUniTaskAsync(RectTransform target, CancellationToken ct = default)
        {
            if (target == default) return;

            Stop();
            target.localScale = mFromScale;

            // 手动处理 DOTween 异步（兼容免费版）
            var tcs = AutoResetUniTaskCompletionSource.Create();
            var tween = target.DOScale(mToScale, mDuration)
                .SetEase(mEase)
                .SetLink(target.gameObject)
                .OnComplete(() => tcs.TrySetResult());
            
            // 注册取消回调
            using var registration = ct.Register(static state => ((Tweener)state).Kill(), tween);
            
            await tcs.Task;
        }
#endif
    }

    /// <summary>
    /// DOTween 滑动动画
    /// </summary>
    public class DOTweenSlideAnimation : IUIAnimation, IPoolable
#if YOKIFRAME_UNITASK_SUPPORT
        , IUIAnimationUniTask
#endif
    {
        private float mDuration;
        private SlideDirection mDirection;
        private float mOffset;
        private Ease mEase;
        private Tweener mTweener;
        private Vector2 mToPosition;
        
        private Action<object> mOnCompleteCallback;
        private object mCallbackState;

        #region IPoolable
        
        public bool IsRecycled { get; set; }
        
        void IPoolable.OnRecycled() => Stop();
        
        #endregion

        public float Duration => mDuration;
        public bool IsPlaying => mTweener != default && mTweener.IsPlaying();

        public DOTweenSlideAnimation() { }

        public DOTweenSlideAnimation(SlideAnimationConfig config, Ease ease = Ease.OutQuad)
        {
            Setup(config, ease);
        }

        public DOTweenSlideAnimation(float duration, SlideDirection direction, float offset, Ease ease = Ease.OutQuad)
        {
            Setup(duration, direction, offset, ease);
        }

        internal DOTweenSlideAnimation Setup(SlideAnimationConfig config, Ease ease = Ease.OutQuad)
        {
            mDuration = config.Duration;
            mDirection = config.Direction;
            mOffset = config.Offset;
            mEase = ease;
            return this;
        }

        internal DOTweenSlideAnimation Setup(float duration, SlideDirection direction, float offset, Ease ease = Ease.OutQuad)
        {
            mDuration = duration;
            mDirection = direction;
            mOffset = offset;
            mEase = ease;
            return this;
        }

        public void Play(RectTransform target, Action onComplete = null)
        {
            Play(target, onComplete == default ? null : static state => ((Action)state)?.Invoke(), onComplete);
        }
        
        public void Play(RectTransform target, Action<object> onComplete, object state)
        {
            if (target == default)
            {
                onComplete?.Invoke(state);
                return;
            }

            Stop();
            mToPosition = target.anchoredPosition;
            var fromPosition = CalculateStartPosition(mToPosition);
            target.anchoredPosition = fromPosition;
            
            mOnCompleteCallback = onComplete;
            mCallbackState = state;

            mTweener = target.DOAnchorPos(mToPosition, mDuration)
                .SetEase(mEase)
                .OnComplete(OnTweenComplete);
        }
        
        private void OnTweenComplete()
        {
            var callback = mOnCompleteCallback;
            var state = mCallbackState;
            mOnCompleteCallback = null;
            mCallbackState = null;
            callback?.Invoke(state);
        }

        public void Stop()
        {
            mTweener?.Kill();
            mTweener = null;
            mOnCompleteCallback = null;
            mCallbackState = null;
        }

        public void Reset(RectTransform target)
        {
            if (target == default) return;
            var currentPos = target.anchoredPosition;
            target.anchoredPosition = CalculateStartPosition(currentPos);
        }

        public void SetToEndState(RectTransform target)
        {
            if (target == default) return;
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
        
        public void Recycle() => SafePoolKit<DOTweenSlideAnimation>.Instance.Recycle(this);

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask PlayUniTaskAsync(RectTransform target, CancellationToken ct = default)
        {
            if (target == default) return;

            Stop();
            mToPosition = target.anchoredPosition;
            var fromPosition = CalculateStartPosition(mToPosition);
            target.anchoredPosition = fromPosition;

            // 手动处理 DOTween 异步（兼容免费版）
            var tcs = AutoResetUniTaskCompletionSource.Create();
            var tween = target.DOAnchorPos(mToPosition, mDuration)
                .SetEase(mEase)
                .SetLink(target.gameObject)
                .OnComplete(() => tcs.TrySetResult());
            
            // 注册取消回调
            using var registration = ct.Register(static state => ((Tweener)state).Kill(), tween);
            
            await tcs.Task;
        }
#endif
    }
}
#endif
