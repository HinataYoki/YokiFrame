#if UNITY_2022_3_OR_NEWER
using System;
using UnityEngine;

namespace YokiFrame
{
    public abstract partial class UIPanel
    {
        [Header("动画配置")]
        [SerializeReference] protected UIAnimationConfig mShowAnimationConfig;
        [SerializeReference] protected UIAnimationConfig mHideAnimationConfig;
        private IUIAnimation mShowAnimation;
        private IUIAnimation mHideAnimation;
        private bool mAnimationConfigsInitialized;

        /// <summary>获取 Inspector 保存的显示动画配置。</summary>
        public UIAnimationConfig ShowAnimationConfig => mShowAnimationConfig;

        /// <summary>获取 Inspector 保存的隐藏动画配置。</summary>
        public UIAnimationConfig HideAnimationConfig => mHideAnimationConfig;

        /// <summary>替换显示动画；旧动画会先停止并释放运行资源。</summary>
        public void SetShowAnimation(IUIAnimation animation)
        {
            ReplaceAnimation(ref mShowAnimation, animation);
        }

        /// <summary>替换隐藏动画；旧动画会先停止并释放运行资源。</summary>
        public void SetHideAnimation(IUIAnimation animation)
        {
            ReplaceAnimation(ref mHideAnimation, animation);
        }

        /// <summary>获取当前显示动画。</summary>
        public IUIAnimation ShowAnimation => mShowAnimation;

        /// <summary>获取当前隐藏动画。</summary>
        public IUIAnimation HideAnimation => mHideAnimation;

        /// <summary>首次绑定 owner 时从序列化配置创建动画，不覆盖代码提前注入的实例。</summary>
        internal void InitializeAnimationConfigs()
        {
            if (mAnimationConfigsInitialized) return;
            mAnimationConfigsInitialized = true;
            if (mShowAnimation == null && mShowAnimationConfig != null)
                mShowAnimation = UIAnimationFactory.Create(mShowAnimationConfig);
            if (mHideAnimation == null && mHideAnimationConfig != null)
                mHideAnimation = UIAnimationFactory.Create(mHideAnimationConfig);
        }

        /// <summary>在面板 RectTransform 上播放显示动画。</summary>
        public void PlayShowAnimation()
        {
            if (mShowAnimation != null) mShowAnimation.Play(transform as RectTransform);
        }

        /// <summary>在面板 RectTransform 上播放隐藏动画。</summary>
        public void PlayHideAnimation()
        {
            if (mHideAnimation != null) mHideAnimation.Play(transform as RectTransform);
        }

        /// <summary>由 Controller 播放显示转换；返回 false 表示没有动画或启动失败。</summary>
        internal bool TryPlayShowAnimation(Action onComplete)
        {
            return TryPlayAnimation(mShowAnimation, onComplete);
        }

        /// <summary>由 Controller 播放隐藏转换；返回 false 表示没有动画或启动失败。</summary>
        internal bool TryPlayHideAnimation(Action onComplete)
        {
            return TryPlayAnimation(mHideAnimation, onComplete);
        }

        /// <summary>停止当前显示和隐藏动画，使旧 generation 的回调失效。</summary>
        internal void StopAnimations()
        {
            if (mShowAnimation != null) mShowAnimation.Stop();
            if (mHideAnimation != null) mHideAnimation.Stop();
        }

        /// <summary>销毁前停止并释放面板动画。</summary>
        private void ReleaseAnimations()
        {
            ReplaceAnimation(ref mShowAnimation, null);
            ReplaceAnimation(ref mHideAnimation, null);
        }

        /// <summary>替换一个动画引用并处理旧实例。</summary>
        private static void ReplaceAnimation(ref IUIAnimation current, IUIAnimation replacement)
        {
            if (ReferenceEquals(current, replacement)) return;
            if (current != null)
            {
                current.Stop();
                current.Recycle();
            }
            current = replacement;
        }

        /// <summary>隔离自定义动画启动异常，保证 Controller 可以同步提交终态。</summary>
        private bool TryPlayAnimation(IUIAnimation animation, Action onComplete)
        {
            if (animation == null) return false;
            try
            {
                animation.Play(transform as RectTransform, onComplete);
                return true;
            }
            catch (Exception exception)
            {
                LogKit.Exception(exception, this);
                return false;
            }
        }
    }
}
#endif
