using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UI 动画工厂
    /// </summary>
    public static class UIAnimationFactory
    {
        #region 从配置创建
        
        /// <summary>
        /// 从配置创建动画（使用池化对象）
        /// </summary>
        public static IUIAnimation Create(UIAnimationConfig config)
        {
            if (config == default) return null;

#if YOKIFRAME_DOTWEEN_SUPPORT
            return CreateDOTweenAnimation(config);
#else
            return CreateCoroutineAnimation(config);
#endif
        }

#if YOKIFRAME_DOTWEEN_SUPPORT
        private static IUIAnimation CreateDOTweenAnimation(UIAnimationConfig config)
        {
            return config switch
            {
                FadeAnimationConfig fadeConfig => SafePoolKit<DOTweenFadeAnimation>.Instance.Allocate().Setup(fadeConfig),
                ScaleAnimationConfig scaleConfig => SafePoolKit<DOTweenScaleAnimation>.Instance.Allocate().Setup(scaleConfig),
                SlideAnimationConfig slideConfig => SafePoolKit<DOTweenSlideAnimation>.Instance.Allocate().Setup(slideConfig),
                _ => config.CreateAnimation()
            };
        }
#else
        private static IUIAnimation CreateCoroutineAnimation(UIAnimationConfig config)
        {
            return config switch
            {
                FadeAnimationConfig fadeConfig => SafePoolKit<FadeAnimation>.Instance.Allocate().Setup(fadeConfig),
                ScaleAnimationConfig scaleConfig => SafePoolKit<ScaleAnimation>.Instance.Allocate().Setup(scaleConfig),
                SlideAnimationConfig slideConfig => SafePoolKit<SlideAnimation>.Instance.Allocate().Setup(slideConfig),
                _ => config.CreateAnimation()
            };
        }
#endif

        #endregion

        #region 组合动画

        /// <summary>
        /// 创建并行组合动画
        /// </summary>
        public static CompositeAnimation CreateParallel() 
            => SafePoolKit<CompositeAnimation>.Instance.Allocate().Setup(CompositeMode.Parallel);

        /// <summary>
        /// 创建顺序组合动画
        /// </summary>
        public static CompositeAnimation CreateSequential() 
            => SafePoolKit<CompositeAnimation>.Instance.Allocate().Setup(CompositeMode.Sequential);

        /// <summary>
        /// 创建组合动画（通用）
        /// </summary>
        public static CompositeAnimation CreateComposite(CompositeMode mode) 
            => SafePoolKit<CompositeAnimation>.Instance.Allocate().Setup(mode);

        /// <summary>
        /// 创建并行组合动画（带初始动画列表）
        /// </summary>
        public static CompositeAnimation CreateParallel(List<IUIAnimation> animations)
        {
            var composite = SafePoolKit<CompositeAnimation>.Instance.Allocate().Setup(CompositeMode.Parallel);
            composite.AddRange(animations);
            return composite;
        }

        /// <summary>
        /// 创建顺序组合动画（带初始动画列表）
        /// </summary>
        public static CompositeAnimation CreateSequential(List<IUIAnimation> animations)
        {
            var composite = SafePoolKit<CompositeAnimation>.Instance.Allocate().Setup(CompositeMode.Sequential);
            composite.AddRange(animations);
            return composite;
        }

        #endregion

        #region 快捷创建方法

        /// <summary>
        /// 创建淡入动画
        /// </summary>
        public static IUIAnimation CreateFadeIn(float duration = 0.3f)
        {
#if YOKIFRAME_DOTWEEN_SUPPORT
            return SafePoolKit<DOTweenFadeAnimation>.Instance.Allocate().Setup(duration, 0f, 1f);
#else
            return SafePoolKit<FadeAnimation>.Instance.Allocate().Setup(duration, 0f, 1f);
#endif
        }

        /// <summary>
        /// 创建淡出动画
        /// </summary>
        public static IUIAnimation CreateFadeOut(float duration = 0.3f)
        {
#if YOKIFRAME_DOTWEEN_SUPPORT
            return SafePoolKit<DOTweenFadeAnimation>.Instance.Allocate().Setup(duration, 1f, 0f);
#else
            return SafePoolKit<FadeAnimation>.Instance.Allocate().Setup(duration, 1f, 0f);
#endif
        }

        /// <summary>
        /// 创建弹出缩放动画
        /// </summary>
        public static IUIAnimation CreatePopIn(float duration = 0.3f)
        {
#if YOKIFRAME_DOTWEEN_SUPPORT
            return SafePoolKit<DOTweenScaleAnimation>.Instance.Allocate().Setup(duration, Vector3.zero, Vector3.one);
#else
            return SafePoolKit<ScaleAnimation>.Instance.Allocate().Setup(duration, Vector3.zero, Vector3.one);
#endif
        }

        /// <summary>
        /// 创建收缩动画
        /// </summary>
        public static IUIAnimation CreatePopOut(float duration = 0.3f)
        {
#if YOKIFRAME_DOTWEEN_SUPPORT
            return SafePoolKit<DOTweenScaleAnimation>.Instance.Allocate().Setup(duration, Vector3.one, Vector3.zero);
#else
            return SafePoolKit<ScaleAnimation>.Instance.Allocate().Setup(duration, Vector3.one, Vector3.zero);
#endif
        }

        /// <summary>
        /// 创建从底部滑入动画
        /// </summary>
        public static IUIAnimation CreateSlideInFromBottom(float duration = 0.3f, float offset = 100f)
        {
#if YOKIFRAME_DOTWEEN_SUPPORT
            return SafePoolKit<DOTweenSlideAnimation>.Instance.Allocate().Setup(duration, SlideDirection.Bottom, offset);
#else
            return SafePoolKit<SlideAnimation>.Instance.Allocate().Setup(duration, SlideDirection.Bottom, offset);
#endif
        }

        /// <summary>
        /// 创建从顶部滑入动画
        /// </summary>
        public static IUIAnimation CreateSlideInFromTop(float duration = 0.3f, float offset = 100f)
        {
#if YOKIFRAME_DOTWEEN_SUPPORT
            return SafePoolKit<DOTweenSlideAnimation>.Instance.Allocate().Setup(duration, SlideDirection.Top, offset);
#else
            return SafePoolKit<SlideAnimation>.Instance.Allocate().Setup(duration, SlideDirection.Top, offset);
#endif
        }

        #endregion

        #region 归还

        /// <summary>
        /// 归还动画到池（通过接口统一调用）
        /// </summary>
        public static void Return(IUIAnimation animation) => animation?.Recycle();

        #endregion
    }
}
