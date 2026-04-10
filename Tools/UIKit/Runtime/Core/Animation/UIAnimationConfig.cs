using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 动画配置基类
    /// </summary>
    [Serializable]
    public abstract class UIAnimationConfig
    {
        /// <summary>
        /// 动画时长（秒）
        /// </summary>
        [Tooltip("动画时长（秒）")]
        public float Duration = 0.3f;
        
        /// <summary>
        /// 动画曲线
        /// </summary>
        [Tooltip("动画曲线")]
        public AnimationCurve Curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        /// <summary>
        /// 创建动画实例
        /// </summary>
        public abstract IUIAnimation CreateAnimation();
    }

    /// <summary>
    /// 滑动方向
    /// </summary>
    public enum SlideDirection
    {
        Top,
        Bottom,
        Left,
        Right
    }

    /// <summary>
    /// 淡入淡出动画配置
    /// </summary>
    [Serializable]
    public class FadeAnimationConfig : UIAnimationConfig
    {
        /// <summary>
        /// 起始透明度
        /// </summary>
        [Range(0f, 1f)]
        [Tooltip("起始透明度")]
        public float FromAlpha = 0f;
        
        /// <summary>
        /// 目标透明度
        /// </summary>
        [Range(0f, 1f)]
        [Tooltip("目标透明度")]
        public float ToAlpha = 1f;

        public override IUIAnimation CreateAnimation()
        {
            return SafePoolKit<FadeAnimation>.Instance.Allocate().Setup(this);
        }
    }

    /// <summary>
    /// 缩放动画配置
    /// </summary>
    [Serializable]
    public class ScaleAnimationConfig : UIAnimationConfig
    {
        /// <summary>
        /// 起始缩放
        /// </summary>
        [Tooltip("起始缩放")]
        public Vector3 FromScale = Vector3.zero;
        
        /// <summary>
        /// 目标缩放
        /// </summary>
        [Tooltip("目标缩放")]
        public Vector3 ToScale = Vector3.one;

        public override IUIAnimation CreateAnimation()
        {
            return SafePoolKit<ScaleAnimation>.Instance.Allocate().Setup(this);
        }
    }

    /// <summary>
    /// 滑动动画配置
    /// </summary>
    [Serializable]
    public class SlideAnimationConfig : UIAnimationConfig
    {
        /// <summary>
        /// 滑动方向
        /// </summary>
        [Tooltip("滑动方向")]
        public SlideDirection Direction = SlideDirection.Bottom;
        
        /// <summary>
        /// 滑动偏移量
        /// </summary>
        [Tooltip("滑动偏移量（像素）")]
        public float Offset = 100f;

        public override IUIAnimation CreateAnimation()
        {
            return SafePoolKit<SlideAnimation>.Instance.Allocate().Setup(this);
        }
    }
}
