#if UNITY_2022_3_OR_NEWER
using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>内部动画扩展接口，支持从当前视觉值续播到目标状态，用于显隐反向中断时的平滑过渡。</summary>
    internal interface IUIAnimationInternal
    {
        /// <summary>从当前视觉值开始播放到动画配置的结束状态，时长按剩余距离缩放。</summary>
        /// <param name="target">目标 RectTransform。</param>
        /// <param name="onComplete">完成回调。</param>
        void PlayFromCurrent(RectTransform target, Action onComplete = null);
    }
}
#endif
