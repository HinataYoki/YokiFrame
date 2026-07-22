using System;

namespace YokiFrame
{
    /// <summary>提供 ISequence 的 float 插值 fluent 扩展。</summary>
    public static class LerpExtension
    {
        /// <summary>
        /// 向容器追加自定义起止值的线性插值。
        /// </summary>
        /// <param name="self">目标顺序语义容器。</param>
        /// <param name="a">起始值。</param>
        /// <param name="b">目标值。</param>
        /// <param name="duration">有限持续秒数；小于等于零时同步输出起点和终点。</param>
        /// <param name="onLerp">每次推进时接收当前值的回调。</param>
        /// <param name="onLerpFinish">仅在正常完成时调用的回调。</param>
        /// <returns>原容器。</returns>
        public static ISequence Lerp(
            this ISequence self,
            float a,
            float b,
            float duration,
            Action<float> onLerp = null,
            Action onLerpFinish = null)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            return ActionRuntime.AppendCreated(self, YokiFrame.Lerp.Allocate(a, b, duration, onLerp, onLerpFinish));
        }

        /// <summary>
        /// 向容器追加从 0 到 1 的线性插值。
        /// </summary>
        /// <param name="self">目标顺序语义容器。</param>
        /// <param name="duration">有限持续秒数；小于等于零时同步输出 0 和 1。</param>
        /// <param name="onLerp">每次推进时接收当前值的回调。</param>
        /// <param name="onLerpFinish">仅在正常完成时调用的回调。</param>
        /// <returns>原容器。</returns>
        public static ISequence Lerp01(
            this ISequence self,
            float duration,
            Action<float> onLerp = null,
            Action onLerpFinish = null) => Lerp(self, 0f, 1f, duration, onLerp, onLerpFinish);
    }
}
