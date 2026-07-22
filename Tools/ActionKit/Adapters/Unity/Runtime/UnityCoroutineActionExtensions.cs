#if UNITY_5_3_OR_NEWER
using System;
using System.Collections;

namespace YokiFrame
{
    /// <summary>提供 ISequence 与 IEnumerator 的 Unity Coroutine Adapter fluent 扩展。</summary>
    public static class UnityCoroutineActionExtensions
    {
        /// <summary>向容器追加每轮由 factory 创建的 Unity Coroutine Action。</summary>
        /// <param name="self">目标顺序语义容器。</param>
        /// <param name="enumeratorFactory">每轮创建 Unity IEnumerator 的 factory。</param>
        /// <returns>原容器。</returns>
        public static ISequence UnityCoroutine(this ISequence self, Func<IEnumerator> enumeratorFactory)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            return ActionRuntime.AppendCreated(self, UnityCoroutineAction.Allocate(enumeratorFactory));
        }

        /// <summary>直接包装一次性 Unity IEnumerator；Repeat 必须改用 factory。</summary>
        /// <param name="self">待交给 Unity Coroutine 的一次性枚举器。</param>
        /// <returns>可加入动作树或直接启动的 Action。</returns>
        public static IAction ToUnityAction(this IEnumerator self) => UnityCoroutineAction.Allocate(self);
    }
}
#endif
