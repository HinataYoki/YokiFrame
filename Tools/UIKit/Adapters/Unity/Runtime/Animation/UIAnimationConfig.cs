#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>可由 Unity SerializeReference 保存的 UIKit 动画配置。</summary>
    [Serializable]
    public abstract class UIAnimationConfig
    {
        [Min(0f), Tooltip("动画时长（秒）")]
        public float Duration = 0.3f;

        [Tooltip("归一化动画进度曲线")]
        public AnimationCurve Curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        /// <summary>基于当前配置创建独立动画实例。</summary>
        public abstract IUIAnimation CreateAnimation();

        /// <summary>
        /// 按剩余嵌套深度创建动画；叶子配置忽略该参数。
        /// </summary>
        /// <param name="remainingDepth">还可继续下钻的层数。</param>
        /// <returns>创建的动画实例。</returns>
        internal virtual IUIAnimation CreateAnimation(int remainingDepth)
        {
            return CreateAnimation();
        }
    }

    /// <summary>滑入动画相对目标终点的起始方向。</summary>
    public enum SlideDirection
    {
        Top,
        Bottom,
        Left,
        Right
    }

    /// <summary>淡入淡出配置。</summary>
    [Serializable]
    public sealed class FadeAnimationConfig : UIAnimationConfig
    {
        [Range(0f, 1f), Tooltip("起始透明度")]
        public float FromAlpha;

        [Range(0f, 1f), Tooltip("目标透明度")]
        public float ToAlpha = 1f;

        /// <inheritdoc />
        public override IUIAnimation CreateAnimation()
        {
            return new FadeAnimation(Duration, FromAlpha, ToAlpha, Curve);
        }
    }

    /// <summary>缩放动画配置。</summary>
    [Serializable]
    public sealed class ScaleAnimationConfig : UIAnimationConfig
    {
        [Tooltip("起始缩放")]
        public Vector3 FromScale = Vector3.zero;

        [Tooltip("目标缩放")]
        public Vector3 ToScale = Vector3.one;

        /// <inheritdoc />
        public override IUIAnimation CreateAnimation()
        {
            return new ScaleAnimation(Duration, FromScale, ToScale, Curve);
        }
    }

    /// <summary>以目标当前位置为终点的方向滑入配置。</summary>
    [Serializable]
    public sealed class SlideAnimationConfig : UIAnimationConfig
    {
        [Tooltip("动画起点相对终点的方向")]
        public SlideDirection Direction = SlideDirection.Bottom;

        [Min(0f), Tooltip("起点距离终点的像素偏移")]
        public float Offset = 100f;

        /// <inheritdoc />
        public override IUIAnimation CreateAnimation()
        {
            return new SlideAnimation(Duration, Direction, Offset, Curve);
        }
    }

    /// <summary>可序列化的并行或顺序组合动画配置。</summary>
    [Serializable]
    public sealed class CompositeAnimationConfig : UIAnimationConfig
    {
        [Tooltip("子动画播放模式")]
        public CompositeMode Mode;

        [SerializeReference, Tooltip("按列表顺序保存的多态子动画")]
        public List<UIAnimationConfig> Animations = new();

        /// <summary>
        /// 组合配置允许的最大嵌套层数；远大于任何真实 UI 动画层级，仅用于阻断自引用环。
        /// </summary>
        /// <remarks>
        /// Unity 默认 Inspector 允许在 <c>[SerializeReference]</c> 列表里把配置拖到自身或祖先上形成环；
        /// 无守卫时 <see cref="CreateAnimation()"/> 会无界递归并以不可捕获的栈溢出终止 Player。
        /// </remarks>
        private const int MAX_NESTING_DEPTH = 32;

        /// <inheritdoc />
        public override IUIAnimation CreateAnimation()
        {
            return CreateAnimation(MAX_NESTING_DEPTH);
        }

        /// <summary>
        /// 按剩余深度递归创建子动画；本层已是最后允许层时不再下钻，从而忽略超限子树。
        /// </summary>
        /// <param name="remainingDepth">含本层在内还可创建的层数。</param>
        /// <returns>组合动画实例；达到上限时为空组合。</returns>
        internal override IUIAnimation CreateAnimation(int remainingDepth)
        {
            var composite = new CompositeAnimation(Mode);
            if (remainingDepth <= 1) return composite;

            for (var index = 0; index < Animations.Count; index++)
            {
                UIAnimationConfig config = Animations[index];
                if (config != null) composite.Add(config.CreateAnimation(remainingDepth - 1));
            }

            return composite;
        }
    }
}
#endif
