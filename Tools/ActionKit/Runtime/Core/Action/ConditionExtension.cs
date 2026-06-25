using System;

namespace YokiFrame
{
    /// <summary>
    /// <see cref="ISequence"/> 的条件等待扩展方法。
    /// </summary>
    public static class ConditionExtension
    {
        /// <summary>
        /// 向序列中追加一个条件满足即完成的 Action。
        /// </summary>
        /// <param name="self">目标序列。</param>
        /// <param name="condition">完成条件。</param>
        public static ISequence Condition(this ISequence self, Func<bool> condition) =>
            self.Append(YokiFrame.Condition.Allocate(condition));
    }
}
