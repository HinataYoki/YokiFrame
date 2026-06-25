using System;

namespace YokiFrame
{
    /// <summary>
    /// <see cref="ISequence"/> 的重复容器扩展方法。
    /// </summary>
    public static class RepeatExtension
    {
        /// <summary>
        /// 向序列中追加一个重复容器。
        /// </summary>
        /// <param name="self">目标序列。</param>
        /// <param name="repeat">重复容器配置回调。</param>
        /// <param name="count">重复次数；小于等于 0 表示不限制次数。</param>
        /// <param name="condition">继续重复的条件；为空时仅受重复次数限制。</param>
        public static ISequence Repeat(this ISequence self, Action<IRepeat> repeat, int count = -1, Func<bool> condition = null)
        {
            var repeatAction = YokiFrame.Repeat.Allocate(count, condition);
            repeat?.Invoke(repeatAction);
            return self.Append(repeatAction);
        }
    }
}
