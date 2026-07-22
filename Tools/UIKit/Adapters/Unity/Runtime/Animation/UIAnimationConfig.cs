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

        /// <inheritdoc />
        public override IUIAnimation CreateAnimation()
        {
            var composite = new CompositeAnimation(Mode);
            for (var index = 0; index < Animations.Count; index++)
            {
                UIAnimationConfig config = Animations[index];
                if (config != null) composite.Add(config.CreateAnimation());
            }
            return composite;
        }
    }
}
#endif
