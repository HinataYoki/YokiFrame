using System;
using System.Collections;

namespace YokiFrame
{
    /// <summary>提供 ISequence 与 IEnumerator 的 CoroutineAction fluent 扩展。</summary>
    public static class CoroutineExtension
    {
        /// <summary>
        /// 向容器追加由 factory 创建的 IEnumerator。
        /// </summary>
        /// <param name="self">目标顺序语义容器。</param>
        /// <param name="coroutineGetter">每次 Repeat 轮次开始时创建枚举器的 factory。</param>
        /// <returns>原容器。</returns>
        public static ISequence Coroutine(this ISequence self, Func<IEnumerator> coroutineGetter)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            return ActionRuntime.AppendCreated(self, CoroutineAction.Allocate(coroutineGetter));
        }

        /// <summary>
        /// 直接把已有 IEnumerator 包装为一次性 Action，不创建捕获闭包；Repeat 后续轮会立即完成。
        /// </summary>
        /// <param name="self">待包装的一次性枚举器。</param>
        /// <returns>可加入动作树或直接启动的 Action。</returns>
        public static IAction ToAction(this IEnumerator self) => CoroutineAction.Allocate(self);
    }
}
