using System;

namespace YokiFrame
{
    /// <summary>
    /// <see cref="ISequence"/> 的插值扩展方法。
    /// </summary>
    public static class LerpExtension
    {
        /// <summary>
        /// 向序列中追加一个浮点插值 Action。
        /// </summary>
        /// <param name="self">目标序列。</param>
        /// <param name="a">起始值。</param>
        /// <param name="b">目标值。</param>
        /// <param name="duration">插值持续时间。</param>
        /// <param name="onLerp">每次更新时接收当前插值的回调。</param>
        /// <param name="onLerpFinish">插值完成时调用的回调。</param>
        public static ISequence Lerp(this ISequence self, float a, float b, float duration, Action<float> onLerp = null, Action onLerpFinish = null) =>
            self.Append(YokiFrame.Lerp.Allocate(a, b, duration, onLerp, onLerpFinish));

        /// <summary>
        /// 向序列中追加一个 0 到 1 的浮点插值 Action。
        /// </summary>
        /// <param name="self">目标序列。</param>
        /// <param name="duration">插值持续时间。</param>
        /// <param name="onLerp">每次更新时接收当前插值的回调。</param>
        /// <param name="onLerpFinish">插值完成时调用的回调。</param>
        public static ISequence Lerp01(this ISequence self, float duration, Action<float> onLerp = null, Action onLerpFinish = null) =>
            self.Append(YokiFrame.Lerp.Allocate(0, 1, duration, onLerp, onLerpFinish));
    }
}
