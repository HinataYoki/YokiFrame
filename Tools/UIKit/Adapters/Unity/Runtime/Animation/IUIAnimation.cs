#if UNITY_2022_3_OR_NEWER
using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>UIKit 基础 UI 动画契约；实现不依赖第三方 Tween 库。</summary>
    public interface IUIAnimation
    {
        /// <summary>获取动画时长（秒）。</summary>
        float Duration { get; }

        /// <summary>判断动画是否正在播放。</summary>
        bool IsPlaying { get; }

        /// <summary>在目标 RectTransform 上播放动画。</summary>
        void Play(RectTransform target, Action onComplete = null);

        /// <summary>停止动画并保留当前状态。</summary>
        void Stop();

        /// <summary>将目标恢复到动画起始状态。</summary>
        void Reset(RectTransform target);

        /// <summary>将目标设置到动画结束状态。</summary>
        void SetToEndState(RectTransform target);

        /// <summary>释放当前动画占用的运行资源。</summary>
        void Recycle();
    }
}
#endif
