#if UNITY_2022_3_OR_NEWER && YOKIFRAME_DOTWEEN_SUPPORT
using System;
using DG.Tweening;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>DOTween 动画共享生命周期，实现取消、状态查询和回调提交。</summary>
    public abstract class DOTweenUIAnimationBase : IUIAnimation
    {
        private Tween mTween;
        private Action mOnComplete;

        /// <summary>创建指定时长的 DOTween UI 动画。</summary>
        protected DOTweenUIAnimationBase(float duration)
        {
            Duration = Mathf.Max(0f, duration);
        }

        /// <inheritdoc />
        public float Duration { get; }

        /// <inheritdoc />
        public bool IsPlaying => mTween != null && mTween.IsActive() && mTween.IsPlaying();

        /// <inheritdoc />
        public void Play(RectTransform target, Action onComplete = null)
        {
            Stop();
            if (target == null)
            {
                if (onComplete != null) onComplete();
                return;
            }
            Reset(target);
            mOnComplete = onComplete;
            mTween = CreateTween(target)
                .SetUpdate(true)
                .SetTarget(target)
                .OnComplete(Complete);
        }

        /// <inheritdoc />
        public void Stop()
        {
            Tween tween = mTween;
            mTween = null;
            mOnComplete = null;
            if (tween != null && tween.IsActive()) tween.Kill(false);
        }

        /// <inheritdoc />
        public abstract void Reset(RectTransform target);

        /// <inheritdoc />
        public abstract void SetToEndState(RectTransform target);

        /// <inheritdoc />
        public void Recycle()
        {
            Stop();
        }

        /// <summary>由具体动画创建已配置的 Tween。</summary>
        protected abstract Tween CreateTween(RectTransform target);

        /// <summary>清理 Tween 引用并执行一次完成回调。</summary>
        private void Complete()
        {
            mTween = null;
            Action callback = mOnComplete;
            mOnComplete = null;
            if (callback != null) callback();
        }
    }

    /// <summary>使用 DOTween 对 CanvasGroup alpha 执行淡入淡出。</summary>
    public sealed class DOTweenFadeAnimation : DOTweenUIAnimationBase
    {
        private readonly float mFromAlpha;
        private readonly float mToAlpha;
        private readonly Ease mEase;

        /// <summary>创建 DOTween 淡入淡出动画。</summary>
        public DOTweenFadeAnimation(float duration, float fromAlpha, float toAlpha, Ease ease = Ease.OutQuad)
            : base(duration)
        {
            mFromAlpha = Mathf.Clamp01(fromAlpha);
            mToAlpha = Mathf.Clamp01(toAlpha);
            mEase = ease;
        }

        /// <inheritdoc />
        public override void Reset(RectTransform target)
        {
            if (target != null) EnsureCanvasGroup(target).alpha = mFromAlpha;
        }

        /// <inheritdoc />
        public override void SetToEndState(RectTransform target)
        {
            if (target != null) EnsureCanvasGroup(target).alpha = mToAlpha;
        }

        /// <inheritdoc />
        protected override Tween CreateTween(RectTransform target)
        {
            CanvasGroup group = EnsureCanvasGroup(target);
            // 直接调用 DOTween 核心 To，避免依赖可选的 DOTween.Modules asmdef。
            return DOTween.To(() => group.alpha, value => group.alpha = value, mToAlpha, Duration)
                .SetEase(mEase);
        }

        /// <summary>获取或创建目标上的 CanvasGroup。</summary>
        private static CanvasGroup EnsureCanvasGroup(RectTransform target)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.gameObject.AddComponent<CanvasGroup>();
        }
    }

    /// <summary>使用 DOTween 对 RectTransform 执行缩放。</summary>
    public sealed class DOTweenScaleAnimation : DOTweenUIAnimationBase
    {
        private readonly Vector3 mFromScale;
        private readonly Vector3 mToScale;
        private readonly Ease mEase;

        /// <summary>创建 DOTween 缩放动画。</summary>
        public DOTweenScaleAnimation(float duration, Vector3 fromScale, Vector3 toScale, Ease ease = Ease.OutBack)
            : base(duration)
        {
            mFromScale = fromScale;
            mToScale = toScale;
            mEase = ease;
        }

        /// <inheritdoc />
        public override void Reset(RectTransform target)
        {
            if (target != null) target.localScale = mFromScale;
        }

        /// <inheritdoc />
        public override void SetToEndState(RectTransform target)
        {
            if (target != null) target.localScale = mToScale;
        }

        /// <inheritdoc />
        protected override Tween CreateTween(RectTransform target)
        {
            return target.DOScale(mToScale, Duration).SetEase(mEase);
        }
    }

    /// <summary>使用 DOTween 对 RectTransform anchoredPosition 执行滑动。</summary>
    public sealed class DOTweenSlideAnimation : DOTweenUIAnimationBase
    {
        private readonly Vector2 mFromPosition;
        private readonly Vector2 mToPosition;
        private readonly Ease mEase;

        /// <summary>创建 DOTween 滑动动画。</summary>
        public DOTweenSlideAnimation(float duration, Vector2 fromPosition, Vector2 toPosition, Ease ease = Ease.OutQuad)
            : base(duration)
        {
            mFromPosition = fromPosition;
            mToPosition = toPosition;
            mEase = ease;
        }

        /// <inheritdoc />
        public override void Reset(RectTransform target)
        {
            if (target != null) target.anchoredPosition = mFromPosition;
        }

        /// <inheritdoc />
        public override void SetToEndState(RectTransform target)
        {
            if (target != null) target.anchoredPosition = mToPosition;
        }

        /// <inheritdoc />
        protected override Tween CreateTween(RectTransform target)
        {
            // anchoredPosition 属于 Unity UI 属性，使用 DOTween 核心 To 可兼容未执行 Setup 的安装形态。
            return DOTween.To(
                    () => target.anchoredPosition,
                    value => target.anchoredPosition = value,
                    mToPosition,
                    Duration)
                .SetEase(mEase);
        }
    }
}
#endif
