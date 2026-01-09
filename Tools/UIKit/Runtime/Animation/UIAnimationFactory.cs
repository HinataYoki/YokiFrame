using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UI 动画工厂
    /// </summary>
    public static class UIAnimationFactory
    {
        /// <summary>
        /// 从配置创建动画
        /// </summary>
        public static IUIAnimation Create(UIAnimationConfig config)
        {
            if (config == null) return null;

#if YOKIFRAME_DOTWEEN_SUPPORT
            return CreateDOTweenAnimation(config);
#else
            return config.CreateAnimation();
#endif
        }

#if YOKIFRAME_DOTWEEN_SUPPORT
        private static IUIAnimation CreateDOTweenAnimation(UIAnimationConfig config)
        {
            return config switch
            {
                FadeAnimationConfig fadeConfig => new DOTweenFadeAnimation(fadeConfig),
                ScaleAnimationConfig scaleConfig => new DOTweenScaleAnimation(scaleConfig),
                SlideAnimationConfig slideConfig => new DOTweenSlideAnimation(slideConfig),
                _ => config.CreateAnimation()
            };
        }
#endif

        /// <summary>
        /// 创建淡入动画
        /// </summary>
        public static IUIAnimation CreateFadeIn(float duration = 0.3f)
        {
            var config = new FadeAnimationConfig
            {
                Duration = duration,
                FromAlpha = 0f,
                ToAlpha = 1f
            };
            return Create(config);
        }

        /// <summary>
        /// 创建淡出动画
        /// </summary>
        public static IUIAnimation CreateFadeOut(float duration = 0.3f)
        {
            var config = new FadeAnimationConfig
            {
                Duration = duration,
                FromAlpha = 1f,
                ToAlpha = 0f
            };
            return Create(config);
        }

        /// <summary>
        /// 创建弹出缩放动画
        /// </summary>
        public static IUIAnimation CreatePopIn(float duration = 0.3f)
        {
            var config = new ScaleAnimationConfig
            {
                Duration = duration,
                FromScale = Vector3.zero,
                ToScale = Vector3.one
            };
            return Create(config);
        }

        /// <summary>
        /// 创建收缩动画
        /// </summary>
        public static IUIAnimation CreatePopOut(float duration = 0.3f)
        {
            var config = new ScaleAnimationConfig
            {
                Duration = duration,
                FromScale = Vector3.one,
                ToScale = Vector3.zero
            };
            return Create(config);
        }

        /// <summary>
        /// 创建从底部滑入动画
        /// </summary>
        public static IUIAnimation CreateSlideInFromBottom(float duration = 0.3f, float offset = 100f)
        {
            var config = new SlideAnimationConfig
            {
                Duration = duration,
                Direction = SlideDirection.Bottom,
                Offset = offset
            };
            return Create(config);
        }

        /// <summary>
        /// 创建从顶部滑入动画
        /// </summary>
        public static IUIAnimation CreateSlideInFromTop(float duration = 0.3f, float offset = 100f)
        {
            var config = new SlideAnimationConfig
            {
                Duration = duration,
                Direction = SlideDirection.Top,
                Offset = offset
            };
            return Create(config);
        }

        /// <summary>
        /// 创建组合动画（并行）
        /// </summary>
        public static CompositeAnimation CreateParallel(params IUIAnimation[] animations)
        {
            var composite = new CompositeAnimation(CompositeMode.Parallel);
            foreach (var anim in animations)
            {
                composite.Add(anim);
            }
            return composite;
        }

        /// <summary>
        /// 创建组合动画（顺序）
        /// </summary>
        public static CompositeAnimation CreateSequential(params IUIAnimation[] animations)
        {
            var composite = new CompositeAnimation(CompositeMode.Sequential);
            foreach (var anim in animations)
            {
                composite.Add(anim);
            }
            return composite;
        }
    }
}
