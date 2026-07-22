#if UNITY_2022_3_OR_NEWER
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>从序列化配置或常用参数创建 UIKit 基础动画。</summary>
    public static class UIAnimationFactory
    {
        /// <summary>从非空配置创建独立动画实例。</summary>
        public static IUIAnimation Create(UIAnimationConfig config)
        {
            return config == null ? null : config.CreateAnimation();
        }

        /// <summary>创建空的并行组合动画。</summary>
        public static CompositeAnimation CreateParallel()
        {
            return new CompositeAnimation(CompositeMode.Parallel);
        }

        /// <summary>创建带初始子动画的并行组合动画。</summary>
        public static CompositeAnimation CreateParallel(IEnumerable<IUIAnimation> animations)
        {
            return CreateParallel().AddRange(animations);
        }

        /// <summary>创建空的顺序组合动画。</summary>
        public static CompositeAnimation CreateSequential()
        {
            return new CompositeAnimation(CompositeMode.Sequential);
        }

        /// <summary>创建带初始子动画的顺序组合动画。</summary>
        public static CompositeAnimation CreateSequential(IEnumerable<IUIAnimation> animations)
        {
            return CreateSequential().AddRange(animations);
        }

        /// <summary>按指定模式创建空组合动画。</summary>
        public static CompositeAnimation CreateComposite(CompositeMode mode)
        {
            return new CompositeAnimation(mode);
        }

        /// <summary>创建淡入动画。</summary>
        public static IUIAnimation CreateFadeIn(float duration = 0.3f)
        {
            return new FadeAnimation(duration, 0f, 1f);
        }

        /// <summary>创建淡出动画。</summary>
        public static IUIAnimation CreateFadeOut(float duration = 0.3f)
        {
            return new FadeAnimation(duration, 1f, 0f);
        }

        /// <summary>创建从零缩放到正常大小的弹出动画。</summary>
        public static IUIAnimation CreatePopIn(float duration = 0.3f)
        {
            return new ScaleAnimation(duration, Vector3.zero, Vector3.one);
        }

        /// <summary>创建从正常大小收缩到零的动画。</summary>
        public static IUIAnimation CreatePopOut(float duration = 0.3f)
        {
            return new ScaleAnimation(duration, Vector3.one, Vector3.zero);
        }

        /// <summary>创建从底部滑入到当前位置的动画。</summary>
        public static IUIAnimation CreateSlideInFromBottom(float duration = 0.3f, float offset = 100f)
        {
            return new SlideAnimation(duration, SlideDirection.Bottom, offset);
        }

        /// <summary>创建从顶部滑入到当前位置的动画。</summary>
        public static IUIAnimation CreateSlideInFromTop(float duration = 0.3f, float offset = 100f)
        {
            return new SlideAnimation(duration, SlideDirection.Top, offset);
        }

        /// <summary>停止并释放一个可空动画。</summary>
        public static void Return(IUIAnimation animation)
        {
            if (animation != null) animation.Recycle();
        }
    }
}
#endif
