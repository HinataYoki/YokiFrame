#if UNITY_5_3_OR_NEWER
using System;
using System.Collections;

namespace YokiFrame
{
    /// <summary>提供不污染纯 C# ActionKit 门面的 Unity 原生 Coroutine 创建入口。</summary>
    public static class ActionKitUnityCoroutine
    {
        /// <summary>创建每轮由 factory 提供 IEnumerator 的 Unity Coroutine Action。</summary>
        /// <param name="enumeratorFactory">每轮创建 Unity IEnumerator 的 factory。</param>
        /// <returns>可加入动作树或直接启动的 Action。</returns>
        public static IAction From(Func<IEnumerator> enumeratorFactory) =>
            UnityCoroutineAction.Allocate(enumeratorFactory);

        /// <summary>直接包装一次性 Unity IEnumerator；Repeat 必须改用 factory 入口。</summary>
        /// <param name="enumerator">待交给 Unity Coroutine 的枚举器。</param>
        /// <returns>可加入动作树或直接启动的 Action。</returns>
        public static IAction From(IEnumerator enumerator) => UnityCoroutineAction.Allocate(enumerator);
    }
}
#endif
