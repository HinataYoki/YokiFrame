using System;

namespace YokiFrame
{
    /// <summary>
    /// <see cref="ISequence"/> 的回调扩展方法。
    /// </summary>
    public static class CallbackExtension
    {
        /// <summary>
        /// 向序列中追加一个立即调用回调并完成的 Action。
        /// </summary>
        /// <param name="self">目标序列。</param>
        /// <param name="callback">执行时调用的回调。</param>
        public static ISequence Callback(this ISequence self, Action callback) =>
            self.Append(YokiFrame.Callback.Allocate(callback));
    }
}
