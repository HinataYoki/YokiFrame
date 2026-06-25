using System;
using System.Collections;

namespace YokiFrame
{
    /// <summary>
    /// <see cref="ISequence"/> 的枚举器 Action 扩展方法。
    /// </summary>
    public static class CoroutineExtension
    {
        /// <summary>
        /// 向序列中追加一个由 <see cref="IEnumerator"/> 驱动的 Action。
        /// </summary>
        /// <param name="self">目标序列。</param>
        /// <param name="coroutineGetter">返回枚举器的委托。</param>
        public static ISequence Coroutine(this ISequence self, Func<IEnumerator> coroutineGetter) =>
            self.Append(CoroutineAction.Allocate(coroutineGetter));

        /// <summary>
        /// 将枚举器包装为 Action。
        /// </summary>
        /// <param name="self">目标枚举器。</param>
        public static IAction ToAction(this IEnumerator self) => CoroutineAction.Allocate(() => self);
    }
}
